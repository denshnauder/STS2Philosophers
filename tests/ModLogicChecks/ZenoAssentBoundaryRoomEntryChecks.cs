namespace STS2Philosophers;

internal static class ZenoAssentBoundaryRoomEntryChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string EventInstanceId = "ZENO_EVENT_ROOM_ENTRY_001";
    private const string BossId = "ACT_THREE_BOSS_ROOM_001";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [],
        []);

    public static void Run()
    {
        ReadyEventEntersOnceAndDuplicateReusesTheRoom();
        WrongRoomsAndWrongIdentityAreRejected();
        UnstablePersistenceAndWrongStageAreRejected();
        NativeFailurePreservesTheFrozenRoute();
        Console.WriteLine("Zeno room entry checks passed: one native nested room, strict identity and frozen-destination guards.");
    }

    private static void ReadyEventEntersOnceAndDuplicateReusesTheRoom()
    {
        ZenoRoutePersistenceRuntime runtime = CreateRuntime(CreateEventActive());
        object routeEvent = new();
        FakeGateway gateway = new(BeforeEntry(), AfterEntry());
        ZenoAssentBoundaryRoomEntry<object> entry = CreateEntry(runtime, gateway, routeEvent);

        ZenoAssentBoundaryRoomEntryStatus first = Enter(entry, routeEvent);
        ZenoAssentBoundaryRoomEntryStatus duplicate = Enter(entry, routeEvent);

        Assert(first == ZenoAssentBoundaryRoomEntryStatus.Entered &&
               duplicate == ZenoAssentBoundaryRoomEntryStatus.AlreadyEntered &&
               gateway.EnterCalls == 1,
            "A ready route must push one native room and duplicate callbacks must reuse that entered identity.");
        Assert(runtime.CurrentState.Route?.Stage == ZenoRouteStage.EventActive &&
               runtime.CurrentState.PendingOperation is null &&
               runtime.PendingRequestId is null,
            "Room entry must not rewrite the already confirmed EventActive route.");
    }

    private static void WrongRoomsAndWrongIdentityAreRejected()
    {
        ZenoRoutePersistenceRuntime runtime = CreateRuntime(CreateEventActive());
        object routeEvent = new();
        ZenoRouteRoomStackSnapshot[] invalid =
        [
            Snapshot(
                new ZenoRouteObservedRoom(ZenoRouteObservedEventKind.Unrelated),
                Destination(),
                sameRoom: false),
            Snapshot(
                new ZenoRouteObservedRoom(ZenoRouteObservedEventKind.Zeno, "OTHER_EVENT"),
                Destination(),
                sameRoom: false),
            new ZenoRouteRoomStackSnapshot(
                false,
                3,
                new ZenoRouteObservedRoom(ZenoRouteObservedEventKind.None),
                Destination(),
                false),
            Snapshot(
                new ZenoRouteObservedRoom(ZenoRouteObservedEventKind.None),
                new ZenoRouteObservedRoom(
                    ZenoRouteObservedEventKind.None,
                    Destination: new ZenoRouteObservedDestination(
                        ZenoResumeDestination.Boss,
                        "OTHER_BOSS")),
                sameRoom: true),
        ];

        foreach (ZenoRouteRoomStackSnapshot snapshot in invalid)
        {
            FakeGateway gateway = new(snapshot);
            ZenoAssentBoundaryRoomEntry<object> entry = CreateEntry(runtime, gateway, routeEvent);
            Assert(Enter(entry, routeEvent) == ZenoAssentBoundaryRoomEntryStatus.Rejected &&
                   gateway.EnterCalls == 0,
                "An unrelated modal, competing Zeno identity, oversized stack or wrong destination must not enter.");
        }

        FakeGateway validGateway = new(BeforeEntry(), AfterEntry());
        ZenoAssentBoundaryRoomEntry<object> wrongReadyIdentity = new(
            runtime,
            Catalog,
            validGateway,
            _ => "OTHER_EVENT");
        Assert(Enter(wrongReadyIdentity, routeEvent) == ZenoAssentBoundaryRoomEntryStatus.Rejected &&
               validGateway.EnterCalls == 0,
            "The ready model identity must match the persisted EventActive identity.");
    }

    private static void UnstablePersistenceAndWrongStageAreRejected()
    {
        object routeEvent = new();
        ZenoRouteFeatureState outcomePending = ZenoRouteStateService.PrepareOutcome(
            CreateEventActive(),
            3,
            ZenoAssentOutcome.NoReassent,
            null,
            Catalog).State;
        ZenoRoutePersistenceRuntime pendingRuntime = CreateRuntime(outcomePending);
        FakeGateway pendingGateway = new(BeforeEntry(), AfterEntry());
        Assert(Enter(CreateEntry(pendingRuntime, pendingGateway, routeEvent), routeEvent) ==
               ZenoAssentBoundaryRoomEntryStatus.Rejected && pendingGateway.EnterCalls == 0,
            "A write-ahead operation or frozen persistence request must block room entry.");

        ZenoRoutePersistenceRuntime openingRuntime = CreateRuntime(CreateOpeningClaimed());
        FakeGateway openingGateway = new(BeforeEntry(), AfterEntry());
        Assert(Enter(CreateEntry(openingRuntime, openingGateway, routeEvent), routeEvent) ==
               ZenoAssentBoundaryRoomEntryStatus.Rejected && openingGateway.EnterCalls == 0,
            "OpeningClaimed must not bypass the confirmed event-establishment transaction.");

        ZenoAssentBoundaryEstablishmentResult<object> frozen = new(
            ZenoAssentBoundaryEstablishmentStatus.Frozen,
            null);
        Assert(CreateEntry(CreateRuntime(CreateEventActive()), new FakeGateway(BeforeEntry()), routeEvent)
                .EnterAsync(frozen).GetAwaiter().GetResult() ==
               ZenoAssentBoundaryRoomEntryStatus.Rejected,
            "A non-ready establishment result must never expose a room entry.");
    }

    private static void NativeFailurePreservesTheFrozenRoute()
    {
        ZenoRoutePersistenceRuntime runtime = CreateRuntime(CreateEventActive());
        ZenoRouteFeatureState before = runtime.CurrentState;
        object routeEvent = new();
        FakeGateway gateway = new(BeforeEntry()) { ThrowOnEnter = true };

        Assert(Enter(CreateEntry(runtime, gateway, routeEvent), routeEvent) ==
               ZenoAssentBoundaryRoomEntryStatus.Failed &&
               gateway.EnterCalls == 1 &&
               ReferenceEquals(before, runtime.CurrentState),
            "A native entry exception must report failure without changing route facts or consuming the frozen destination.");
    }

    private static ZenoAssentBoundaryRoomEntry<object> CreateEntry(
        ZenoRoutePersistenceRuntime runtime,
        FakeGateway gateway,
        object routeEvent) =>
        new(runtime, Catalog, gateway, candidate =>
            ReferenceEquals(candidate, routeEvent) ? EventInstanceId : null);

    private static ZenoAssentBoundaryRoomEntryStatus Enter(
        ZenoAssentBoundaryRoomEntry<object> entry,
        object routeEvent) =>
        entry.EnterAsync(new ZenoAssentBoundaryEstablishmentResult<object>(
                ZenoAssentBoundaryEstablishmentStatus.Ready,
                routeEvent))
            .GetAwaiter()
            .GetResult();

    private static ZenoRoutePersistenceRuntime CreateRuntime(ZenoRouteFeatureState state)
    {
        PhilosophyRunState shared = new();
        shared.SetCurrentZenoRouteState(state, Catalog);
        Assert(ZenoRoutePersistenceRuntime.TryCreate(
                shared,
                Catalog,
                new UnexpectedPersistenceAdapter(),
                out ZenoRoutePersistenceRuntime? runtime) && runtime is not null,
            "The room-entry fixture must create its per-run runtime.");
        return runtime!;
    }

    private static ZenoRouteFeatureState CreateEventActive() =>
        Commit(ZenoRouteStateService.PrepareEventEstablished(
            CreateOpeningClaimed(),
            2,
            Catalog).State);

    private static ZenoRouteFeatureState CreateOpeningClaimed()
    {
        ZenoAssentMaterialSnapshot material = ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [],
            null,
            null,
            null);
        ZenoRouteFeatureState unresolved = new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_20260925_ROOM_ENTRY_001",
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
        ZenoRouteFeatureState waiting = Commit(
            ZenoRouteStateService.PrepareSwitch(
                unresolved,
                0,
                material,
                Catalog).State);
        return Commit(ZenoRouteStateService.PrepareOpeningClaim(
            waiting,
            1,
            ZenoOpeningTrigger.BeforeBoss,
            ZenoResumeDestination.Boss,
            BossId,
            EventInstanceId,
            Catalog).State);
    }

    private static ZenoRouteFeatureState Commit(ZenoRouteFeatureState prepared)
    {
        ZenoRoutePendingOperation pending = prepared.PendingOperation ??
            throw new InvalidOperationException("Expected a prepared route operation.");
        return ZenoRouteStateService.Commit(
            prepared,
            pending.OperationId,
            pending.CandidateDigest,
            Catalog).State;
    }

    private static ZenoRouteRoomStackSnapshot BeforeEntry() =>
        Snapshot(Destination(), Destination(), sameRoom: true);

    private static ZenoRouteRoomStackSnapshot AfterEntry() =>
        Snapshot(
            new ZenoRouteObservedRoom(
                ZenoRouteObservedEventKind.Zeno,
                EventInstanceId),
            Destination(),
            sameRoom: false);

    private static ZenoRouteObservedRoom Destination() =>
        new(
            ZenoRouteObservedEventKind.None,
            Destination: new ZenoRouteObservedDestination(
                ZenoResumeDestination.Boss,
                BossId));

    private static ZenoRouteRoomStackSnapshot Snapshot(
        ZenoRouteObservedRoom current,
        ZenoRouteObservedRoom baseRoom,
        bool sameRoom) =>
        new(false, sameRoom ? 1 : 2, current, baseRoom, sameRoom);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakeGateway(params ZenoRouteRoomStackSnapshot[] snapshots)
        : IZenoAssentBoundaryRoomEntryGateway<object>
    {
        private readonly Queue<ZenoRouteRoomStackSnapshot> _snapshots = new(snapshots);
        private ZenoRouteRoomStackSnapshot? _lastSnapshot;

        public int EnterCalls { get; private set; }

        public bool ThrowOnEnter { get; init; }

        public ZenoRouteRoomStackSnapshot ReadSnapshot()
        {
            if (_snapshots.Count > 0)
            {
                _lastSnapshot = _snapshots.Dequeue();
            }

            return _lastSnapshot ??
                throw new InvalidOperationException("No room snapshot is available.");
        }

        public Task EnterAsync(
            object routeEvent,
            CancellationToken cancellationToken = default)
        {
            EnterCalls++;
            if (ThrowOnEnter)
            {
                throw new InvalidOperationException("Scripted native room-entry failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class UnexpectedPersistenceAdapter : IZenoRoutePersistenceConfirmationAdapter
    {
        public Task<ZenoRoutePersistenceResult> PersistAsync(
            ZenoRoutePersistenceRequest request,
            ZenoRouteFeatureState state,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Room entry must not start another persistence transaction.");
    }
}
