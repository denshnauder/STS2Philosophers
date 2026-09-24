using STS2Philosophers;

internal static class ZenoRouteRecoveryPlannerChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string PriorStatement = "STATEMENT_PRIOR";
    private const string CurrentStatement = "STATEMENT_CURRENT";
    private const string NarrowStatement = "STATEMENT_NARROW";
    private const string LaterOutcome = "OUTCOME_PUBLIC";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [PriorStatement, CurrentStatement, NarrowStatement],
        [LaterOutcome]);

    public static void Run()
    {
        EveryStableStageHasOneRecoveryAction();
        EveryPendingOperationHasOneForwardAction();
        ContradictoryScenesAreIsolated();
        PlansCarryOnlyTheFrozenRecoveryIdentity();
        Console.WriteLine("Zeno recovery planner checks passed: all stable stages, seven pending operations and contradictory scenes.");
    }

    private static void EveryStableStageHasOneRecoveryAction()
    {
        ZenoRouteFeatureState unresolved = CreateUnresolved();
        AssertAction(unresolved, DiogenesScene(), ZenoRouteRecoveryAction.RestoreDiogenesChoice);

        ZenoRouteFeatureState diogenesClosed = Commit(
            ZenoRouteStateService.PrepareStay(unresolved, 0, Catalog).State);
        AssertAction(diogenesClosed, DiogenesScene(), ZenoRouteRecoveryAction.RemainTerminal);
        AssertAction(diogenesClosed, EmptyScene(), ZenoRouteRecoveryAction.RemainTerminal);

        ZenoRouteFeatureState waiting = Commit(
            ZenoRouteStateService.PrepareSwitch(unresolved, 0, CreateMaterial(), Catalog).State);
        AssertAction(waiting, DiogenesScene(), ZenoRouteRecoveryAction.WaitForNextRoom);
        AssertAction(waiting, EmptyScene(), ZenoRouteRecoveryAction.WaitForNextRoom);

        ZenoRouteFeatureState ready = Commit(
            ZenoRouteStateService.PrepareIntervalCompletion(waiting, 1, "ROOM_RECEIPT_001", Catalog).State);
        AssertAction(ready, EmptyScene(), ZenoRouteRecoveryAction.WaitForSafeClaim);

        ZenoRouteFeatureState opening = Commit(
            ZenoRouteStateService.PrepareOpeningClaim(
                ready,
                2,
                ZenoOpeningTrigger.SafeBoundary,
                ZenoResumeDestination.Map,
                "MAP_AFTER_ROOM_001",
                "ZENO_EVENT_001",
                Catalog).State);
        AssertAction(opening, PausedScene(), ZenoRouteRecoveryAction.ResumeSameEvent);
        AssertAction(opening, ZenoScene(), ZenoRouteRecoveryAction.ResumeSameEvent);

        ZenoRouteFeatureState active = Commit(
            ZenoRouteStateService.PrepareEventEstablished(opening, 3, Catalog).State);
        AssertAction(active, ZenoScene(), ZenoRouteRecoveryAction.RestoreMaterialReview);
        AssertAction(active, PausedScene(), ZenoRouteRecoveryAction.RestoreMaterialReview);

        ZenoRouteFeatureState outcome = Commit(
            ZenoRouteStateService.PrepareOutcome(
                active,
                4,
                ZenoAssentOutcome.Narrow,
                NarrowStatement,
                Catalog).State);
        AssertAction(outcome, ZenoScene(), ZenoRouteRecoveryAction.RestoreCommittedOutcome);
        AssertAction(outcome, PausedScene(), ZenoRouteRecoveryAction.RestoreCommittedOutcome);

        ZenoRouteFeatureState closed = Commit(
            ZenoRouteStateService.PrepareClose(outcome, 5, Catalog).State);
        AssertAction(
            closed,
            new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.None,
                ZenoRouteDestinationPresence.Matching),
            ZenoRouteRecoveryAction.RemainTerminal);

        ZenoRouteFeatureState terminated = waiting with
        {
            Route = waiting.Route! with { Stage = ZenoRouteStage.RunTerminated },
        };
        AssertAction(terminated, EmptyScene(), ZenoRouteRecoveryAction.RemainTerminal);
    }

    private static void EveryPendingOperationHasOneForwardAction()
    {
        ZenoRouteFeatureState unresolved = CreateUnresolved();
        AssertPending(
            ZenoRouteStateService.PrepareStay(unresolved, 0, Catalog).State,
            DiogenesScene(),
            ZenoRouteRecoveryAction.RetryPendingTransaction);

        ZenoRouteFeatureState switchPending = ZenoRouteStateService.PrepareSwitch(
            unresolved,
            0,
            CreateMaterial(),
            Catalog).State;
        AssertPending(switchPending, DiogenesScene(), ZenoRouteRecoveryAction.RetryPendingTransaction);
        ZenoRouteFeatureState waiting = Commit(switchPending);

        ZenoRouteFeatureState intervalPending = ZenoRouteStateService.PrepareIntervalCompletion(
            waiting,
            1,
            "ROOM_RECEIPT_001",
            Catalog).State;
        AssertPending(intervalPending, EmptyScene(), ZenoRouteRecoveryAction.RetryPendingTransaction);
        ZenoRouteFeatureState ready = Commit(intervalPending);

        ZenoRouteFeatureState openingPending = ZenoRouteStateService.PrepareOpeningClaim(
            ready,
            2,
            ZenoOpeningTrigger.SafeBoundary,
            ZenoResumeDestination.Map,
            "MAP_AFTER_ROOM_001",
            "ZENO_EVENT_001",
            Catalog).State;
        AssertPending(openingPending, PausedScene(), ZenoRouteRecoveryAction.RetryPendingTransaction);
        ZenoRouteFeatureState opening = Commit(openingPending);

        ZenoRouteFeatureState establishedPending = ZenoRouteStateService.PrepareEventEstablished(
            opening,
            3,
            Catalog).State;
        AssertPending(establishedPending, ZenoScene(), ZenoRouteRecoveryAction.RetryPendingTransaction);
        ZenoRouteFeatureState active = Commit(establishedPending);

        ZenoRouteFeatureState outcomePending = ZenoRouteStateService.PrepareOutcome(
            active,
            4,
            ZenoAssentOutcome.Narrow,
            NarrowStatement,
            Catalog).State;
        AssertPending(outcomePending, ZenoScene(), ZenoRouteRecoveryAction.RetryPendingTransaction);
        ZenoRouteFeatureState outcome = Commit(outcomePending);

        ZenoRouteFeatureState closePending = ZenoRouteStateService.PrepareClose(outcome, 5, Catalog).State;
        AssertPending(closePending, ZenoScene(), ZenoRouteRecoveryAction.CompleteClosing);
        AssertPending(closePending, PausedScene(), ZenoRouteRecoveryAction.CompleteClosing);
        AssertPending(
            closePending,
            new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.None,
                ZenoRouteDestinationPresence.Matching),
            ZenoRouteRecoveryAction.CompleteClosing);
    }

    private static void ContradictoryScenesAreIsolated()
    {
        ZenoRouteFeatureState unresolved = CreateUnresolved();
        AssertAction(unresolved, EmptyScene(), ZenoRouteRecoveryAction.Isolate);

        ZenoRouteFeatureState waiting = Commit(
            ZenoRouteStateService.PrepareSwitch(unresolved, 0, CreateMaterial(), Catalog).State);
        AssertAction(waiting, ZenoScene(), ZenoRouteRecoveryAction.Isolate);

        ZenoRouteFeatureState opening = Commit(
            ZenoRouteStateService.PrepareOpeningClaim(
                waiting,
                1,
                ZenoOpeningTrigger.BeforeBoss,
                ZenoResumeDestination.Boss,
                "ACT_THREE_BOSS_001",
                "ZENO_EVENT_BOSS_001",
                Catalog).State);
        AssertAction(
            opening,
            new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.MatchingZeno,
                ZenoRouteDestinationPresence.Matching),
            ZenoRouteRecoveryAction.Isolate);

        ZenoRouteFeatureState active = Commit(
            ZenoRouteStateService.PrepareEventEstablished(opening, 2, Catalog).State);
        ZenoRouteFeatureState outcome = Commit(
            ZenoRouteStateService.PrepareOutcome(
                active,
                3,
                ZenoAssentOutcome.Narrow,
                NarrowStatement,
                Catalog).State);
        ZenoRouteFeatureState closePending = ZenoRouteStateService.PrepareClose(outcome, 4, Catalog).State;
        AssertAction(
            closePending,
            new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.MatchingZeno,
                ZenoRouteDestinationPresence.Matching),
            ZenoRouteRecoveryAction.Isolate);
        AssertAction(
            closePending,
            new ZenoRouteRecoveryScene(
                ZenoRouteEventPresence.Conflicting,
                ZenoRouteDestinationPresence.Pending),
            ZenoRouteRecoveryAction.Isolate);
    }

    private static void PlansCarryOnlyTheFrozenRecoveryIdentity()
    {
        ZenoRouteFeatureState waiting = Commit(
            ZenoRouteStateService.PrepareSwitch(CreateUnresolved(), 0, CreateMaterial(), Catalog).State);
        ZenoRouteFeatureState opening = Commit(
            ZenoRouteStateService.PrepareOpeningClaim(
                waiting,
                1,
                ZenoOpeningTrigger.BeforeBoss,
                ZenoResumeDestination.Boss,
                "ACT_THREE_BOSS_001",
                "ZENO_EVENT_BOSS_001",
                Catalog).State);
        ZenoRouteFeatureState active = Commit(
            ZenoRouteStateService.PrepareEventEstablished(opening, 2, Catalog).State);
        ZenoRouteFeatureState outcome = Commit(
            ZenoRouteStateService.PrepareOutcome(
                active,
                3,
                ZenoAssentOutcome.Narrow,
                NarrowStatement,
                Catalog).State);

        ZenoRouteRecoveryPlan plan = ZenoRouteRecoveryPlanner.CreatePlan(outcome, PausedScene(), Catalog);
        Assert(plan.Action == ZenoRouteRecoveryAction.RestoreCommittedOutcome, "Committed outcomes must restore their result page.");
        Assert(plan.EventInstanceId == "ZENO_EVENT_BOSS_001", "The plan must retain the frozen event instance.");
        Assert(plan.Outcome == ZenoAssentOutcome.Narrow, "The plan must retain the committed outcome.");
        Assert(plan.ResumeDestination == ZenoResumeDestination.Boss &&
               plan.ResumeDestinationId == "ACT_THREE_BOSS_001",
            "The plan must retain the frozen destination rather than query a new one.");
        Assert(plan.OperationId is null, "A stable recovery plan must not invent a transaction identity.");
    }

    private static void AssertPending(
        ZenoRouteFeatureState state,
        ZenoRouteRecoveryScene scene,
        ZenoRouteRecoveryAction action)
    {
        ZenoRouteRecoveryPlan plan = ZenoRouteRecoveryPlanner.CreatePlan(state, scene, Catalog);
        Assert(plan.Action == action, $"Expected pending action {action}, got {plan.Action}.");
        Assert(plan.OperationId == state.PendingOperation?.OperationId,
            "A pending recovery plan must preserve the prepared operation identity.");
    }

    private static void AssertAction(
        ZenoRouteFeatureState state,
        ZenoRouteRecoveryScene scene,
        ZenoRouteRecoveryAction action)
    {
        ZenoRouteRecoveryPlan plan = ZenoRouteRecoveryPlanner.CreatePlan(state, scene, Catalog);
        Assert(plan.Action == action, $"Expected action {action}, got {plan.Action}.");
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

    private static ZenoRouteFeatureState CreateUnresolved() =>
        new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_20260924_RECOVERY_001",
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
            [PriorStatement, CurrentStatement],
            CurrentStatement,
            NarrowStatement,
            LaterOutcome);

    private static ZenoRouteRecoveryScene EmptyScene() =>
        new(ZenoRouteEventPresence.None, ZenoRouteDestinationPresence.NotApplicable);

    private static ZenoRouteRecoveryScene DiogenesScene() =>
        new(ZenoRouteEventPresence.Diogenes, ZenoRouteDestinationPresence.NotApplicable);

    private static ZenoRouteRecoveryScene PausedScene() =>
        new(ZenoRouteEventPresence.None, ZenoRouteDestinationPresence.Pending);

    private static ZenoRouteRecoveryScene ZenoScene() =>
        new(ZenoRouteEventPresence.MatchingZeno, ZenoRouteDestinationPresence.Pending);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
