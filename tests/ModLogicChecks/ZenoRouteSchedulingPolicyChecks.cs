using STS2Philosophers;

internal static class ZenoRouteSchedulingPolicyChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string CurrentStatement = "STATEMENT_CURRENT";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [CurrentStatement],
        []);

    public static void Run()
    {
        PriorityIsTerminationThenActExitThenBossThenSafeBoundary();
        OrdinaryBoundaryRequiresOneCompletedRoomAndKeepsItsReceipt();
        ExistingClaimIsStableAndCannotBeTakenAgain();
        InvalidOrFrozenInputsDoNotScheduleEffects();
        RuntimePersistsSafeClaimAndTermination();
        Console.WriteLine("Zeno route scheduling checks passed: deterministic priority, one claim and persisted termination.");
    }

    private static void PriorityIsTerminationThenActExitThenBossThenSafeBoundary()
    {
        ZenoRouteFeatureState waiting = CreateWaiting("RUN_SCHEDULING_PRIORITY");
        ZenoRouteSchedulingContext allSignals = new(
            IsRunTerminated: true,
            CompletedRoomReceiptId: "ROOM_001",
            SafeBoundaryDestinationId: "MAP_001",
            BossDestinationId: "BOSS_001",
            ActExitDestinationId: "ACT_EXIT_001");
        AssertDecision(
            ZenoRouteSchedulingPolicy.CreatePlan(waiting, allSignals, Catalog),
            ZenoRouteSchedulingDecision.TerminateRun);

        ZenoRouteSchedulingPlan actExit = ZenoRouteSchedulingPolicy.CreatePlan(
            waiting,
            allSignals with { IsRunTerminated = false },
            Catalog);
        AssertClaim(actExit, ZenoOpeningTrigger.BeforeActExit, ZenoResumeDestination.ActExit, "ACT_EXIT_001");

        ZenoRouteSchedulingPlan boss = ZenoRouteSchedulingPolicy.CreatePlan(
            waiting,
            allSignals with { IsRunTerminated = false, ActExitDestinationId = null },
            Catalog);
        AssertClaim(boss, ZenoOpeningTrigger.BeforeBoss, ZenoResumeDestination.Boss, "BOSS_001");

        ZenoRouteSchedulingPlan ordinary = ZenoRouteSchedulingPolicy.CreatePlan(
            waiting,
            allSignals with
            {
                IsRunTerminated = false,
                ActExitDestinationId = null,
                BossDestinationId = null,
            },
            Catalog);
        AssertDecision(ordinary, ZenoRouteSchedulingDecision.CompleteIntervalAndClaimOpening);
        Assert(ordinary.OpeningTrigger == ZenoOpeningTrigger.SafeBoundary &&
               ordinary.ResumeDestination == ZenoResumeDestination.Map,
            "An ordinary signal must claim only the map safe boundary after completing its interval.");
    }

    private static void OrdinaryBoundaryRequiresOneCompletedRoomAndKeepsItsReceipt()
    {
        ZenoRouteFeatureState waiting = CreateWaiting("RUN_SCHEDULING_INTERVAL");
        ZenoRouteSchedulingPlan immediateMap = ZenoRouteSchedulingPolicy.CreatePlan(
            waiting,
            new ZenoRouteSchedulingContext(SafeBoundaryDestinationId: "MAP_IMMEDIATE"),
            Catalog);
        AssertDecision(immediateMap, ZenoRouteSchedulingDecision.Ignore);

        ZenoRouteSchedulingPlan completionOnly = ZenoRouteSchedulingPolicy.CreatePlan(
            waiting,
            new ZenoRouteSchedulingContext(CompletedRoomReceiptId: "ROOM_002"),
            Catalog);
        AssertDecision(completionOnly, ZenoRouteSchedulingDecision.CompleteInterval);

        ZenoRouteFeatureState ready = Commit(
            ZenoRouteStateService.PrepareIntervalCompletion(
                waiting,
                waiting.Route!.Revision,
                "ROOM_002",
                Catalog).State);
        ZenoRouteSchedulingPlan readyClaim = ZenoRouteSchedulingPolicy.CreatePlan(
            ready,
            new ZenoRouteSchedulingContext(
                CompletedRoomReceiptId: "ROOM_002",
                SafeBoundaryDestinationId: "MAP_002"),
            Catalog);
        AssertClaim(readyClaim, ZenoOpeningTrigger.SafeBoundary, ZenoResumeDestination.Map, "MAP_002");
        Assert(readyClaim.CompletedRoomReceiptId == "ROOM_002",
            "A ready ordinary claim must retain the persisted room receipt.");

        AssertDecision(
            ZenoRouteSchedulingPolicy.CreatePlan(
                ready,
                new ZenoRouteSchedulingContext(
                    CompletedRoomReceiptId: "ROOM_CONFLICT",
                    SafeBoundaryDestinationId: "MAP_002"),
                Catalog),
            ZenoRouteSchedulingDecision.Invalid);
    }

    private static void ExistingClaimIsStableAndCannotBeTakenAgain()
    {
        ZenoRouteFeatureState waiting = CreateWaiting("RUN_SCHEDULING_EXISTING");
        ZenoRouteSchedulingPlan first = ZenoRouteSchedulingPolicy.CreatePlan(
            waiting,
            new ZenoRouteSchedulingContext(BossDestinationId: "BOSS_003"),
            Catalog);
        ZenoRouteFeatureState claimed = Commit(
            ZenoRouteStateService.PrepareOpeningClaim(
                waiting,
                waiting.Route!.Revision,
                first.OpeningTrigger!.Value,
                first.ResumeDestination!.Value,
                first.ResumeDestinationId!,
                first.EventInstanceId!,
                Catalog).State);

        ZenoRouteSchedulingPlan repeated = ZenoRouteSchedulingPolicy.CreatePlan(
            claimed,
            new ZenoRouteSchedulingContext(ActExitDestinationId: "ACT_EXIT_LATE"),
            Catalog);
        AssertDecision(repeated, ZenoRouteSchedulingDecision.ExistingClaim);
        Assert(repeated.OpeningTrigger == ZenoOpeningTrigger.BeforeBoss &&
               repeated.ResumeDestinationId == "BOSS_003" &&
               repeated.EventInstanceId == first.EventInstanceId,
            "A later higher-priority callback must return the persisted winner instead of creating another event.");

        ZenoRouteSchedulingPlan sameRunPlan = ZenoRouteSchedulingPolicy.CreatePlan(
            CreateWaiting("RUN_SCHEDULING_EXISTING"),
            new ZenoRouteSchedulingContext(ActExitDestinationId: "ACT_EXIT_003"),
            Catalog);
        Assert(sameRunPlan.EventInstanceId == first.EventInstanceId,
            "All trigger paths in one run must derive the same event instance identity.");
    }

    private static void InvalidOrFrozenInputsDoNotScheduleEffects()
    {
        ZenoRouteFeatureState waiting = CreateWaiting("RUN_SCHEDULING_INVALID");
        AssertDecision(
            ZenoRouteSchedulingPolicy.CreatePlan(
                waiting,
                new ZenoRouteSchedulingContext(BossDestinationId: " "),
                Catalog),
            ZenoRouteSchedulingDecision.Invalid);

        ZenoRouteFeatureState pending = ZenoRouteStateService.PrepareIntervalCompletion(
            waiting,
            waiting.Route!.Revision,
            "ROOM_PENDING",
            Catalog).State;
        AssertDecision(
            ZenoRouteSchedulingPolicy.CreatePlan(
                pending,
                new ZenoRouteSchedulingContext(ActExitDestinationId: "ACT_EXIT_004"),
                Catalog),
            ZenoRouteSchedulingDecision.Frozen);
    }

    private static void RuntimePersistsSafeClaimAndTermination()
    {
        (ZenoRouteSchedulingRuntime safeScheduler, ConfirmingAdapter safeAdapter) =
            CreateRuntime(CreateWaiting("RUN_SCHEDULING_SAFE_RUNTIME"));
        ZenoRouteSchedulingExecutionResult safe = safeScheduler.ExecuteAsync(
                new ZenoRouteSchedulingContext(
                    CompletedRoomReceiptId: "ROOM_RUNTIME",
                    SafeBoundaryDestinationId: "MAP_RUNTIME"))
            .GetAwaiter().GetResult();
        Assert(safe.Status == ZenoRouteSchedulingExecutionStatus.Completed &&
               safe.State.Route?.Stage == ZenoRouteStage.OpeningClaimed &&
               safe.State.Route.CompletedRoomReceiptId == "ROOM_RUNTIME" &&
               safe.State.Route.OpeningTrigger == ZenoOpeningTrigger.SafeBoundary,
            "The scheduler runtime must persist interval completion before the ordinary opening claim.");
        Assert(safeAdapter.RequestCount == 4,
            "Two durable state transitions must each confirm their prepared and committed checkpoints.");

        ZenoRouteSchedulingExecutionResult duplicate = safeScheduler.ExecuteAsync(
                new ZenoRouteSchedulingContext(ActExitDestinationId: "ACT_EXIT_TOO_LATE"))
            .GetAwaiter().GetResult();
        Assert(duplicate.Status == ZenoRouteSchedulingExecutionStatus.ExistingClaim &&
               duplicate.State.Route?.OpeningTrigger == ZenoOpeningTrigger.SafeBoundary &&
               safeAdapter.RequestCount == 4,
            "A duplicate trigger must observe the existing claim without another persistence write.");

        (ZenoRouteSchedulingRuntime deathScheduler, ConfirmingAdapter deathAdapter) =
            CreateRuntime(CreateWaiting("RUN_SCHEDULING_DEATH_RUNTIME"));
        ZenoRouteSchedulingExecutionResult terminated = deathScheduler.ExecuteAsync(
                new ZenoRouteSchedulingContext(
                    IsRunTerminated: true,
                    BossDestinationId: "BOSS_NEVER_ENTERED"))
            .GetAwaiter().GetResult();
        Assert(terminated.Status == ZenoRouteSchedulingExecutionStatus.Completed &&
               terminated.State.Route?.Stage == ZenoRouteStage.RunTerminated &&
               terminated.State.Route.EventInstanceId is null &&
               deathAdapter.RequestCount == 2,
            "Run termination must durably win without fabricating an event claim.");
    }

    private static (ZenoRouteSchedulingRuntime Scheduler, ConfirmingAdapter Adapter) CreateRuntime(
        ZenoRouteFeatureState state)
    {
        PhilosophyRunState shared = new();
        shared.SetCurrentZenoRouteState(state, Catalog);
        ConfirmingAdapter adapter = new();
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                adapter,
                out ZenoRoutePersistenceRuntime? runtime) && runtime is not null,
            "The scheduling fixture must create a valid persistence runtime.");
        return (new ZenoRouteSchedulingRuntime(runtime!, Catalog), adapter);
    }

    private static ZenoRouteFeatureState CreateWaiting(string runId)
    {
        ZenoRouteFeatureState unresolved = new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                runId,
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
        ZenoAssentMaterialSnapshot material = ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [CurrentStatement],
            CurrentStatement,
            null,
            null);
        return Commit(ZenoRouteStateService.PrepareSwitch(unresolved, 0, material, Catalog).State);
    }

    private static ZenoRouteFeatureState Commit(ZenoRouteFeatureState prepared)
    {
        ZenoRoutePendingOperation pending = prepared.PendingOperation ??
            throw new InvalidOperationException("Expected a prepared operation.");
        ZenoRouteTransitionResult committed = ZenoRouteStateService.Commit(
            prepared,
            pending.OperationId,
            pending.CandidateDigest,
            Catalog);
        Assert(committed.Status == ZenoRouteTransitionStatus.Committed,
            "The scheduling fixture must commit its valid transition.");
        return committed.State;
    }

    private static void AssertClaim(
        ZenoRouteSchedulingPlan plan,
        ZenoOpeningTrigger trigger,
        ZenoResumeDestination destination,
        string destinationId)
    {
        AssertDecision(plan, ZenoRouteSchedulingDecision.ClaimOpening);
        Assert(plan.OpeningTrigger == trigger &&
               plan.ResumeDestination == destination &&
               plan.ResumeDestinationId == destinationId &&
               !string.IsNullOrWhiteSpace(plan.EventInstanceId),
            $"Expected {trigger} to claim {destination} {destinationId}.");
    }

    private static void AssertDecision(
        ZenoRouteSchedulingPlan plan,
        ZenoRouteSchedulingDecision decision)
    {
        Assert(plan.Decision == decision, $"Expected scheduling decision {decision}, got {plan.Decision}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ConfirmingAdapter : IZenoRoutePersistenceConfirmationAdapter
    {
        public int RequestCount { get; private set; }

        public Task<ZenoRoutePersistenceResult> PersistAsync(
            ZenoRoutePersistenceRequest request,
            ZenoRouteFeatureState state,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return Task.FromResult(new ZenoRoutePersistenceResult(
                request.RequestId,
                ZenoRoutePersistenceStatus.Confirmed));
        }
    }
}
