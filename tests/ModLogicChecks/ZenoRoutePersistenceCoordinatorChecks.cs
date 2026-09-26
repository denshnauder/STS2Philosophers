using STS2Philosophers;

internal static class ZenoRoutePersistenceCoordinatorChecks
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
        CompletedTransactionPersistsBothCheckpoints();
        PreparationFailureReleasesTheSourceChoice();
        PreparationUnknownFreezesAndRetriesTheSameRequest();
        CommitUnknownRetriesWithoutRepeatingPreparation();
        CommitFailureRemainsFrozenAfterPreparationConfirmation();
        ExternalClosePersistsAroundTheExternalEffect();
        RestoredClosePreparationWaitsForTheExternalEffect();
        CompetingCallbackCannotPersistASecondWinner();
        AdapterExceptionBecomesUnknown();

        Console.WriteLine("Zeno persistence coordinator checks passed: per-run serialization, freeze and stable retries.");
    }

    private static void CompletedTransactionPersistsBothCheckpoints()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        ZenoRoutePersistenceCoordinator coordinator = new(initial, Catalog);
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);

        ZenoRoutePersistenceCoordinationResult result = coordinator.ExecuteAsync(
                PrepareSwitch(initial),
                adapter)
            .GetAwaiter()
            .GetResult();

        Assert(result.Status == ZenoRoutePersistenceCoordinationStatus.Completed,
            "Both confirmed checkpoints should complete the transaction.");
        Assert(result.State.Route?.Stage == ZenoRouteStage.WaitingInterval,
            "A completed Switch should reach its target stage.");
        Assert(result.State.PendingOperation is null, "A completed transaction should clear its write-ahead record.");
        Assert(adapter.Requests.Select(request => request.Checkpoint).SequenceEqual(
                [ZenoRoutePersistenceCheckpoint.Prepared, ZenoRoutePersistenceCheckpoint.Committed]),
            "The coordinator should persist preparation before commitment.");
    }

    private static void PreparationFailureReleasesTheSourceChoice()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        ZenoRoutePersistenceCoordinator coordinator = new(initial, Catalog);
        ScriptedAdapter adapter = new(ZenoRoutePersistenceStatus.Failed);

        ZenoRoutePersistenceCoordinationResult result = coordinator.ExecuteAsync(
                PrepareSwitch(initial),
                adapter)
            .GetAwaiter()
            .GetResult();

        Assert(result.Status == ZenoRoutePersistenceCoordinationStatus.PreparationReleased,
            "Confirmed non-persistence should release a preparation.");
        Assert(result.State.Route?.Stage == ZenoRouteStage.Unresolved && result.State.PendingOperation is null,
            "A released preparation should restore only the source route state.");

        ScriptedAdapter retry = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        ZenoRoutePersistenceCoordinationResult stay = coordinator.ExecuteAsync(
                ZenoRouteStateService.PrepareStay(result.State, 0, Catalog),
                retry)
            .GetAwaiter()
            .GetResult();
        Assert(stay.Status == ZenoRoutePersistenceCoordinationStatus.Completed,
            "A different choice may proceed only after confirmed preparation failure.");
    }

    private static void PreparationUnknownFreezesAndRetriesTheSameRequest()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        ZenoRoutePersistenceCoordinator coordinator = new(initial, Catalog);
        ScriptedAdapter unknown = new(ZenoRoutePersistenceStatus.Unknown);

        ZenoRoutePersistenceCoordinationResult first = coordinator.ExecuteAsync(
                PrepareSwitch(initial),
                unknown)
            .GetAwaiter()
            .GetResult();
        Assert(first.IsFrozen && first.Checkpoint == ZenoRoutePersistenceCheckpoint.Prepared,
            "An unknown preparation should freeze at the prepared checkpoint.");

        ScriptedAdapter loser = new(ZenoRoutePersistenceStatus.Confirmed);
        ZenoRoutePersistenceCoordinationResult competing = coordinator.ExecuteAsync(
                ZenoRouteStateService.PrepareStay(initial, 0, Catalog),
                loser)
            .GetAwaiter()
            .GetResult();
        Assert(competing.IsFrozen && loser.Requests.Count == 0,
            "A competing choice must not invoke persistence while the winner is frozen.");

        ScriptedAdapter retry = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        ZenoRoutePersistenceCoordinationResult completed = coordinator.RetryAsync(retry)
            .GetAwaiter()
            .GetResult();
        Assert(completed.Status == ZenoRoutePersistenceCoordinationStatus.Completed,
            "A confirmed retry should resume the same transaction through commitment.");
        Assert(retry.Requests[0].RequestId == first.RequestId,
            "The prepared retry must reuse the frozen request identity.");
    }

    private static void CommitUnknownRetriesWithoutRepeatingPreparation()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        ZenoRoutePersistenceCoordinator coordinator = new(initial, Catalog);
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Unknown);

        ZenoRoutePersistenceCoordinationResult first = coordinator.ExecuteAsync(
                PrepareSwitch(initial),
                adapter)
            .GetAwaiter()
            .GetResult();
        Assert(first.IsFrozen && first.Checkpoint == ZenoRoutePersistenceCheckpoint.Committed,
            "An unknown commit should freeze at the committed checkpoint.");
        Assert(first.State.Route?.Stage == ZenoRouteStage.WaitingInterval && first.State.PendingOperation is null,
            "The in-memory target should remain committed while input is frozen.");

        ScriptedAdapter retry = new(ZenoRoutePersistenceStatus.Confirmed);
        ZenoRoutePersistenceCoordinationResult completed = coordinator.RetryAsync(retry)
            .GetAwaiter()
            .GetResult();
        Assert(completed.Status == ZenoRoutePersistenceCoordinationStatus.Completed,
            "A matching committed retry should complete without re-preparing.");
        Assert(retry.Requests.Count == 1 && retry.Requests[0].Checkpoint == ZenoRoutePersistenceCheckpoint.Committed,
            "A committed retry must not repeat preparation or application.");
        Assert(retry.Requests[0].RequestId == first.RequestId,
            "The committed retry must reuse the frozen request identity.");
    }

    private static void CommitFailureRemainsFrozenAfterPreparationConfirmation()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        ZenoRoutePersistenceCoordinator coordinator = new(initial, Catalog);
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Failed);

        ZenoRoutePersistenceCoordinationResult result = coordinator.ExecuteAsync(
                PrepareSwitch(initial),
                adapter)
            .GetAwaiter()
            .GetResult();
        Assert(result.IsFrozen && result.PersistenceStatus == ZenoRoutePersistenceStatus.Failed,
            "A failed commit save must remain frozen after preparation was durable.");
        Assert(result.State.Route?.Stage == ZenoRouteStage.WaitingInterval,
            "A durable preparation must never reopen the source choice.");
    }

    private static void ExternalClosePersistsAroundTheExternalEffect()
    {
        ZenoRouteFeatureState outcome = CreateOutcomeCommitted();
        ZenoRoutePersistenceCoordinator coordinator = new(outcome, Catalog);
        ScriptedAdapter adapter = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);

        ZenoRoutePersistenceCoordinationResult prepared = coordinator.PrepareExternalAsync(
                ZenoRouteStateService.PrepareClose(
                    outcome,
                    outcome.Route!.Revision,
                    Catalog),
                adapter)
            .GetAwaiter()
            .GetResult();
        Assert(prepared.IsFrozen && coordinator.HasConfirmedExternalPreparation &&
               prepared.State.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed &&
               adapter.Requests.Count == 1 &&
               adapter.Requests[0].Checkpoint == ZenoRoutePersistenceCheckpoint.Prepared,
            "Closing must durably retain its write-ahead record before the room or destination changes.");

        ZenoRoutePersistenceCoordinationResult committed = coordinator.CommitExternalAsync(adapter)
            .GetAwaiter()
            .GetResult();
        Assert(committed.Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
               committed.State.Route?.Stage == ZenoRouteStage.ZenoClosed &&
               committed.State.PendingOperation is null &&
               adapter.Requests.Count == 2 &&
               adapter.Requests[1].Checkpoint == ZenoRoutePersistenceCheckpoint.Committed,
            "Closing may commit only after the caller reports the frozen destination was resumed.");
    }

    private static void RestoredClosePreparationWaitsForTheExternalEffect()
    {
        ZenoRouteFeatureState outcome = CreateOutcomeCommitted();
        ZenoRouteFeatureState closePrepared = ZenoRouteStateService.PrepareClose(
            outcome,
            outcome.Route!.Revision,
            Catalog).State;
        ZenoRoutePersistenceCoordinator restored =
            ZenoRoutePersistenceCoordinator.Restore(closePrepared, Catalog);
        ScriptedAdapter adapter = new(ZenoRoutePersistenceStatus.Confirmed);

        ZenoRoutePersistenceCoordinationResult retry = restored.RetryAsync(adapter)
            .GetAwaiter()
            .GetResult();
        Assert(retry.IsFrozen && restored.HasConfirmedExternalPreparation &&
               adapter.Requests.Count == 0 &&
               restored.CurrentState.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed,
            "A close preparation loaded from disk is already durable and must not auto-commit before scene recovery.");

        ZenoRoutePersistenceCoordinationResult committed = restored.CommitExternalAsync(adapter)
            .GetAwaiter()
            .GetResult();
        Assert(committed.Status == ZenoRoutePersistenceCoordinationStatus.Completed &&
               adapter.Requests.Single().Checkpoint == ZenoRoutePersistenceCheckpoint.Committed,
            "Recovered closing should persist only the final checkpoint after the external effect succeeds.");
    }

    private static void CompetingCallbackCannotPersistASecondWinner()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        ZenoRoutePersistenceCoordinator coordinator = new(initial, Catalog);
        BlockingAdapter winner = new();

        Task<ZenoRoutePersistenceCoordinationResult> first = coordinator.ExecuteAsync(
            PrepareSwitch(initial),
            winner);
        winner.FirstRequestStarted.Task.GetAwaiter().GetResult();

        ScriptedAdapter loser = new(
            ZenoRoutePersistenceStatus.Confirmed,
            ZenoRoutePersistenceStatus.Confirmed);
        Task<ZenoRoutePersistenceCoordinationResult> second = coordinator.ExecuteAsync(
            ZenoRouteStateService.PrepareStay(initial, 0, Catalog),
            loser);
        winner.ReleaseFirstRequest.SetResult();

        ZenoRoutePersistenceCoordinationResult firstResult = first.GetAwaiter().GetResult();
        ZenoRoutePersistenceCoordinationResult secondResult = second.GetAwaiter().GetResult();
        Assert(firstResult.Status == ZenoRoutePersistenceCoordinationStatus.Completed,
            "The lock owner should complete normally.");
        Assert(secondResult.Status == ZenoRoutePersistenceCoordinationStatus.Rejected,
            "The stale competing callback should lose after the winner commits.");
        Assert(loser.Requests.Count == 0, "The losing callback must not call the adapter.");
    }

    private static void AdapterExceptionBecomesUnknown()
    {
        ZenoRouteFeatureState initial = CreateUnresolved();
        ZenoRoutePersistenceCoordinator coordinator = new(initial, Catalog);
        ZenoRoutePersistenceCoordinationResult result = coordinator.ExecuteAsync(
                PrepareSwitch(initial),
                new ThrowingAdapter())
            .GetAwaiter()
            .GetResult();

        Assert(result.IsFrozen && result.PersistenceStatus == ZenoRoutePersistenceStatus.Unknown,
            "An adapter exception cannot prove either persistence or non-persistence.");
    }

    private static ZenoRouteTransitionResult PrepareSwitch(ZenoRouteFeatureState state) =>
        ZenoRouteStateService.PrepareSwitch(state, 0, CreateMaterial(), Catalog);

    private static ZenoRouteFeatureState CreateUnresolved()
    {
        ZenoRouteState route = new(
            ZenoRouteState.CurrentVersion,
            ZenoRouteStage.Unresolved,
            0,
            0,
            "RUN_20260924_COORDINATOR_001",
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
            null);
        return new ZenoRouteFeatureState(ZenoRouteFeatureGeneration.Current, route);
    }

    private static ZenoRouteFeatureState CreateOutcomeCommitted()
    {
        ZenoRouteState route = new(
            ZenoRouteState.CurrentVersion,
            ZenoRouteStage.OutcomeCommitted,
            5,
            5,
            "RUN_20260926_CLOSE_001",
            2,
            ZenoRouteIds.RouteEdge,
            ZenoRouteIds.TerminalNode,
            CreateMaterial(),
            "COMPLETED_ACT_2_ROOM_42",
            ZenoOpeningTrigger.BeforeBoss,
            ZenoResumeDestination.Boss,
            "BOSS_ACT_2_ROW_14_COL_3",
            "ZENO_EVENT_INSTANCE_001",
            ZenoAssentOutcome.Keep,
            CurrentStatement);
        ZenoRouteFeatureState state = new(ZenoRouteFeatureGeneration.Current, route);
        Assert(ZenoRouteStateCodec.IsValidFeature(state, Catalog),
            "The close transaction fixture must be valid.");
        return state;
    }

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

    private sealed class BlockingAdapter : IZenoRoutePersistenceConfirmationAdapter
    {
        private int _calls;

        public TaskCompletionSource FirstRequestStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstRequest { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ZenoRoutePersistenceResult> PersistAsync(
            ZenoRoutePersistenceRequest request,
            ZenoRouteFeatureState state,
            CancellationToken cancellationToken = default)
        {
            _calls++;
            if (_calls == 1)
            {
                FirstRequestStarted.SetResult();
                await ReleaseFirstRequest.Task;
            }

            return new ZenoRoutePersistenceResult(
                request.RequestId,
                ZenoRoutePersistenceStatus.Confirmed);
        }
    }

    private sealed class ThrowingAdapter : IZenoRoutePersistenceConfirmationAdapter
    {
        public Task<ZenoRoutePersistenceResult> PersistAsync(
            ZenoRoutePersistenceRequest request,
            ZenoRouteFeatureState state,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated adapter failure.");
        }
    }
}
