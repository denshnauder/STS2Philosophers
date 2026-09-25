using STS2Philosophers;

internal static class ZenoRouteRecoverySceneClassifierChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string CurrentStatement = "STATEMENT_CURRENT";
    private const string NarrowStatement = "STATEMENT_NARROW";
    private const string EventId = "ZENO_EVENT_001";
    private const string BossId = "ACT_THREE_BOSS_001";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [CurrentStatement, NarrowStatement],
        []);

    public static void Run()
    {
        OriginalDiogenesAndWaitingRoomsAreDistinguished();
        MatchingZenoAndMissingZenoKeepTheFrozenDestinationPending();
        RestoredAndConflictingDestinationsAreDistinguished();
        PendingCandidatesSupplyTheirFrozenIdentity();
        UnrelatedAndDuplicateModalsAreHandledConservatively();
        TerminationAndUnsupportedStacksCannotInventARecovery();
        Console.WriteLine("Zeno room scene classifier checks passed: current/base rooms, event identity and frozen destinations.");
    }

    private static void OriginalDiogenesAndWaitingRoomsAreDistinguished()
    {
        ZenoRouteFeatureState unresolved = CreateUnresolved();
        AssertScene(
            unresolved,
            Snapshot(Event(ZenoRouteObservedEventKind.Diogenes), Empty(), false),
            ZenoRouteEventPresence.Diogenes,
            ZenoRouteDestinationPresence.NotApplicable);

        ZenoRouteFeatureState waiting = Commit(ZenoRouteStateService.PrepareSwitch(
            unresolved,
            0,
            CreateMaterial(),
            Catalog).State);
        AssertScene(
            waiting,
            Snapshot(UnrelatedEvent(), Empty(), false),
            ZenoRouteEventPresence.None,
            ZenoRouteDestinationPresence.NotApplicable);
        AssertScene(
            waiting,
            Snapshot(Empty(), Empty(), true),
            ZenoRouteEventPresence.None,
            ZenoRouteDestinationPresence.NotApplicable);
    }

    private static void MatchingZenoAndMissingZenoKeepTheFrozenDestinationPending()
    {
        ZenoRouteFeatureState active = CreateEventActive();
        AssertScene(
            active,
            Snapshot(Event(ZenoRouteObservedEventKind.Zeno, EventId), Empty(), false),
            ZenoRouteEventPresence.MatchingZeno,
            ZenoRouteDestinationPresence.Pending);
        AssertScene(
            active,
            Snapshot(Empty(), Empty(), true),
            ZenoRouteEventPresence.None,
            ZenoRouteDestinationPresence.Pending);

        AssertScene(
            active,
            Snapshot(Event(ZenoRouteObservedEventKind.Zeno, "OTHER_EVENT"), Empty(), false),
            ZenoRouteEventPresence.Conflicting,
            ZenoRouteDestinationPresence.Conflicting);
    }

    private static void RestoredAndConflictingDestinationsAreDistinguished()
    {
        ZenoRouteFeatureState outcome = CreateOutcomeCommitted();
        ZenoRouteFeatureState closePending = ZenoRouteStateService.PrepareClose(
            outcome,
            4,
            Catalog).State;
        AssertScene(
            closePending,
            Snapshot(Destination(ZenoResumeDestination.Boss, BossId), Empty(), false),
            ZenoRouteEventPresence.None,
            ZenoRouteDestinationPresence.Matching);
        AssertScene(
            closePending,
            Snapshot(Destination(ZenoResumeDestination.Boss, "OTHER_BOSS"), Empty(), false),
            ZenoRouteEventPresence.None,
            ZenoRouteDestinationPresence.Conflicting);

        ZenoRouteFeatureState closed = Commit(closePending);
        AssertScene(
            closed,
            Snapshot(UnrelatedEvent(), Destination(ZenoResumeDestination.Boss, "LATER_ROOM"), false),
            ZenoRouteEventPresence.None,
            ZenoRouteDestinationPresence.Matching);
    }

    private static void PendingCandidatesSupplyTheirFrozenIdentity()
    {
        ZenoRouteFeatureState waiting = Commit(ZenoRouteStateService.PrepareSwitch(
            CreateUnresolved(),
            0,
            CreateMaterial(),
            Catalog).State);
        ZenoRouteFeatureState openingPending = ZenoRouteStateService.PrepareOpeningClaim(
            waiting,
            1,
            ZenoOpeningTrigger.BeforeBoss,
            ZenoResumeDestination.Boss,
            BossId,
            EventId,
            Catalog).State;

        AssertScene(
            openingPending,
            Snapshot(Empty(), Empty(), true),
            ZenoRouteEventPresence.None,
            ZenoRouteDestinationPresence.Pending);
        AssertScene(
            openingPending,
            Snapshot(Event(ZenoRouteObservedEventKind.Zeno, EventId), Empty(), false),
            ZenoRouteEventPresence.MatchingZeno,
            ZenoRouteDestinationPresence.Pending);
    }

    private static void UnrelatedAndDuplicateModalsAreHandledConservatively()
    {
        ZenoRouteFeatureState active = CreateEventActive();
        AssertScene(
            active,
            Snapshot(UnrelatedEvent(), Empty(), false),
            ZenoRouteEventPresence.Conflicting,
            ZenoRouteDestinationPresence.Conflicting);
        AssertScene(
            active,
            Snapshot(
                Event(ZenoRouteObservedEventKind.Zeno, EventId),
                Event(ZenoRouteObservedEventKind.Zeno, EventId),
                false),
            ZenoRouteEventPresence.Conflicting,
            ZenoRouteDestinationPresence.Conflicting);
        AssertScene(
            active,
            Snapshot(
                Event(ZenoRouteObservedEventKind.Zeno, EventId),
                Event(ZenoRouteObservedEventKind.Diogenes),
                false),
            ZenoRouteEventPresence.Conflicting,
            ZenoRouteDestinationPresence.Conflicting);
    }

    private static void TerminationAndUnsupportedStacksCannotInventARecovery()
    {
        ZenoRouteFeatureState waiting = Commit(ZenoRouteStateService.PrepareSwitch(
            CreateUnresolved(),
            0,
            CreateMaterial(),
            Catalog).State);
        ZenoRouteFeatureState terminated = waiting with
        {
            Route = waiting.Route! with { Stage = ZenoRouteStage.RunTerminated },
        };
        AssertScene(
            terminated,
            Snapshot(Empty(), Empty(), true, isTerminated: true),
            ZenoRouteEventPresence.None,
            ZenoRouteDestinationPresence.NotApplicable);
        AssertScene(
            waiting,
            Snapshot(Empty(), Empty(), true, isTerminated: true),
            ZenoRouteEventPresence.Conflicting,
            ZenoRouteDestinationPresence.Conflicting);
        AssertScene(
            waiting,
            new ZenoRouteRoomStackSnapshot(false, 3, Empty(), Empty(), false),
            ZenoRouteEventPresence.Conflicting,
            ZenoRouteDestinationPresence.Conflicting);
    }

    private static ZenoRouteFeatureState CreateEventActive()
    {
        ZenoRouteFeatureState waiting = Commit(ZenoRouteStateService.PrepareSwitch(
            CreateUnresolved(),
            0,
            CreateMaterial(),
            Catalog).State);
        ZenoRouteFeatureState opening = Commit(ZenoRouteStateService.PrepareOpeningClaim(
            waiting,
            1,
            ZenoOpeningTrigger.BeforeBoss,
            ZenoResumeDestination.Boss,
            BossId,
            EventId,
            Catalog).State);
        return Commit(ZenoRouteStateService.PrepareEventEstablished(opening, 2, Catalog).State);
    }

    private static ZenoRouteFeatureState CreateOutcomeCommitted()
    {
        ZenoRouteFeatureState active = CreateEventActive();
        return Commit(ZenoRouteStateService.PrepareOutcome(
            active,
            3,
            ZenoAssentOutcome.Narrow,
            NarrowStatement,
            Catalog).State);
    }

    private static void AssertScene(
        ZenoRouteFeatureState state,
        ZenoRouteRoomStackSnapshot snapshot,
        ZenoRouteEventPresence eventPresence,
        ZenoRouteDestinationPresence destinationPresence)
    {
        ZenoRouteRecoveryScene scene = ZenoRouteRecoverySceneClassifier.Classify(state, snapshot);
        Assert(scene.EventPresence == eventPresence,
            $"Expected event {eventPresence}, got {scene.EventPresence}.");
        Assert(scene.DestinationPresence == destinationPresence,
            $"Expected destination {destinationPresence}, got {scene.DestinationPresence}.");
    }

    private static ZenoRouteRoomStackSnapshot Snapshot(
        ZenoRouteObservedRoom current,
        ZenoRouteObservedRoom baseRoom,
        bool sameRoom,
        bool isTerminated = false) =>
        new(isTerminated, sameRoom ? 1 : 2, current, baseRoom, sameRoom);

    private static ZenoRouteObservedRoom Empty() =>
        new(ZenoRouteObservedEventKind.None);

    private static ZenoRouteObservedRoom UnrelatedEvent() =>
        new(ZenoRouteObservedEventKind.Unrelated);

    private static ZenoRouteObservedRoom Event(
        ZenoRouteObservedEventKind kind,
        string? instanceId = null) =>
        new(kind, instanceId);

    private static ZenoRouteObservedRoom Destination(
        ZenoResumeDestination kind,
        string id) =>
        new(
            ZenoRouteObservedEventKind.None,
            Destination: new ZenoRouteObservedDestination(kind, id));

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
                "RUN_SCENE_CLASSIFIER_001",
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
}
