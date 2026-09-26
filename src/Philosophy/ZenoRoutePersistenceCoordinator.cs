namespace STS2Philosophers;

internal enum ZenoRoutePersistenceCoordinationStatus
{
    Completed,
    PreparationReleased,
    Frozen,
    Rejected,
    NoPendingRetry,
}

internal sealed record ZenoRoutePersistenceCoordinationResult(
    ZenoRoutePersistenceCoordinationStatus Status,
    ZenoRouteFeatureState State,
    ZenoRoutePersistenceStatus? PersistenceStatus,
    ZenoRoutePersistenceCheckpoint? Checkpoint,
    string? RequestId)
{
    public bool IsFrozen => Status == ZenoRoutePersistenceCoordinationStatus.Frozen;
}

internal sealed class ZenoRoutePersistenceCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ZenoRouteValidationCatalog _catalog;
    private ZenoRouteFeatureState _state;
    private FrozenRequest? _frozen;
    private FrozenRequest? _externalPrepared;

    public ZenoRoutePersistenceCoordinator(
        ZenoRouteFeatureState initialState,
        ZenoRouteValidationCatalog catalog)
    {
        if (!IsValidBase(initialState, catalog) || initialState.PendingOperation is not null)
        {
            throw new ArgumentException(
                "The coordinator requires a valid route without an unresolved write-ahead record.",
                nameof(initialState));
        }

        _state = initialState;
        _catalog = catalog;
    }

    private ZenoRoutePersistenceCoordinator(
        ZenoRouteFeatureState restoredState,
        ZenoRouteValidationCatalog catalog,
        ZenoRoutePersistenceRequest restoredRequest,
        ZenoRouteFeatureState sourceState)
    {
        _state = restoredState;
        _catalog = catalog;
        FrozenRequest restored = new(
            restoredRequest,
            restoredState,
            sourceState,
            restoredState.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed
                ? ZenoRoutePersistenceStatus.Confirmed
                : ZenoRoutePersistenceStatus.Unknown);
        if (restoredState.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed)
        {
            _externalPrepared = restored;
        }
        else
        {
            _frozen = restored;
        }
    }

    public static ZenoRoutePersistenceCoordinator Restore(
        ZenoRouteFeatureState state,
        ZenoRouteValidationCatalog catalog)
    {
        if (!IsValidBase(state, catalog))
        {
            throw new ArgumentException(
                "The restored coordinator state must contain a valid route.",
                nameof(state));
        }

        if (state.PendingOperation is null)
        {
            return new ZenoRoutePersistenceCoordinator(state, catalog);
        }

        if (!ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(
                state,
                catalog,
                out ZenoRoutePersistenceRequest? request) ||
            request is null)
        {
            throw new ArgumentException(
                "The restored write-ahead record could not produce its persistence request.",
                nameof(state));
        }

        ZenoRouteFeatureState sourceState = state with { PendingOperation = null };
        if (!IsValidBase(sourceState, catalog))
        {
            throw new ArgumentException(
                "The restored write-ahead record did not retain a valid source state.",
                nameof(state));
        }

        return new ZenoRoutePersistenceCoordinator(
            state,
            catalog,
            request,
            sourceState);
    }

    public ZenoRouteFeatureState CurrentState => _state;

    public string? PendingRequestId => (_frozen ?? _externalPrepared)?.Request.RequestId;

    public ZenoRoutePersistenceCheckpoint? PendingCheckpoint =>
        (_frozen ?? _externalPrepared)?.Request.Checkpoint;

    public bool HasConfirmedExternalPreparation => _externalPrepared is not null;

    public async Task<ZenoRoutePersistenceCoordinationResult> PrepareExternalAsync(
        ZenoRouteTransitionResult preparedTransition,
        IZenoRoutePersistenceConfirmationAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_externalPrepared is not null)
            {
                return FrozenResult(_externalPrepared, ZenoRoutePersistenceStatus.Confirmed);
            }

            if (_frozen is not null)
            {
                return FrozenResult(_frozen, _frozen.LastStatus);
            }

            if (!IsPreparedFromCurrent(preparedTransition) ||
                preparedTransition.State.PendingOperation?.Kind != ZenoRouteOperationKind.EventClosed)
            {
                return Result(ZenoRoutePersistenceCoordinationStatus.Rejected);
            }

            ZenoRouteFeatureState sourceState = _state;
            _state = preparedTransition.State;
            if (!ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(
                    _state,
                    _catalog,
                    out ZenoRoutePersistenceRequest? request) ||
                request is null)
            {
                _state = sourceState;
                return Result(ZenoRoutePersistenceCoordinationStatus.Rejected);
            }

            ZenoRoutePersistenceResult persistence = await InvokeAdapterAsync(
                adapter,
                request,
                _state,
                cancellationToken);
            if (persistence.Status == ZenoRoutePersistenceStatus.Failed)
            {
                _state = sourceState;
                return Result(
                    ZenoRoutePersistenceCoordinationStatus.PreparationReleased,
                    persistence.Status,
                    request);
            }

            FrozenRequest external = new(
                request,
                _state,
                sourceState,
                persistence.Status);
            if (persistence.Status == ZenoRoutePersistenceStatus.Confirmed)
            {
                _externalPrepared = external;
                return FrozenResult(external, persistence.Status);
            }

            _frozen = external;
            return FrozenResult(external, persistence.Status);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ZenoRoutePersistenceCoordinationResult> CommitExternalAsync(
        IZenoRoutePersistenceConfirmationAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_externalPrepared is null || _state.PendingOperation is not { } pending ||
                pending.Kind != ZenoRouteOperationKind.EventClosed)
            {
                return Result(ZenoRoutePersistenceCoordinationStatus.Rejected);
            }

            ZenoRouteTransitionResult committed = ZenoRouteStateService.Commit(
                _state,
                pending.OperationId,
                pending.CandidateDigest,
                _catalog);
            if (committed.Status is not (
                    ZenoRouteTransitionStatus.Committed or
                    ZenoRouteTransitionStatus.CommitReused))
            {
                return Result(ZenoRoutePersistenceCoordinationStatus.Rejected);
            }

            _state = committed.State;
            _externalPrepared = null;
            if (!ZenoRoutePersistenceConfirmation.TryCreateCommittedRequest(
                    _state,
                    _catalog,
                    out ZenoRoutePersistenceRequest? request) ||
                request is null)
            {
                throw new InvalidOperationException(
                    "The externally committed route could not create its persistence request.");
            }

            ZenoRoutePersistenceResult persistence = await InvokeAdapterAsync(
                adapter,
                request,
                _state,
                cancellationToken);
            return HandleCommittedResult(request, persistence);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ZenoRoutePersistenceCoordinationResult> ExecuteAsync(
        ZenoRouteTransitionResult preparedTransition,
        IZenoRoutePersistenceConfirmationAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_frozen is not null)
            {
                return FrozenResult(_frozen, _frozen.LastStatus);
            }

            if (_externalPrepared is not null)
            {
                return FrozenResult(_externalPrepared, ZenoRoutePersistenceStatus.Confirmed);
            }

            if (!IsPreparedFromCurrent(preparedTransition))
            {
                return Result(ZenoRoutePersistenceCoordinationStatus.Rejected);
            }

            ZenoRouteFeatureState sourceState = _state;
            _state = preparedTransition.State;
            if (!ZenoRoutePersistenceConfirmation.TryCreatePreparedRequest(
                    _state,
                    _catalog,
                    out ZenoRoutePersistenceRequest? request) ||
                request is null)
            {
                _state = sourceState;
                return Result(ZenoRoutePersistenceCoordinationStatus.Rejected);
            }

            ZenoRoutePersistenceResult persistence = await InvokeAdapterAsync(
                adapter,
                request,
                _state,
                cancellationToken);
            return await HandlePreparedResultAsync(
                sourceState,
                request,
                persistence,
                adapter,
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ZenoRoutePersistenceCoordinationResult> RetryAsync(
        IZenoRoutePersistenceConfirmationAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_frozen is null)
            {
                return _externalPrepared is null
                    ? Result(ZenoRoutePersistenceCoordinationStatus.NoPendingRetry)
                    : FrozenResult(_externalPrepared, ZenoRoutePersistenceStatus.Confirmed);
            }

            FrozenRequest frozen = _frozen;
            ZenoRoutePersistenceResult persistence = await InvokeAdapterAsync(
                adapter,
                frozen.Request,
                frozen.SaveState,
                cancellationToken);
            return frozen.Request.Checkpoint == ZenoRoutePersistenceCheckpoint.Prepared
                ? await HandlePreparedResultAsync(
                    frozen.SourceState ?? throw new InvalidOperationException(
                        "A prepared retry requires its source state."),
                    frozen.Request,
                    persistence,
                    adapter,
                    cancellationToken)
                : HandleCommittedResult(frozen.Request, persistence);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ZenoRoutePersistenceCoordinationResult> HandlePreparedResultAsync(
        ZenoRouteFeatureState sourceState,
        ZenoRoutePersistenceRequest request,
        ZenoRoutePersistenceResult persistence,
        IZenoRoutePersistenceConfirmationAdapter adapter,
        CancellationToken cancellationToken)
    {
        if (persistence.Status == ZenoRoutePersistenceStatus.Failed)
        {
            _state = sourceState;
            _frozen = null;
            return Result(
                ZenoRoutePersistenceCoordinationStatus.PreparationReleased,
                persistence.Status,
                request);
        }

        if (persistence.Status == ZenoRoutePersistenceStatus.Unknown)
        {
            _frozen = new FrozenRequest(request, _state, sourceState, persistence.Status);
            return FrozenResult(_frozen, persistence.Status);
        }

        ZenoRoutePendingOperation pending = _state.PendingOperation ??
            throw new InvalidOperationException("A confirmed prepared request lost its write-ahead record.");
        ZenoRouteTransitionResult committed = ZenoRouteStateService.Commit(
            _state,
            pending.OperationId,
            pending.CandidateDigest,
            _catalog);
        if (committed.Status is not (
                ZenoRouteTransitionStatus.Committed or
                ZenoRouteTransitionStatus.CommitReused))
        {
            _frozen = new FrozenRequest(request, _state, sourceState, ZenoRoutePersistenceStatus.Unknown);
            return FrozenResult(_frozen, ZenoRoutePersistenceStatus.Unknown);
        }

        _state = committed.State;
        if (!ZenoRoutePersistenceConfirmation.TryCreateCommittedRequest(
                _state,
                _catalog,
                out ZenoRoutePersistenceRequest? committedRequest) ||
            committedRequest is null)
        {
            throw new InvalidOperationException("A committed route could not create its persistence request.");
        }

        ZenoRoutePersistenceResult committedPersistence = await InvokeAdapterAsync(
            adapter,
            committedRequest,
            _state,
            cancellationToken);
        return HandleCommittedResult(committedRequest, committedPersistence);
    }

    private ZenoRoutePersistenceCoordinationResult HandleCommittedResult(
        ZenoRoutePersistenceRequest request,
        ZenoRoutePersistenceResult persistence)
    {
        if (persistence.Status == ZenoRoutePersistenceStatus.Confirmed)
        {
            _frozen = null;
            return Result(
                ZenoRoutePersistenceCoordinationStatus.Completed,
                persistence.Status,
                request);
        }

        _frozen = new FrozenRequest(request, _state, null, persistence.Status);
        return FrozenResult(_frozen, persistence.Status);
    }

    private async Task<ZenoRoutePersistenceResult> InvokeAdapterAsync(
        IZenoRoutePersistenceConfirmationAdapter adapter,
        ZenoRoutePersistenceRequest request,
        ZenoRouteFeatureState state,
        CancellationToken cancellationToken)
    {
        try
        {
            ZenoRoutePersistenceResult result = await adapter.PersistAsync(
                request,
                state,
                cancellationToken);
            return ZenoRoutePersistenceConfirmation.ValidateAdapterResult(request, result);
        }
        catch (Exception)
        {
            return ZenoRoutePersistenceConfirmation.FromSaveTaskCompletion(request);
        }
    }

    private bool IsPreparedFromCurrent(ZenoRouteTransitionResult transition)
    {
        if (transition.Status is not (
                ZenoRouteTransitionStatus.Prepared or
                ZenoRouteTransitionStatus.PendingReused) ||
            transition.State.Route is null ||
            transition.State.PendingOperation is null ||
            _state.Route is null ||
            _state.PendingOperation is not null ||
            transition.State.FeatureGeneration != _state.FeatureGeneration ||
            !string.Equals(
                transition.State.Route.RunId,
                _state.Route.RunId,
                StringComparison.Ordinal) ||
            !string.Equals(
                ZenoRouteStateCodec.ComputeCandidateDigest(transition.State.Route),
                ZenoRouteStateCodec.ComputeCandidateDigest(_state.Route),
                StringComparison.Ordinal))
        {
            return false;
        }

        return ZenoRouteStateCodec.IsValidFeature(transition.State, _catalog);
    }

    private static bool IsValidBase(
        ZenoRouteFeatureState state,
        ZenoRouteValidationCatalog catalog)
    {
        return state.Route is not null && ZenoRouteStateCodec.IsValidFeature(state, catalog);
    }

    private ZenoRoutePersistenceCoordinationResult FrozenResult(
        FrozenRequest frozen,
        ZenoRoutePersistenceStatus status)
    {
        return new ZenoRoutePersistenceCoordinationResult(
            ZenoRoutePersistenceCoordinationStatus.Frozen,
            _state,
            status,
            frozen.Request.Checkpoint,
            frozen.Request.RequestId);
    }

    private ZenoRoutePersistenceCoordinationResult Result(
        ZenoRoutePersistenceCoordinationStatus status,
        ZenoRoutePersistenceStatus? persistenceStatus = null,
        ZenoRoutePersistenceRequest? request = null)
    {
        return new ZenoRoutePersistenceCoordinationResult(
            status,
            _state,
            persistenceStatus,
            request?.Checkpoint,
            request?.RequestId);
    }

    private sealed record FrozenRequest(
        ZenoRoutePersistenceRequest Request,
        ZenoRouteFeatureState SaveState,
        ZenoRouteFeatureState? SourceState,
        ZenoRoutePersistenceStatus LastStatus);
}
