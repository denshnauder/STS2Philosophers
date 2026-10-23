using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class NativeQuestionNodeChecks
{
    private static readonly Assembly Mod = typeof(PlatoCivicSeal).Assembly;
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type TypeOf(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) => TypeOf(type)
        .GetMethods(Methods).Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length)
        .Invoke(null, args)!;
    private static object Get(object state, string name) => state.GetType().GetProperty(name)!.GetValue(state)!;
    private static void Put(object state, string name, object value) => state.GetType().GetProperty(name)!.SetValue(state, value);
    private static object Reload(object state) => Call("PhilosophyRunStateCodec", "Decode", Call("PhilosophyRunStateCodec", "Encode", state));
    private static string Encoded(object state) => (string)Call("PhilosophyRunStateCodec", "Encode", state);
    private static void Commit(string person, string actionType, object state, string name, params object[] rest)
    {
        Check((bool)Call(person + "Flow", "TryPrepare", state, Enum.Parse(TypeOf(actionType), name))
            && (bool)Call(person + "Flow", "TryCommitPending", [state, .. rest]), $"Question predecessor commits actual {name}.");
    }

    private static readonly (string Person, string Kind, int Act, Type Carrier, string Node)[] Cases =
    [
        ("ProtagorasSharedDeliberation", "Protagoras", 0, typeof(SocratesBalanceWeight), "SOCRATES__VIRTUE_AND_HAPPINESS__CORE"),
        ("ParmenidesInquiry", "Parmenides", 0, typeof(HeraclitusLyre), "HERACLITUS__BEING_AND_CHANGE__CORE"),
        ("SextusOpposedAppearances", "Sextus", 0, typeof(SocratesQuestionCup), "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"),
        ("GorgiasSpeechInquiry", "Gorgias", 0, typeof(AristotleCategoryTablet), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE"),
        ("ZenoCivicChallenge", "Zeno", 0, typeof(PlatoCivicSeal), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE"),
        ("AnaximanderOriginInquiry", "Anaximander", 1, typeof(HeraclitusLyre), "HERACLITUS__BEING_AND_CHANGE__CORE"),
        ("EmpedoclesRootsInquiry", "Empedocles", 1, typeof(HeraclitusLyre), "HERACLITUS__BEING_AND_CHANGE__CORE"),
        ("AnaxagorasMixtureInquiry", "Anaxagoras", 1, typeof(HeraclitusLyre), "HERACLITUS__BEING_AND_CHANGE__CORE"),
        ("PyrrhoReportedCrossing", "Pyrrho", 1, typeof(SocratesQuestionCup), "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"),
        ("OckhamNamesInquiry", "Ockham", 1, typeof(AristotleCategoryTablet), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE"),
        ("LutherSignedCare", "Luther", 1, typeof(PlatoCivicSeal), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE"),
        ("AnselmUnfinishedProof", "Anselm", 2, typeof(SocratesQuestionCup), "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"),
    ];

    private sealed class QuestionMap : ActMap
    {
        private readonly MapPoint[,] _grid = new MapPoint[3, 2];
        protected override MapPoint[,] Grid => _grid;
        public override MapPoint StartingMapPoint => Point;
        public override MapPoint BossMapPoint => Right;
        internal MapPoint Point { get; } = new(1, 0) { PointType = MapPointType.Unknown };
        internal MapPoint Left { get; } = new(0, 1) { PointType = MapPointType.Monster };
        internal MapPoint Right { get; } = new(2, 1) { PointType = MapPointType.Monster };
        internal QuestionMap()
        {
            _grid[1, 0] = Point; _grid[0, 1] = Left; _grid[2, 1] = Right;
            Point.AddChildPoint(Left); Point.AddChildPoint(Right);
        }
    }

    public static Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        object? priorTurn = typeof(CombatManager).GetField("_turnState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(CombatManager.Instance);
        object priorSynchronizer = manager.EventSynchronizer;
        object? priorNetwork = manager.NetService;
        try
        {
            TestMode.IsOn = true;
            foreach (var item in Cases)
            {
                ModelDb.Inject(TypeOf(item.Person));
                var (fixture, run, map, pool, original) = Fixture(item.Carrier, item.Act);
                object shared = Shared(item.Kind, item.Node, fixture.Relic.Id.Entry);
                Call("PhilosophyRunStateService", "Restore", run, shared);
                CheckNativeGuards(fixture, run, map, original, shared);
                string rng = JsonSerializer.Serialize(run.Rng.ToSerializable());
                int originalQueueIndex = (int)typeof(RoomSet).GetField("eventsVisited")!.GetValue(pool)!;
                EventModel chosen = run.Act.PullNextEvent(run); // The actual native hook/visited pipeline.
                Check(chosen.GetType() == TypeOf(item.Person), $"Actual native PullNextEvent selects {item.Person}.");
                Check(run.VisitedEventIds.SetEquals([chosen.Id]) && !run.VisitedEventIds.Contains(original.Id),
                    "The native visited set records the returned question, leaving the displaced original unvisited.");
                Check(JsonSerializer.Serialize(run.Rng.ToSerializable()) == rng
                    && (int)typeof(RoomSet).GetField("eventsVisited")!.GetValue(pool)! == originalQueueIndex,
                    "The override consumes neither a second RNG value nor another native event queue entry.");
                object started = CurrentShared(run);
                object person = Get(started, item.Person);
                Check((bool)person.GetType().GetMethod("IsUsable")!.Invoke(person, null)!,
                    "The real person record exists and is usable before native EventModel startup.");
                CheckRoomRecovery(run, fixture.Player, chosen, started, item.Act);
                string once = Encoded(CurrentShared(run));
                Set(run, "_currentRooms", new List<AbstractRoom>());
                Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original)
                    && Encoded(CurrentShared(run)) == once,
                    "Repeated entry cannot refresh an already initialized question or its budget.");
                CheckLegacyPatchHasNoFactory(item.Person);
            }
            CheckNativeNonEvent();
            CheckBindings();
            Console.WriteLine("Native question node checks passed: all 12 real person initializations through native PullNextEvent/public relic hook, exact visited IDs and unchanged RNG/event queue, actual sole-carrier/current-run/room/Unknown gates, native room round-trips and one/two-room recovery, legacy factory removal, unchanged native Shop roll/creation, and save/Roll/CreateRoom/history ordering. No rendered UI, full map travel, or player-save access.");
            return Task.CompletedTask;
        }
        finally
        {
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave);
            Set(manager, "EventSynchronizer", priorSynchronizer);
            Set(manager, "NetService", priorNetwork);
            Set(CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
        }
    }

    private static (NativeRuntimeFixture Fixture, RunState Run, QuestionMap Map, RoomSet Pool, EventModel Original) Fixture(Type relic, int actIndex)
    {
        Set(RunManager.Instance, "State", null);
        NativeRuntimeFixture fixture = new(relic);
        Set(CombatManager.Instance, "_turnState", null);
        RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { fixture.Player }); Set(run, "_currentActIndex", actIndex);
        Set(run, "Rng", new RunRngSet("123456789")); Set(run, "ActFloor", 1);
        Set(run, "_currentRooms", new List<AbstractRoom>()); Set(run, "_allCards", new List<CardModel>());
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        ModelDb.Inject(typeof(MultiplayerScalingModel)); Set(run, "MultiplayerScalingModel", ModelDb.Singleton<MultiplayerScalingModel>());
        Set(fixture.Player, "Deck", new CardPile(PileType.Deck)); Set(fixture.Player, "_runState", run);
        Set(fixture.Combat, "RunState", run);
        QuestionMap map = new(); Set(run, "Map", map); Set(run, "_visitedMapCoords", new List<MapCoord> { map.Point.coord });
        Set(run, "_mapPointHistory", Enumerable.Range(0, actIndex + 1).Select(_ => new List<MapPointHistoryEntry>()).ToList());
        Set(run, "_visitedEventIds", new HashSet<ModelId>());
        Set(run, "UnlockState", new UnlockState(Array.Empty<string>(), Array.Empty<ModelId>(), 1));
        Set(run, "Odds", new MegaCrit.Sts2.Core.Odds.RunOddsSet(run.Rng.UnknownMapPoint));
        Type native = typeof(EventModel).Assembly.GetTypes().Where(type => !type.IsAbstract
            && type.Namespace == "MegaCrit.Sts2.Core.Models.Events" && typeof(EventModel).IsAssignableFrom(type)
            && !typeof(AncientEventModel).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null
            && type.GetMethod(nameof(EventModel.IsAllowed))!.DeclaringType == typeof(EventModel))
            .OrderBy(type => type.FullName).First();
        ModelDb.Inject(native); EventModel original = ModelDb.GetById<EventModel>(ModelDb.GetId(native));
        RoomSet pool = new(); Set(pool, "events", new List<EventModel> { original });
        ActModel act = (ActModel)RuntimeHelpers.GetUninitializedObject(typeof(Overgrowth)); Set(act, "_rooms", pool);
        Set(run, "Acts", new ActModel[] { act, act, act });
        Set(RunManager.Instance, "State", run); Set(RunManager.Instance, "ShouldSave", true);
        Set(RunManager.Instance, "NetService", new NetSingleplayerGameService());
        return (fixture, run, map, pool, original);
    }

    private static object CurrentShared(RunState run)
    {
        object?[] args = [run, null];
        Check((bool)TypeOf("PhilosophyRunStateService").GetMethod("TryGet")!.Invoke(null, args)!, "Current shared state remains accessible.");
        return args[1]!;
    }

    private static void CheckNativeGuards(NativeRuntimeFixture fixture, RunState run, QuestionMap map, EventModel original, object shared)
    {
        string before = Encoded(shared);
        void Unchanged(string reason) => Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original)
            && Encoded(shared) == before, reason);
        map.Point.PointType = MapPointType.Monster; Unchanged("A combat map node never becomes a question event."); map.Point.PointType = MapPointType.Unknown;
        Set(run, "_currentRooms", new List<AbstractRoom> { new MapRoom() });
        Unchanged("Calling the hook inside an existing room cannot simulate native node entry."); Set(run, "_currentRooms", new List<AbstractRoom>());
        Set(RunManager.Instance, "ShouldSave", false); Unchanged("A nonpersistent run cannot start a saved question."); Set(RunManager.Instance, "ShouldSave", true);
        Set(RunManager.Instance, "State", null); Unchanged("A stale owner run cannot consume a current question."); Set(RunManager.Instance, "State", run);
        object network = RunManager.Instance.NetService;
        Set(RunManager.Instance, "NetService", null); Unchanged("An absent singleplayer network service cannot authorize node entry.");
        Set(RunManager.Instance, "NetService", network);
        var copy = ModelDb.GetById<RelicModel>(fixture.Relic.Id).ToMutable(); copy.Owner = fixture.Player;
        ((List<RelicModel>)fixture.Player.Relics).Add(copy); Unchanged("Two Western carriers disable selection.");
        ((List<RelicModel>)fixture.Player.Relics).Remove(fixture.Relic);
        Unchanged("Assigning Owner without actually holding this carrier cannot authorize a question.");
        ((List<RelicModel>)fixture.Player.Relics).Add(fixture.Relic);
        ((List<RelicModel>)fixture.Player.Relics).Remove(copy);
        ((List<Player>)run.Players).Add(fixture.Player); Unchanged("A multiplayer roster cannot enter the singleplayer question.");
        ((List<Player>)run.Players).RemoveAt(1);
    }

    private static void CheckRoomRecovery(RunState run, Player player, EventModel chosen, object shared, int actIndex)
    {
        EventRoom room = new(chosen);
        var saved = room.ToSerializable();
        EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(saved, null!)!;
        Check(ReferenceEquals(restored.CanonicalEvent, chosen) && Equals(restored.ToSerializable().EventId, chosen.Id),
            "The normal native room checkpoint preserves the selected event identity.");
        object reloaded = Reload(shared); Call("PhilosophyRunStateService", "Restore", run, reloaded);
        string before = Encoded(reloaded), rng = JsonSerializer.Serialize(run.Rng.ToSerializable());
        EventModel local = chosen.ToMutable(); Set(local, "Owner", player);
        object synchronizer = RuntimeHelpers.GetUninitializedObject(typeof(RunManager).GetProperty("EventSynchronizer")!.PropertyType);
        Set(synchronizer, "_events", new List<EventModel> { local }); Set(synchronizer, "_playerCollection", run);
        Set(synchronizer, "_localPlayerId", player.NetId); Set(synchronizer, "_pendingOptionTasks", new List<Task>());
        Set(RunManager.Instance, "EventSynchronizer", synchronizer);
        foreach (var rooms in new[] { new List<AbstractRoom> { restored }, new List<AbstractRoom> { new MapRoom(), room } })
        {
            Set(run, "_currentRooms", rooms);
            Check((bool)Call("RouteEventMapBoundary", "IsActiveEvent", run, local, actIndex),
                "Both a natural single room and a legacy nested question retain exact active-event identity.");
            Check(local.GetType().GetMethod("TryRestoreState", Methods)!.Invoke(local, null) is not null,
                "Actual event restoration reads the initialized/reloaded person state without another factory.");
            Check(Encoded(CurrentShared(run)) == before && JsonSerializer.Serialize(run.Rng.ToSerializable()) == rng,
                "Native room and shared-state restoration consume neither a second budget nor an event roll.");
        }
    }

    private static void CheckNativeNonEvent()
    {
        var (fixture, run, _, _, _) = Fixture(typeof(PlatoCivicSeal), 0);
        object shared = Shared("Zeno", "PLATO__FREEDOM_AND_INSTITUTIONS__CORE", fixture.Relic.Id.Entry);
        Call("PhilosophyRunStateService", "Restore", run, shared); string before = Encoded(shared);
        // The native odds dictionary also contains disabled Elite=-1. When Event
        // is blacklisted its fallback is the first allowed type, so omit no other
        // room type from this controlled, Shop-only native roll fixture.
        RoomType[] blacklist = Enum.GetValues<RoomType>().Where(type => type != RoomType.Shop).ToArray();
        RoomType rolled = (RoomType)typeof(RunManager).GetMethod("RollRoomTypeFor", Methods)!.Invoke(RunManager.Instance,
            [MapPointType.Unknown, blacklist])!;
        Check(rolled == RoomType.Shop, "The actual native Unknown room-type roll can remain a Shop.");
        AbstractRoom room = (AbstractRoom)typeof(RunManager).GetMethod("CreateRoom", Methods)!
            .Invoke(RunManager.Instance, [rolled, MapPointType.Unknown, null])!;
        Check(room is MerchantRoom && Encoded(shared) == before && run.VisitedEventIds.Count == 0,
            "Native Shop creation never calls event selection or consumes its question.");
    }

    private static void CheckLegacyPatchHasNoFactory(string person)
    {
        MethodInfo patch = TypeOf(person + "Patch").GetMethod("InsertAtBoundaryAsync", Methods)!;
        MethodBase[] calls = References(patch).ToArray();
        Check(!calls.Any(method => method.Name is "Create" or "TryReserveQuestion" or "TryDecline")
            && calls.Any(method => method.DeclaringType == TypeOf("RouteEventMapBoundary") && method.Name == "EnterAsync"),
            "Legacy boundary patches retain recovery entry but contain no new invitation or budget factory.");
    }

    private static void CheckBindings()
    {
        MethodInfo hook = typeof(WesternPracticeRelic).GetMethod(nameof(AbstractModel.ModifyNextEvent))!;
        Check(hook.GetBaseDefinition().DeclaringType == typeof(AbstractModel)
            && References(hook).Any(method => method.DeclaringType == TypeOf("NativeQuestionNodeEvents")),
            "Selection is bound to the actual public native model hook through the single carrier.");
        Check(!Mod.GetTypes().SelectMany(type => type.GetMethods(Methods | BindingFlags.DeclaredOnly))
            .Any(method => method.Name == nameof(AbstractModel.ModifyUnknownMapPointRoomTypes)),
            "The mod does not override native Unknown room-type candidates.");
        var pull = References(typeof(ActModel).GetMethod(nameof(ActModel.PullNextEvent))!).ToList();
        int modify = pull.FindIndex(method => method.DeclaringType?.Name == "Hook" && method.Name == "ModifyNextEvent");
        Check(modify > pull.FindIndex(method => method.Name == "EnsureNextEventIsValid")
            && pull.FindIndex(method => method.Name == "AddVisitedEvent") > modify,
            "Native validation precedes replacement and the final returned model is registered afterwards.");
        var enter = References(typeof(RunManager).GetMethod("EnterMapPointInternal", Methods)!).ToList();
        int save = enter.FindIndex(method => method.DeclaringType == typeof(SaveManager) && method.Name == "SaveRun");
        int roll = enter.FindIndex(method => method.Name == "RollRoomTypeFor");
        int create = enter.FindIndex(method => method.Name == "CreateRoom");
        int history = enter.FindIndex(method => method.Name == "AppendToMapPointHistory");
        Check(save >= 0 && save < roll && roll < create && create < history,
            "The real pre-entry checkpoint captures the target/RNG before Roll and selection; history follows the created room.");
        var start = References(typeof(RunManager).GetMethod("EnterRoomInternal", Methods)!).ToList();
        Check(start.FindIndex(method => method.Name == "PushRoom") >= 0
            && start.FindIndex(method => method.Name == "PushRoom")
                < start.FindIndex(method => method.Name == "GetAwaiter"),
            "The created native room is pushed before startup first awaits, keeping the selected state and room together.");
    }

    private static IEnumerable<MethodBase> References(MethodInfo method)
    {
        Type? machine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;
        if (machine is not null) method = machine.GetMethod("MoveNext", Methods)!;
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(opcode => (ushort)opcode.Value);
        for (int i = 0; i < il.Length;)
        {
            ushort value = il[i++]; if (value == 0xfe) value = (ushort)(0xfe00 | il[i++]);
            OpCode opcode = opcodes[value];
            if (opcode.OperandType == OperandType.InlineMethod)
            {
                int token = BitConverter.ToInt32(il, i); i += 4;
                MethodBase? target = null;
                try { target = method.Module.ResolveMethod(token, method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments()); }
                catch (ArgumentException) { }
                if (target is not null) yield return target;
                continue;
            }
            i += opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4,
            };
        }
    }

    private static object Shared(string kind, string node, string carrier)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, [node])!;
        object shared = Activator.CreateInstance(TypeOf("PhilosophyRunState"), nonPublic: true)!;
        object doctrine = Activator.CreateInstance(TypeOf("CurrentDoctrine"), nonPublic: true)!;
        Put(doctrine, "DoctrineId", carrier); Put(doctrine, "ThinkerId", node.Split("__")[0]);
        Put(shared, "CurrentDoctrine", doctrine); Put(shared, "WesternJourney", journey);
        bool GraphCall(string method, params object[] rest) => (bool)graph.GetType().GetMethod(method)!.Invoke(graph, [journey, .. rest])!;
        object Create(string person, string id, params object[] rest) => Call(person + "State", "Create", [id, .. rest]);
        if (kind is "Anaximander" or "Empedocles" or "Anaxagoras")
        {
            Check(GraphCall("TryReserveQuestion", 1), "Actual being Question1 reserved.");
            object prior = Create("ParmenidesInquiry", "PARMENIDES:NATIVE:1"); Put(shared, "ParmenidesInquiry", prior);
            string[] actions = kind == "Anaximander" ? ["DeclineInvitation", "Leave"]
                : ["AcceptInvitation", "SpeakOfBeing", "AdoptBoundary", "Leave"];
            foreach (string action in actions) Commit("ParmenidesInquiry", "ParmenidesInquiryAction", prior, action, journey, graph);
            if (kind != "Anaximander")
            {
                object democritus = Create("DemocritusArrangement", "DEMOCRITUS:NATIVE:2"); Put(shared, "DemocritusArrangement", democritus);
                string[] fixedActions = kind == "Empedocles"
                    ? ["AcceptInvitation", "StudyMotion", "LeaveGap", "AdoptAtoms", "Leave"] : ["DeclineInvitation", "Leave"];
                foreach (string action in fixedActions) Commit("DemocritusArrangement", "DemocritusArrangementAction", democritus, action, journey, graph);
            }
        }
        if (kind == "Ockham")
        {
            Check(GraphCall("TryReserveQuestion", 1), "Actual language Question1 reserved.");
            object prior = Create("GorgiasSpeechInquiry", "GORGIAS:NATIVE:1"); Put(shared, "GorgiasSpeechInquiry", prior);
            foreach (string action in new[] { "AcceptInvitation", "AssertContents", "SeparateBeliefFromProof", "RecordBoundary", "Leave" })
                Commit("GorgiasSpeechInquiry", "GorgiasSpeechInquiryAction", prior, action, journey);
        }
        if (kind == "Luther")
        {
            Check(GraphCall("TryReserveQuestion", 1), "Actual institution Question1 reserved.");
            object zeno = Create("ZenoCivicChallenge", "ZENO:NATIVE:1"); Put(shared, "ZenoCivicChallenge", zeno);
            foreach (string action in new[] { "AcceptInvitation", "DemandPublicRule", "PublishPublicObjection", "Leave" })
                Commit("ZenoCivicChallenge", "ZenoCivicAction", zeno, action, journey, graph);
            object prior = Create("AugustinePromise", "AUGUSTINE:NATIVE:2"); Put(shared, "AugustinePromise", prior);
            foreach (string action in new[] { "AcceptInvitation", "PromisePublic", "RepairPublic", "Leave" })
                Commit("AugustinePromise", "AugustinePromiseAction", prior, action, journey, graph);
        }
        if (kind is "Pyrrho" or "Anselm")
        {
            Check(GraphCall("TryReserveQuestion", 1) && GraphCall("TryDecline", "Fixed", 2), "Actual knowledge opportunities.");
            object sextus = Create("SextusOpposedAppearances", "SEXTUS:NATIVE:1"); Put(shared, "SextusOpposedAppearances", sextus);
            foreach (string action in new[] { "AcceptInvitation", "SuspendAssent", "Leave" })
                Commit("SextusOpposedAppearances", "SextusAppearancesAction", sextus, action);
            object descartes = Create("DescartesMethodicReconstruction", "DESCARTES:NATIVE:2"); Put(shared, "DescartesMethodicReconstruction", descartes);
            foreach (string action in new[] { "DeclineInvitation", "Leave" })
                Commit("DescartesMethodicReconstruction", "DescartesReconstructionAction", descartes, action);
            if (kind == "Anselm")
            {
                Check(GraphCall("TryReserveQuestion", 2), "Actual knowledge Question2 reserved.");
                object pyrrho = Create("PyrrhoReportedCrossing", "PYRRHO:NATIVE:2", sextus, descartes); Put(shared, "PyrrhoReportedCrossing", pyrrho);
                foreach (string action in new[] { "CiteConflict", "InspectAndCross", "Leave" })
                    Commit("PyrrhoReportedCrossing", "PyrrhoReportedCrossingAction", pyrrho, action, journey, sextus, descartes);
            }
        }
        return Reload(shared);
    }
}
