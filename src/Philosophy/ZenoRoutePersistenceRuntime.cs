namespace STS2Philosophers;

internal sealed class ZenoRoutePersistenceRuntime
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly PhilosophyRunState _sharedState;
    private readonly ZenoRouteValidationCatalog _catalog;
    private readonly IZenoRoutePersistenceConfirmationAdapter _adapter;
    private readonly ZenoRoutePersistenceCoordinator _coordinator;

    private ZenoRoutePersistenceRuntime(
        PhilosophyRunState sharedState,
        ZenoRouteValidationCatalog catalog,
        IZenoRoutePersistenceConfirmationAdapter adapter,
        ZenoRoutePersistenceCoordinator coordinator)
    {
        _sharedState = sharedState;
        _catalog = catalog;
        _adapter = adapter;
        _coordinator = coordinator;
        SynchronizeSharedState(coordinator.CurrentState);
    }

    public ZenoRouteFeatureState CurrentState => _coordinator.CurrentState;

    public string? PendingRequestId => _coordinator.PendingRequestId;

    public ZenoRoutePersistenceCheckpoint? PendingCheckpoint => _coordinator.PendingCheckpoint;

    public static bool TryCreate(
        PhilosophyRunState sharedState,
        ZenoRouteValidationCatalog catalog,
        IZenoRoutePersistenceConfirmationAdapter adapter,
        out ZenoRoutePersistenceRuntime? runtime)
    {
        runtime = null;
        ZenoRouteDecodeResult payload = sharedState.ZenoRoutePayload;
        if (payload.Classification != ZenoRoutePayloadClassification.Current ||
            payload.State is null ||
            payload.State.Route is null ||
            !ZenoRouteStateCodec.IsValidFeature(payload.State, catalog))
        {
            return false;
        }

        try
        {
            ZenoRoutePersistenceCoordinator coordinator =
                ZenoRoutePersistenceCoordinator.Restore(payload.State, catalog);
            runtime = new ZenoRoutePersistenceRuntime(
                sharedState,
                catalog,
                adapter,
                coordinator);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool TryRecoverIsolatedSharedState(
        PhilosophyRunState isolatedState,
        ZenoRouteValidationCatalog catalog,
        out PhilosophyRunState? recoveredState)
    {
        recoveredState = null;
        if (isolatedState.PreservedSaveMarkerEntries.Count == 0 ||
            isolatedState.ZenoRoutePayload.Classification == ZenoRoutePayloadClassification.Current)
        {
            return false;
        }

        PhilosophyRunStateMarkerRestoreResult restored =
            PhilosophyRunStateMarkerCarrier.Restore(
                isolatedState.PreservedSaveMarkerEntries,
                catalog);
        if (restored.Classification is not (
                PhilosophyRunStateMarkerClassification.Current or
                PhilosophyRunStateMarkerClassification.IdenticalCurrentDuplicates) ||
            restored.State?.ZenoRoutePayload.Classification != ZenoRoutePayloadClassification.Current)
        {
            return false;
        }

        recoveredState = restored.State;
        return true;
    }

    public async Task<ZenoRoutePersistenceCoordinationResult> ExecuteAsync(
        ZenoRouteTransitionResult preparedTransition,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                return await _coordinator.ExecuteAsync(
                    preparedTransition,
                    _adapter,
                    cancellationToken);
            }
            finally
            {
                SynchronizeSharedState(_coordinator.CurrentState);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ZenoRoutePersistenceCoordinationResult> RetryAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                return await _coordinator.RetryAsync(_adapter, cancellationToken);
            }
            finally
            {
                SynchronizeSharedState(_coordinator.CurrentState);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ZenoRouteRecoveryExecutionResult> RecoverAsync(
        IZenoRouteRecoverySceneProbe sceneProbe,
        IZenoRouteRecoveryActionExecutor actionExecutor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sceneProbe);
        ArgumentNullException.ThrowIfNull(actionExecutor);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ZenoRouteRecoveryScene scene;
            try
            {
                scene = await sceneProbe.ReadAsync(_coordinator.CurrentState, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return new ZenoRouteRecoveryExecutionResult(
                    ZenoRouteRecoveryExecutionStatus.SceneUnavailable,
                    null);
            }

            ZenoRouteRecoveryPlan plan = ZenoRouteRecoveryPlanner.CreatePlan(
                _coordinator.CurrentState,
                scene,
                _catalog);
            if (plan.Action == ZenoRouteRecoveryAction.Isolate)
            {
                return new ZenoRouteRecoveryExecutionResult(
                    ZenoRouteRecoveryExecutionStatus.Isolated,
                    plan);
            }

            if (plan.Action == ZenoRouteRecoveryAction.CompleteClosing)
            {
                return new ZenoRouteRecoveryExecutionResult(
                    ZenoRouteRecoveryExecutionStatus.ExternalEffectBlocked,
                    plan);
            }

            if (plan.Action == ZenoRouteRecoveryAction.RetryPendingTransaction)
            {
                ZenoRoutePersistenceCoordinationResult persistence;
                try
                {
                    persistence = await _coordinator.RetryAsync(_adapter, cancellationToken);
                }
                finally
                {
                    SynchronizeSharedState(_coordinator.CurrentState);
                }

                return new ZenoRouteRecoveryExecutionResult(
                    MapPersistenceStatus(persistence.Status),
                    plan,
                    persistence);
            }

            try
            {
                bool executed = await actionExecutor.ExecuteAsync(plan, cancellationToken);
                return new ZenoRouteRecoveryExecutionResult(
                    executed
                        ? ZenoRouteRecoveryExecutionStatus.Completed
                        : ZenoRouteRecoveryExecutionStatus.ActionFailed,
                    plan);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return new ZenoRouteRecoveryExecutionResult(
                    ZenoRouteRecoveryExecutionStatus.ActionFailed,
                    plan);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal bool Owns(
        PhilosophyRunState sharedState,
        ZenoRouteValidationCatalog catalog)
    {
        return ReferenceEquals(_sharedState, sharedState) &&
               ReferenceEquals(_catalog, catalog);
    }

    private void SynchronizeSharedState(ZenoRouteFeatureState state)
    {
        _sharedState.SetCurrentZenoRouteState(state, _catalog);
    }

    private static ZenoRouteRecoveryExecutionStatus MapPersistenceStatus(
        ZenoRoutePersistenceCoordinationStatus status) =>
        status switch
        {
            ZenoRoutePersistenceCoordinationStatus.Completed =>
                ZenoRouteRecoveryExecutionStatus.Completed,
            ZenoRoutePersistenceCoordinationStatus.Frozen =>
                ZenoRouteRecoveryExecutionStatus.Frozen,
            ZenoRoutePersistenceCoordinationStatus.PreparationReleased =>
                ZenoRouteRecoveryExecutionStatus.PreparationReleased,
            _ => ZenoRouteRecoveryExecutionStatus.PersistenceRejected,
        };
}
