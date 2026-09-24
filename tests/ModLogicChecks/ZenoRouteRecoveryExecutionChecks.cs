using STS2Philosophers;

internal static class ZenoRouteRecoveryExecutionChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string CurrentStatement = "STATEMENT_CURRENT";
    private const string NarrowStatement = "STATEMENT_NARROW";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [CurrentStatement, NarrowStatement],
        []);

    public static void Run()
    {
        StablePageRecoveryUsesThePlannedAction();
        SavedPreparationRetriesInsideTheRecoveryLock();
        ContradictionsAndExternalClosingDoNotExecuteEffects();
        ProbeAndExecutorFailuresStopWithoutChangingState();
        RecoveryCallsAreSerializedPerRuntime();
        Console.WriteLine("Zeno recovery execution checks passed: locked scene reads, same-transaction retries and blocked external closing.");
    }

    private static void StablePageRecoveryUsesThePlannedAction()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        PhilosophyRunState shared = Shared(initial);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                new ScriptedAdapter(),
                out ZenoRoutePersistenceRuntime? runtime),
            "A valid route should create a recovery runtime.");
        RecordingExecutor executor = new();

        ZenoRouteRecoveryExecutionResult result = runtime!.RecoverAsync(
            new FixedProbe(new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.Diogenes,
                ZenoRouteDestinationPresence.NotApplicable)),
            executor).GetAwaiter().GetResult();

        Assert(result.Status == ZenoRouteRecoveryExecutionStatus.Completed &&
               result.Plan?.Action == ZenoRouteRecoveryAction.RestoreDiogenesChoice,
            "A matching stable scene should execute its only planned page action.");
        Assert(executor.Plans.Count == 1 && executor.Plans[0] == result.Plan,
            "The executor must receive the exact immutable plan produced under the recovery lock.");
        Assert(shared.ZenoRoutePayload.State == initial,
            "A page recovery action must not change persisted route facts.");
    }

    private static void SavedPreparationRetriesInsideTheRecoveryLock()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        ZenoRouteFeatureState prepared = ZenoRouteStateService.PrepareSwitch(
            initial,
            0,
            CreateMaterial(),
            Catalog).State;
        PhilosophyRunState shared = Shared(prepared);
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                adapter,
                out ZenoRoutePersistenceRuntime? runtime),
            "A saved preparation should restore its runtime retry.");
        RecordingExecutor executor = new();

        ZenoRouteRecoveryExecutionResult result = runtime!.RecoverAsync(
            new FixedProbe(new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.Diogenes,
                ZenoRouteDestinationPresence.NotApplicable)),
            executor).GetAwaiter().GetResult();

        Assert(result.Status == ZenoRouteRecoveryExecutionStatus.Completed &&
               result.Plan?.Action == ZenoRouteRecoveryAction.RetryPendingTransaction &&
               result.PersistenceResult?.Status == ZenoRoutePersistenceCoordinationStatus.Completed,
            "A matching write-ahead scene must retry the existing transaction to completion.");
        Assert(adapter.Requests.Count == 2 &&
               adapter.Requests[0].Checkpoint == ZenoRoutePersistenceCheckpoint.Prepared &&
               adapter.Requests[1].Checkpoint == ZenoRoutePersistenceCheckpoint.Committed,
            "Recovery must confirm the saved preparation and its committed target without creating another operation.");
        Assert(executor.Plans.Count == 0,
            "Transaction retries belong to the runtime and must not be delegated as page effects.");
        Assert(shared.ZenoRoutePayload.State?.Route?.Stage == ZenoRouteStage.WaitingInterval &&
               shared.ZenoRoutePayload.State.PendingOperation is null,
            "A completed retry must synchronize the confirmed target into shared run state.");
    }

    private static void ContradictionsAndExternalClosingDoNotExecuteEffects()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        PhilosophyRunState shared = Shared(initial);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                new ScriptedAdapter(),
                out ZenoRoutePersistenceRuntime? runtime),
            "The contradiction fixture should create a runtime.");
        RecordingExecutor executor = new();

        ZenoRouteRecoveryExecutionResult isolated = runtime!.RecoverAsync(
            new FixedProbe(new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.None,
                ZenoRouteDestinationPresence.NotApplicable)),
            executor).GetAwaiter().GetResult();
        Assert(isolated.Status == ZenoRouteRecoveryExecutionStatus.Isolated &&
               executor.Plans.Count == 0,
            "A contradictory scene must isolate before any page or persistence effect.");

        ZenoRouteFeatureState outcome = CreateOutcomeCommitted();
        ZenoRouteFeatureState closePrepared = ZenoRouteStateService.PrepareClose(
            outcome,
            outcome.Route!.Revision,
            Catalog).State;
        PhilosophyRunState closingShared = Shared(closePrepared);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                closingShared,
                Catalog,
                new ScriptedAdapter(),
                out ZenoRoutePersistenceRuntime? closingRuntime),
            "A saved close preparation should create a recovery runtime.");

        ZenoRouteRecoveryExecutionResult blocked = closingRuntime!.RecoverAsync(
            new FixedProbe(new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.MatchingZeno,
                ZenoRouteDestinationPresence.Pending)),
            executor).GetAwaiter().GetResult();
        Assert(blocked.Status == ZenoRouteRecoveryExecutionStatus.ExternalEffectBlocked &&
               blocked.Plan?.Action == ZenoRouteRecoveryAction.CompleteClosing &&
               executor.Plans.Count == 0,
            "Closing must remain blocked until the game adapter can prove the same destination was restored.");
        Assert(closingShared.ZenoRoutePayload.State?.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed,
            "Blocking external closing must retain the original write-ahead checkpoint.");
    }

    private static void ProbeAndExecutorFailuresStopWithoutChangingState()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        PhilosophyRunState shared = Shared(initial);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                new ScriptedAdapter(),
                out ZenoRoutePersistenceRuntime? runtime),
            "The failure fixture should create a runtime.");

        ZenoRouteRecoveryExecutionResult probeFailure = runtime!.RecoverAsync(
            new ThrowingProbe(),
            new RecordingExecutor()).GetAwaiter().GetResult();
        Assert(probeFailure.Status == ZenoRouteRecoveryExecutionStatus.SceneUnavailable &&
               probeFailure.Plan is null,
            "A failed scene read must stop before inventing a recovery plan.");

        ZenoRouteRecoveryExecutionResult executorFailure = runtime.RecoverAsync(
            new FixedProbe(new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.Diogenes,
                ZenoRouteDestinationPresence.NotApplicable)),
            new RecordingExecutor(shouldThrow: true)).GetAwaiter().GetResult();
        Assert(executorFailure.Status == ZenoRouteRecoveryExecutionStatus.ActionFailed,
            "A failed page adapter must report failure without claiming the action completed.");
        Assert(shared.ZenoRoutePayload.State == initial,
            "Probe and page failures must not rewrite route facts.");
    }

    private static void RecoveryCallsAreSerializedPerRuntime()
    {
        PhilosophyRunState shared = Shared(CreateUnresolved());
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                new ScriptedAdapter(),
                out ZenoRoutePersistenceRuntime? runtime),
            "The serialization fixture should create a runtime.");
        BlockingProbe probe = new();
        RecordingExecutor executor = new();

        Task<ZenoRouteRecoveryExecutionResult> first = runtime!.RecoverAsync(probe, executor);
        probe.FirstReadStarted.Task.GetAwaiter().GetResult();
        Task<ZenoRouteRecoveryExecutionResult> second = runtime.RecoverAsync(probe, executor);
        Assert(probe.ReadCount == 1 && !second.IsCompleted,
            "A second recovery must wait before reading the room scene.");

        probe.ReleaseFirstRead.SetResult();
        Task.WhenAll(first, second).GetAwaiter().GetResult();
        Assert(probe.ReadCount == 2 && executor.Plans.Count == 2,
            "Both recoveries may complete only after acquiring the same per-run lock in order.");
    }

    private static ZenoRouteFeatureState CreateOutcomeCommitted()
    {
        ZenoRouteFeatureState state = Commit(ZenoRouteStateService.PrepareSwitch(
            CreateUnresolved(),
            0,
            CreateMaterial(),
            Catalog).State);
        state = Commit(ZenoRouteStateService.PrepareOpeningClaim(
            state,
            1,
            ZenoOpeningTrigger.BeforeBoss,
            ZenoResumeDestination.Boss,
            "ACT_THREE_BOSS_001",
            "ZENO_EVENT_001",
            Catalog).State);
        state = Commit(ZenoRouteStateService.PrepareEventEstablished(state, 2, Catalog).State);
        return Commit(ZenoRouteStateService.PrepareOutcome(
            state,
            3,
            ZenoAssentOutcome.Narrow,
            NarrowStatement,
            Catalog).State);
    }

    private static ZenoRouteFeatureState Commit(ZenoRouteFeatureState prepared)
    {
        ZenoRoutePendingOperation pending = prepared.PendingOperation ??
            throw new InvalidOperationException("Expected pending operation.");
        return ZenoRouteStateService.Commit(
            prepared,
            pending.OperationId,
            pending.CandidateDigest,
            Catalog).State;
    }

    private static PhilosophyRunState Shared(ZenoRouteFeatureState state)
    {
        PhilosophyRunState shared = new();
        shared.SetCurrentZenoRouteState(state, Catalog);
        return shared;
    }

    private static ZenoRouteFeatureState CreateUnresolved() =>
        new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_RECOVERY_EXECUTION_001",
                2,
                ZenoRouteIds.RouteEdge,
                ZenoRouteIds.TerminalNode,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));

    private static ZenoAssentMaterialSnapshot CreateMaterial() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [CurrentStatement],
            CurrentStatement,
            NarrowStatement,
            null);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixedProbe(ZenoRouteRecoveryScene scene) : IZenoRouteRecoverySceneProbe
    {
        public Task<ZenoRouteRecoveryScene> ReadAsync(
            ZenoRouteFeatureState state,
            CancellationToken cancellationToken = default) => Task.FromResult(scene);
    }

    private sealed class ThrowingProbe : IZenoRouteRecoverySceneProbe
    {
        public Task<ZenoRouteRecoveryScene> ReadAsync(
            ZenoRouteFeatureState state,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Scene unavailable.");
    }

    private sealed class BlockingProbe : IZenoRouteRecoverySceneProbe
    {
        public TaskCompletionSource FirstReadStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstRead { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public int ReadCount { get; private set; }

        public async Task<ZenoRouteRecoveryScene> ReadAsync(
            ZenoRouteFeatureState state,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            if (ReadCount == 1)
            {
                FirstReadStarted.SetResult();
                await ReleaseFirstRead.Task.WaitAsync(cancellationToken);
            }

            return new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.Diogenes,
                ZenoRouteDestinationPresence.NotApplicable);
        }
    }

    private sealed class RecordingExecutor(bool shouldThrow = false) : IZenoRouteRecoveryActionExecutor
    {
        public List<ZenoRouteRecoveryPlan> Plans { get; } = [];

        public Task<bool> ExecuteAsync(
            ZenoRouteRecoveryPlan plan,
            CancellationToken cancellationToken = default)
        {
            if (shouldThrow)
            {
                throw new InvalidOperationException("Page recovery failed.");
            }

            Plans.Add(plan);
            return Task.FromResult(true);
        }
    }

    private sealed class ScriptedAdapter(params ZenoRoutePersistenceStatus[] statuses)
        : IZenoRoutePersistenceConfirmationAdapter
    {
        private readonly Queue<ZenoRoutePersistenceStatus> _statuses = new(statuses);
        public List<ZenoRoutePersistenceRequest> Requests { get; } = [];

        public Task<ZenoRoutePersistenceResult> PersistAsync(
            ZenoRoutePersistenceRequest request,
            ZenoRouteFeatureState state,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            ZenoRoutePersistenceStatus status = _statuses.Count > 0
                ? _statuses.Dequeue()
                : throw new InvalidOperationException("No scripted persistence result remains.");
            return Task.FromResult(new ZenoRoutePersistenceResult(request.RequestId, status));
        }
    }
}
