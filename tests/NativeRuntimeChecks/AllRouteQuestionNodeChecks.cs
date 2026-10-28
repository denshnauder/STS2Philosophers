using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class AllRouteQuestionNodeChecks
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Assembly Mod = typeof(PlatoCivicSeal).Assembly;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) => T(type).GetMethods(Methods)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length).Invoke(null, args);
    private static object Get(object value, string name) => value.GetType().GetProperty(name, Methods)!.GetValue(value)!;
    private static void Put(object value, string name, object? item) => value.GetType().GetProperty(name, Methods)!.SetValue(value, item);
    private static string Encode(object shared) => (string)Call("PhilosophyRunStateCodec", "Encode", shared)!;
    private static object Decode(string encoded) => Call("PhilosophyRunStateCodec", "Decode", encoded)!;
    private static object Create(string name, string id, params object[] rest) => Call(name + "State", "Create", [id, .. rest])!;

    private static readonly (string Person, int Act, Type Carrier, string Node)[] Cases =
    [
        ("PlatoWholeArrangement", 1, typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE"),
        ("DescartesMethodicReconstruction", 1, typeof(SocratesQuestionCup), "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"),
        ("AugustinePromise", 1, typeof(PlatoCivicSeal), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE"),
        ("AristotleCivicInquiry", 1, typeof(PlatoCivicSeal), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE"),
        ("DemocritusArrangement", 1, typeof(HeraclitusLyre), "HERACLITUS__BEING_AND_CHANGE__CORE"),
        ("MachiavelliFloodPreparation", 1, typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE"),
        ("EpicurusSharedMargin", 2, typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE"),
        ("PlotinusCommonGround", 2, typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE"),
        ("HanFeiPublicStandard", 2, typeof(XunziShengMo), ""),
        ("AnaximenesBreathInvitation", 2, typeof(HeraclitusLyre), "HERACLITUS__BEING_AND_CHANGE__CORE"),
        ("AquinasBridgeNotice", 2, typeof(PlatoCivicSeal), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE"),
        ("SenecaDailyReview", 2, typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE"),
        ("EpictetusGuidePractice", 2, typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE"),
        ("MarcusAureliusCampPractice", 2, typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE"),
        ("BaconSampleLedger", 2, typeof(AristotleCategoryTablet), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE"),
        ("GalileoMeasuredPractice", 2, typeof(HeraclitusLyre), "HERACLITUS__BEING_AND_CHANGE__CORE"),
        ("MillHarm", 1, typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE"),
        ("BeauvoirOther", 2, typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE"),
        ("FichteSelfPositing", 2, typeof(SocratesQuestionCup), "ROUSSEAU__HISTORY_AND_POWER__CORE"),
    ];

    public static async Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        object? priorTurn = typeof(CombatManager).GetField("_turnState", Methods)!.GetValue(CombatManager.Instance);
        object priorSynchronizer = manager.EventSynchronizer;
        object priorExecutor = manager.ActionExecutor;
        object? priorNetwork = manager.NetService;
        try
        {
            TestMode.IsOn = true;
            foreach (var item in Cases) ModelDb.Inject(T(item.Person));
            foreach (var item in Cases)
            {
                // Reuse the existing real native event queue/map shell, without
                // copying or weakening its current-run and public relic hooks.
                var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                    .Invoke(null, [item.Carrier == typeof(XunziShengMo) ? typeof(HeraclitusLyre) : item.Carrier, item.Act])!;
                var fixture = (NativeRuntimeFixture)tuple[0]!;
                var run = (RunState)tuple[1]!;
                RelicModel carrier = fixture.Relic;
                if (item.Carrier == typeof(XunziShengMo))
                {
                    ModelDb.Inject(item.Carrier);
                    carrier = ModelDb.GetById<RelicModel>(ModelDb.GetId(item.Carrier)).ToMutable();
                    carrier.Owner = fixture.Player;
                    ((List<RelicModel>)fixture.Player.Relics).Clear();
                    ((List<RelicModel>)fixture.Player.Relics).Add(carrier);
                }
                object pool = tuple[3]!;
                EventModel original = (EventModel)tuple[4]!;
                MapPoint point = run.CurrentMapPoint!;
                object shared = BuildShared(item.Person, item.Node, carrier.Id.Entry, run);
                Call("PhilosophyRunStateService", "Restore", run, shared);
                shared = CurrentShared(run);
                string before = Encode(shared);
                object? startingJourney = shared.GetType().GetProperty("WesternJourney")!.GetValue(shared);
                string? questionBudget = startingJourney is null ? null
                    : JsonSerializer.Serialize(((IEnumerable<int>)Get(startingJourney, "ResolvedQuestionActs")).Order());
                int fixedAct = startingJourney is null ? 0 : (int)Get(startingJourney, "LastFixedAct");

                void Rejected(string reason) => Check(Call(item.Person + "Patch", "TrySelectNative", run) is null
                    && Encode(shared) == before, item.Person + ": " + reason);
                point.PointType = MapPointType.Monster;
                Rejected("an actual combat node cannot create an event or person state");
                point.PointType = MapPointType.Unknown;
                Set(run, "_currentRooms", new List<AbstractRoom> { new MapRoom() });
                Rejected("an already active room is not native question selection");
                Set(run, "_currentRooms", new List<AbstractRoom>());
                ((HashSet<ModelId>)run.VisitedEventIds).Add(ModelDb.GetId(T(item.Person)));
                Rejected("native visited identity is checked before person/Fixed side effects");
                ((HashSet<ModelId>)run.VisitedEventIds).Clear();
                await (Task)Call(item.Person + "Patch", "InsertAtBoundaryAsync")!;
                Check(Encode(shared) == before && run.CurrentRoomCount == 0,
                    item.Person + ": legacy map callback cannot insert a room or initialize a person");

                string rng = JsonSerializer.Serialize(run.Rng.ToSerializable());
                int queue = (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)!;
                EventModel chosen = run.Act.PullNextEvent(run);
                Check(chosen.GetType() == T(item.Person), $"Real native PullNextEvent selects {item.Person}, got {chosen.GetType().Name}.");
                Check(run.VisitedEventIds.SetEquals([chosen.Id]) && !run.VisitedEventIds.Contains(original.Id)
                    && JsonSerializer.Serialize(run.Rng.ToSerializable()) == rng
                    && (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)! == queue,
                    item.Person + ": exactly the returned identity is visited, with no extra RNG or queue consumption");
                object state = Get(CurrentShared(run), item.Person);
                Check((bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!,
                    item.Person + ": a usable person record exists before the native room starts");
                EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(new EventRoom(chosen).ToSerializable(), null!)!;
                Check(restored.CanonicalEvent.GetType() == T(item.Person),
                    item.Person + ": the native room checkpoint round-trips the exact event identity");
                if (startingJourney is not null)
                {
                    object afterJourney = Get(CurrentShared(run), "WesternJourney");
                    Check(JsonSerializer.Serialize(((IEnumerable<int>)Get(afterJourney, "ResolvedQuestionActs")).Order()) == questionBudget
                        && (int)Get(afterJourney, "LastFixedAct") == fixedAct + (item.Person == "DescartesMethodicReconstruction" ? 1 : 0),
                        item.Person + ": occupying a physical question node preserves its original Fixed/Practice business budget");
                }
                typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", Methods)!.Invoke(null,
                    [run, fixture.Player, chosen, CurrentShared(run), item.Act]);
                Set(run, "_currentRooms", new List<AbstractRoom>());
                // 040 and 046 compete for the same Rousseau-history Fixed2.
                // Make the competitor available here so this guard does not
                // depend on another native suite running first.
                if (item.Person == "MachiavelliFloodPreparation")
                    ModelDb.Inject(typeof(WollstonecraftAdmission));
                string once = Encode(CurrentShared(run));
                Call("PhilosophyRunStateService", "Restore", run, Decode(once));
                Check(ReferenceEquals(carrier.ModifyNextEvent(original), original)
                    && Encode(CurrentShared(run)) == once,
                    item.Person + ": revisiting selection cannot recreate this event or spend a second slot");
                if (item.Person == "DescartesMethodicReconstruction")
                    Check((int)Get(Get(CurrentShared(run), "WesternJourney"), "LastFixedAct") == 2
                        && ((IEnumerable<int>)Get(Get(CurrentShared(run), "WesternJourney"), "DeclinedFixedActs")).Contains(2),
                        "Descartes retains its actual Fixed2 refusal while using a physical question node.");
            }
            CheckGalileoHistory();
            CheckMarcusInterval();
            CheckPlotinusInterval();
            CheckMissingPredecessor();
            Console.WriteLine("All route question-node checks passed: all 19 former Fixed/Practice events initialize through real native PullNextEvent/public relic hooks, original priority, visited/RNG/queue invariants, native room checkpoints, no-op map callbacks, and no side effects on illegal nodes/rooms/visited events; ordinary-combat history and actual predecessor guards. No rendered UI, complete run replay or player-save access.");
        }
        finally
        {
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave);
            Set(manager, "ActionExecutor", priorExecutor);
            Set(manager, "EventSynchronizer", priorSynchronizer); Set(manager, "NetService", priorNetwork);
            Set(CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
        }
    }

    private static object CurrentShared(RunState run)
    {
        object?[] args = [run, null];
        Check((bool)T("PhilosophyRunStateService").GetMethod("TryGet")!.Invoke(null, args)!, "The real run state remains bound.");
        return args[1]!;
    }

    private static object BuildShared(string person, string node, string carrier, RunState run)
    {
        object shared = node.Length == 0 ? Activator.CreateInstance(T("PhilosophyRunState"), true)!
            : typeof(NativeQuestionNodeChecks).GetMethod("Shared", Methods)!.Invoke(null, ["None", node, carrier])!;
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object? journey = node.Length == 0 ? null : Get(shared, "WesternJourney");
        bool Apply(string edge) => (bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph, [journey, edge])!;
        void Edge(string edge) => Check(Apply(edge), "Real route applies " + edge);
        void Store(string name, object state) => Put(shared, name, state);

        if (person is "PlatoWholeArrangement" or "EpicurusSharedMargin" or "PlotinusCommonGround") Edge("WESTERN_EDGE_008");
        if (person is "EpicurusSharedMargin" or "PlotinusCommonGround") Edge("WESTERN_EDGE_009");
        if (person == "PlotinusCommonGround")
        {
            object epicurus = Create("EpicurusSharedMargin", "EPICURUS:QUESTION:3");
            Commit("EpicurusSharedMargin", "EpicurusMarginAction", epicurus, "DeclineInvitation");
            Commit("EpicurusSharedMargin", "EpicurusMarginAction", epicurus, "Leave");
            Store("EpicurusSharedMargin", epicurus);
        }
        if (person == "DescartesMethodicReconstruction")
        {
            Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!, "Knowledge Question1 reserved.");
            object sextus = Create("SextusOpposedAppearances", "SEXTUS:QUESTION:1");
            foreach (string action in new[] { "AcceptInvitation", "SuspendAssent", "Leave" })
                Commit("SextusOpposedAppearances", "SextusAppearancesAction", sextus, action);
            Store("SextusOpposedAppearances", sextus);
        }
        if (person == "AugustinePromise") Edge("WESTERN_EDGE_019");
        if (person == "FichteSelfPositing")
        {
            Put(journey!, "LastFixedAct", 3);
            ((HashSet<string>)Get(journey!, "CompletedEdgeIds")).Add("WESTERN_EDGE_022");
            Put(journey!, "CurrentNodeId", "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE");
            ModelDb.Inject(typeof(KantUniversalization));
        }
        if (person == "MillHarm") Edge("WESTERN_EDGE_040");
        if (person == "BeauvoirOther")
        {
            Edge("WESTERN_EDGE_040");
            object mill = Create("MillHarm", "MILL_HARM:QUESTION:2");
            foreach (string action in new[] { "ApplyHarmTest", "ProtectConduct", "Leave" })
                Commit("MillHarm", "MillHarmAction", mill, action, journey!, graph);
            Store("MillHarm", mill);
        }
        if (person == "DemocritusArrangement") Edge("WESTERN_EDGE_001");
        if (person == "AquinasBridgeNotice")
        {
            object aristotle = Create("AristotleCivicInquiry", "ARISTOTLE:QUESTION:2");
            foreach (string action in new[] { "AcceptInvitation", "Centralize", "ShareDecision", "AdoptInquiry", "Leave" })
                Commit("AristotleCivicInquiry", "AristotleCivicInquiryAction", aristotle, action, journey!, graph);
            Store("AristotleCivicInquiry", aristotle);
        }
        if (person == "BaconSampleLedger")
        {
            Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!, "Language Question1 reserved.");
            object gorgias = Create("GorgiasSpeechInquiry", "GORGIAS:QUESTION:1");
            foreach (string action in new[] { "AcceptInvitation", "AssertContents", "SeparateBeliefFromProof", "RecordBoundary", "Leave" })
                Commit("GorgiasSpeechInquiry", "GorgiasSpeechInquiryAction", gorgias, action, journey!);
            object ockham = Create("OckhamNamesInquiry", "OCKHAM:QUESTION:2", Get(gorgias, "InstanceId"));
            foreach (string action in new[] { "Accept", "CheckWorkers", "UseNamedList", "Close" })
                Commit("OckhamNamesInquiry", "OckhamNamesAction", ockham, action, journey!, gorgias, graph);
            Store("GorgiasSpeechInquiry", gorgias); Store("OckhamNamesInquiry", ockham);
        }
        if (person == "GalileoMeasuredPractice")
        {
            Edge("WESTERN_EDGE_001");
            object democritus = Create("DemocritusArrangement", "DEMOCRITUS:QUESTION:2");
            foreach (string action in new[] { "AcceptInvitation", "StudyMotion", "LeaveGap", "AdoptAtoms", "Leave" })
                Commit("DemocritusArrangement", "DemocritusArrangementAction", democritus, action, journey!, graph);
            Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 2])!, "Being Question2 reserved.");
            object empedocles = Create("EmpedoclesRootsInquiry", "EMPEDOCLES:QUESTION:2");
            foreach (string action in new[] { "AcceptInvitation", "StudyComposition", "CombineDifferent", "RecordComparison", "Leave" })
                Commit("EmpedoclesRootsInquiry", "EmpedoclesRootsInquiryAction", empedocles, action, journey!, graph);
            Store("DemocritusArrangement", democritus); Store("EmpedoclesRootsInquiry", empedocles);
        }
        if (person is "SenecaDailyReview" or "EpictetusGuidePractice" or "MarcusAureliusCampPractice")
            ConfigureStoic(shared, person, run.Rng.Seed);

        object behavior = Activator.CreateInstance(T("ActBehaviorState"), true)!;
        Put(behavior, "ActIndex", run.CurrentActIndex); Put(behavior, "CompletedCombatCount", 2);
        ((IDictionary)Get(shared, "ActBehaviorStates"))[run.CurrentActIndex] = behavior;
        Set(run, "_mapPointHistory", Enumerable.Range(0, run.CurrentActIndex + 1)
            .Select(act => act == run.CurrentActIndex ? new List<MapPointHistoryEntry>
                { new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] } }
                : new List<MapPointHistoryEntry>()).ToList());
        if (person == "FichteSelfPositing")
            Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), new(),
                new()
                {
                    new() { MapPointType = MapPointType.Unknown,
                        Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<KantUniversalization>() }] },
                    new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] },
                } });
        if (person == "MillHarm")
            Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(),
                new()
                {
                    new() { MapPointType = MapPointType.Unknown,
                        Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<WollstonecraftAdmission>() }] },
                    new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] },
                } });
        if (person == "BeauvoirOther")
            Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), new(),
                new()
                {
                    new() { MapPointType = MapPointType.Unknown,
                        Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<WollstonecraftAdmission>() }] },
                    new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] },
                } });
        if (person is "MarcusAureliusCampPractice" or "PlotinusCommonGround")
            Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), new(),
                new()
                {
                    new() { MapPointType = MapPointType.Unknown,
                        Rooms = [new() { RoomType = RoomType.Event, ModelId = person == "PlotinusCommonGround"
                            ? ModelDb.GetId<EpicurusSharedMargin>() : ModelDb.GetId<EpictetusGuidePractice>() }] },
                    new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] },
                } });
        return Decode(Encode(shared));
    }

    private static void ConfigureStoic(object shared, string person, ulong seed)
    {
        object context = Activator.CreateInstance(T("DiogenesStaySwitchEntryContext"), [2, 1, true, false, 17])!;
        object?[] args = [shared, seed, context, null];
        Check((bool)T("DiogenesStaySwitchEntryPolicy").GetMethod("TryCreate")!.Invoke(null, args)!, "Real Diogenes entry freezes the source.");
        object entry = args[3]!; Put(entry, "Closed", true); Put(shared, "DiogenesStaySwitchEntry", entry);
        object route = Activator.CreateInstance(T("ZenoRouteState"), [1, Enum.Parse(T("ZenoRouteStage"), "ZenoClosed"),
            6L, 6L, Get(entry, "RunId"), 2, "WESTERN_EDGE_011", "ZENO_OF_CITIUM__VIRTUE_AND_HAPPINESS__CORE",
            Get(entry, "Material"), "ROOM_18", Enum.Parse(T("ZenoOpeningTrigger"), "SafeBoundary"),
            Enum.Parse(T("ZenoResumeDestination"), "Map"), "MAP_18", "ZENO_18", Enum.Parse(T("ZenoAssentOutcome"), "NoAssent"), null])!;
        object feature = Activator.CreateInstance(T("ZenoRouteFeatureState"), [1, route, null])!;
        shared.GetType().GetMethods(Methods).Single(method => method.Name == "SetCurrentZenoRouteState" && method.GetParameters().Length == 1)
            .Invoke(shared, [feature]);
        if (person == "SenecaDailyReview") return;
        object seneca = Create("SenecaDailyReview", "SENECA:QUESTION:3", route, entry);
        foreach (string action in new[] { "Begin", "ExamineJudgment", "KeepCheck", "Leave" })
            Commit("SenecaDailyReview", "SenecaDailyReviewAction", seneca, action, route, entry);
        Put(shared, "SenecaDailyReview", seneca);
        if (person == "EpictetusGuidePractice") return;
        object epictetus = Create("EpictetusGuidePractice", "EPICTETUS:QUESTION:3", route, seneca);
        foreach (string action in new[] { "GuideWithReservation", "EscortCompanion", "Leave" })
            Commit("EpictetusGuidePractice", "EpictetusGuidePracticeAction", epictetus, action, route, seneca);
        Put(shared, "EpictetusGuidePractice", epictetus);
    }

    private static void Commit(string person, string actionType, object state, string name, params object[] rest)
    {
        object action = Enum.Parse(T(actionType), name);
        Check((bool)Call(person + "Flow", "TryPrepare", state, action)!
            && (bool)Call(person + "Flow", "TryCommitPending", [state, .. rest])!, "Real predecessor commits " + person + ":" + name);
    }

    private static void CheckGalileoHistory()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!.Invoke(null, [typeof(HeraclitusLyre), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!; var run = (RunState)tuple[1]!;
        object shared = BuildShared("GalileoMeasuredPractice", "HERACLITUS__BEING_AND_CHANGE__CORE", fixture.Relic.Id.Entry, run);
        Call("PhilosophyRunStateService", "Restore", run, shared); shared = CurrentShared(run);
        string before = Encode(shared);
        Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), new(),
            new() { new() { MapPointType = MapPointType.Elite, Rooms = [new() { RoomType = RoomType.Elite }] } } });
        Check(Call("GalileoMeasuredPracticePatch", "TrySelectNative", run) is null && Encode(shared) == before,
            "Galileo cannot substitute aggregate combat count or an Elite history for a completed normal battle.");
        Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), new(), new() });
        Check(Call("GalileoMeasuredPracticePatch", "TrySelectNative", run) is null && Encode(shared) == before,
            "Galileo cannot manufacture native battle history from aggregate observations.");
    }

    private static void CheckMissingPredecessor()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!.Invoke(null, [typeof(PlatoCivicSeal), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!; var run = (RunState)tuple[1]!;
        object shared = BuildShared("AquinasBridgeNotice", "PLATO__FREEDOM_AND_INSTITUTIONS__CORE", fixture.Relic.Id.Entry, run);
        Put(shared, "AristotleCivicInquiry", null);
        Call("PhilosophyRunStateService", "Restore", run, shared); shared = CurrentShared(run);
        string before = Encode(shared);
        Check(Call("AquinasBridgeNoticePatch", "TrySelectNative", run) is null && Encode(shared) == before,
            "A matching relic/graph edge cannot replace the actual closed adopted Aristotle predecessor.");
    }

    private static void CheckMarcusInterval()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!.Invoke(null, [typeof(SocratesBalanceWeight), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!; var run = (RunState)tuple[1]!;
        object shared = BuildShared("MarcusAureliusCampPractice", "SOCRATES__VIRTUE_AND_HAPPINESS__CORE", fixture.Relic.Id.Entry, run);
        Call("PhilosophyRunStateService", "Restore", run, shared); shared = CurrentShared(run);
        string before = Encode(shared);
        MapPointHistoryEntry predecessor = new() { MapPointType = MapPointType.Unknown,
            Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<EpictetusGuidePractice>() }] };
        Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), new(),
            new() { new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] }, predecessor } });
        Check(Call("MarcusAureliusCampPracticePatch", "TrySelectNative", run) is null && Encode(shared) == before,
            "Marcus cannot use a battle before Epictetus closed as its required intervening battle.");
        Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), new(), new() { predecessor } });
        Check(Call("MarcusAureliusCampPracticePatch", "TrySelectNative", run) is null && Encode(shared) == before,
            "Marcus needs actual completed native battle history after the predecessor event.");
    }

    private static void CheckPlotinusInterval()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!.Invoke(null, [typeof(SocratesBalanceWeight), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!; var run = (RunState)tuple[1]!;
        object shared = BuildShared("PlotinusCommonGround", "SOCRATES__VIRTUE_AND_HAPPINESS__CORE", fixture.Relic.Id.Entry, run);
        Call("PhilosophyRunStateService", "Restore", run, shared); shared = CurrentShared(run);
        string before = Encode(shared);
        MapPointHistoryEntry predecessor = new() { MapPointType = MapPointType.Unknown,
            Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<EpicurusSharedMargin>() }] };
        Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), new(),
            new() { new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] }, predecessor } });
        Check(Call("PlotinusCommonGroundPatch", "TrySelectNative", run) is null && Encode(shared) == before,
            "Plotinus cannot use two aggregate combats or a battle before Epicurus as the required later combat.");
        Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), new(), new() { predecessor } });
        Check(Call("PlotinusCommonGroundPatch", "TrySelectNative", run) is null && Encode(shared) == before,
            "Plotinus requires actual non-Boss battle history after the closed Epicurus event.");
    }
}
