using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class NewtonTwoSitesNativeChecks
{
    private static readonly Assembly Mod = typeof(SocratesQuestionCup).Assembly;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) => T(type)
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args)!;
    private static object Get(object obj, string name) => obj.GetType().GetProperty(name)!.GetValue(obj)!;
    private static void Put(object obj, string name, object value) => obj.GetType().GetProperty(name)!.SetValue(obj, value);
    private static object Reload(object obj) => Call("PhilosophyRunStateCodec", "Decode", Call("PhilosophyRunStateCodec", "Encode", obj));
    private static void Commit(string flow, string actionEnum, object state, string action, params object[] rest)
    {
        object value = Enum.Parse(T(actionEnum), action);
        Check((bool)Call(flow, "TryPrepare", state, value)
            && (bool)Call(flow, "TryCommitPending", [state, .. rest]), $"Native Newton path commits {action}.");
    }

    private static object Shared(string first, string outcome, bool close = true)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!
            && (bool)graph.GetType().GetMethod("TryDecline")!.Invoke(graph, [journey, "Fixed", 2])!,
            "Native Newton has the actual reserved first question and consumed second Fixed slot.");
        string seed = new RunRngSet("123456789").Seed.ToString();
        object sextus = Call("SextusOpposedAppearancesState", "Create", $"SEXTUS_OPPOSED_APPEARANCES:{seed}:1");
        foreach (string action in new[] { "AcceptInvitation", "SuspendAssent", "Leave" })
            Commit("SextusOpposedAppearancesFlow", "SextusAppearancesAction", sextus, action);
        object descartes = Call("DescartesMethodicReconstructionState", "Create", $"DESCARTES_METHODIC_RECONSTRUCTION:{seed}:2");
        foreach (string action in new[] { "AcceptInvitation", "WithdrawSharedBasis", "InspectCurrentLandmark", "RebuildCoveredJudgment", "Leave" })
            Commit("DescartesMethodicReconstructionFlow", "DescartesReconstructionAction", descartes, action);
        object state = Call("NewtonTwoSitesState", "Create", $"NEWTON_TWO_SITES:{seed}:3", Get(sextus, "InstanceId"), Get(descartes, "InstanceId"));
        string[] path = outcome == "Invitation" ? [] : outcome == "Decline" ? [outcome]
            : ["Accept", first, "RecordError", "RevealOther", outcome];
        foreach (string action in path.Concat(close && outcome != "Invitation" ? ["Leave"] : Array.Empty<string>()))
            Commit("NewtonTwoSitesFlow", "NewtonTwoSitesAction", state, action, journey, sextus, descartes, graph);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
        Put(doctrine, "ThinkerId", "SOCRATES"); Put(doctrine, "DoctrineId", "SOCRATES_QUESTION_CUP");
        Put(shared, "CurrentDoctrine", doctrine); Put(shared, "WesternJourney", journey);
        Put(shared, "SextusOpposedAppearances", sextus); Put(shared, "DescartesMethodicReconstruction", descartes);
        Put(shared, "NewtonTwoSites", state);
        return Reload(shared);
    }

    private static (NativeRuntimeFixture F, RunState Run) Fixture(object shared)
    {
        Set(RunManager.Instance, "State", null);
        var f = new NativeRuntimeFixture(typeof(SocratesQuestionCup));
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { f.Player }); Set(run, "_currentActIndex", 2);
        Set(run, "Rng", new RunRngSet("123456789")); Set(run, "ActFloor", 4);
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(f.Player, "_runState", run); Set(f.Combat, "RunState", run); Set(f.Player, "Deck", new CardPile(PileType.Deck));
        var room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom)); Set(room, "Id", 98);
        Set(run, "_currentRooms", new List<AbstractRoom> { room }); Set(RunManager.Instance, "State", run);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        return (f, run);
    }

    private static SocratesQuestionCup Cup(NativeRuntimeFixture f) => (SocratesQuestionCup)f.Relic;
    private static string Payload(NativeRuntimeFixture f) => (string)Get(Cup(f), "NewtonPracticePayload");
    private static void RestorePayload(NativeRuntimeFixture f, string payload) => Put(Cup(f), "NewtonPracticePayload", payload);
    private static Task StartTurn(NativeRuntimeFixture f) => f.Relic.BeforeSideTurnStart(null!, CombatSide.Player, [f.Player.Creature], f.Combat);
    private static Task EndTurn(NativeRuntimeFixture f) => f.Relic.AfterSideTurnEnd(null!, CombatSide.Player, [f.Player.Creature]);
    private static async Task Begin(NativeRuntimeFixture f)
    {
        await f.Relic.BeforeCombatStart(); await StartTurn(f); f.PlayerState.Energy = 40;
    }
    private static Task<CardPlay> Manual(NativeRuntimeFixture f, bool attack) => attack
        ? f.PayAndComplete(f.AddCard<StrikeSilent>(), f.Enemy)
        : f.PayAndComplete(f.AddCard<DefendSilent>(), null);
    private static Task Automatic(NativeRuntimeFixture f, bool attack)
    {
        CardModel card = attack ? f.AddCard<StrikeSilent>() : f.AddCard<DefendSilent>();
        return f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), new CardPlay
        {
            Card = card, Player = f.Player, IsAutoPlay = true, Target = attack ? f.Enemy : null,
            ResultPile = PileType.Discard, PlayIndex = 0, PlayCount = 1,
            Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
        });
    }

    private static int TipCount(SocratesQuestionCup cup) => ((IEnumerable)typeof(SocratesQuestionCup)
        .GetProperty("ExtraHoverTips", Instance)!.GetValue(cup)!).Cast<object>().Count();

    private static void InstallLoc()
    {
        var tables = new Dictionary<string, LocTable>();
        foreach (string name in new[] { "events", "relics" })
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "content", "STS2Philosophers", "localization", "eng", name + ".json");
            var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
            tables[name] = (LocTable)Activator.CreateInstance(typeof(LocTable), Instance, null,
                [name, translations, null], null)!;
        }
        var manager = (LocManager)RuntimeHelpers.GetUninitializedObject(typeof(LocManager));
        typeof(LocManager).GetMethod("LoadLocFormatters", Instance)!.Invoke(manager, null);
        Set(manager, "_tables", tables); Set(manager, "_engTables", tables);
        typeof(LocManager).GetProperty(nameof(LocManager.Instance), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(null, manager);
    }

    private static List<Action> SnapshotStaticState(Type type)
    {
        List<Action> restore = [];
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (field.IsLiteral) continue;
            object? value = field.GetValue(null);
            if (value is IDictionary dictionary)
            {
                List<DictionaryEntry> entries = [];
                IDictionaryEnumerator iterator = dictionary.GetEnumerator();
                while (iterator.MoveNext()) entries.Add(iterator.Entry);
                restore.Add(() =>
                {
                    if (!field.IsInitOnly) field.SetValue(null, value);
                    dictionary.Clear(); foreach (DictionaryEntry entry in entries) dictionary.Add(entry.Key, entry.Value);
                });
            }
            else if (value is IList list)
            {
                object?[] entries = list.Cast<object?>().ToArray();
                restore.Add(() =>
                {
                    if (!field.IsInitOnly) field.SetValue(null, value);
                    list.Clear(); foreach (object? entry in entries) list.Add(entry);
                });
            }
            else if (!field.IsInitOnly) restore.Add(() => field.SetValue(null, value));
        }
        return restore;
    }

    private static async Task CheckModelAndCallbacks()
    {
        EventModel canonical = ModelDb.Event<NewtonTwoSites>();
        Check((bool)Call("RouteEventMapBoundary", "IsRegisteredEvent", canonical),
            "Newton is registered for exact native event recovery.");
        EventRoom original = new(canonical);
        var serial = original.ToSerializable();
        Check(serial.RoomType == RoomType.Event && Equals(serial.EventId, canonical.Id) && !serial.IsPreFinished,
            "Newton serializes its actual native EventId and open room flag.");
        EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(serial, null!)!;
        Check(ReferenceEquals(restored.CanonicalEvent, canonical) && !restored.IsPreFinished
            && Equals(restored.ToSerializable().EventId, canonical.Id), "Native room restore preserves exact Newton identity.");
        EventModel model = canonical.ToMutable();
        var initial = (IReadOnlyList<EventOption>)typeof(NewtonTwoSites).GetMethod("GenerateInitialOptions", Instance)!.Invoke(model, null)!;
        Check(initial.Count == 1 && initial[0].IsLocked, "Newton initial choices stay locked before persistence.");
        MethodInfo generate = typeof(NewtonTwoSites).GetMethod("GenerateOptions", Instance)!;
        Func<Task>? Callback(EventOption option) => (Func<Task>?)typeof(EventOption).GetProperty("OnChosen", Instance)!.GetValue(option);
        object state = Get(Shared("MeasureDrop", "Invitation"), "NewtonTwoSites");
        var options = (IReadOnlyList<EventOption>)generate.Invoke(model, [state])!;
        Check(options.Count == 2 && Callback(options[0]) is not null, "Newton invitation supplies actual native callbacks.");
        Put(state, "InstanceId", "stale-instance"); Set(model, "_ready", true);
        string before = JsonSerializer.Serialize(state, state.GetType());
        await Callback(options[0])!();
        Check(JsonSerializer.Serialize(state, state.GetType()) == before
            && (bool)typeof(NewtonTwoSites).GetField("_ready", Instance)!.GetValue(model)!,
            "An obsolete invitation callback cannot change state or consume readiness.");
        object pending = Get(Shared("MeasureDrop", "Invitation"), "NewtonTwoSites");
        Check((bool)Call("NewtonTwoSitesFlow", "TryPrepare", pending, Enum.Parse(T("NewtonTwoSitesAction"), "Accept")),
            "A real prepared Newton choice exists for blocked recovery checks.");
        EventModel blocked = canonical.ToMutable();
        options = (IReadOnlyList<EventOption>)generate.Invoke(blocked, [pending])!;
        Set(blocked, "_blocked", true);
        before = JsonSerializer.Serialize(pending, pending.GetType());
        await Callback(options[0])!(); await blocked.AfterEventStarted();
        Check(JsonSerializer.Serialize(pending, pending.GetType()) == before,
            "Blocked native callbacks and startup preserve the pending Newton choice.");
    }

    private static void CheckNativeReceipt(NativeRuntimeFixture f, string expected)
    {
        var detached = (SocratesQuestionCup)RelicModel.FromSerializable(f.Relic.ToSerializable());
        Check((string)Get(detached, "NewtonPracticePayload") == expected,
            "Actual native relic SavedProperty serialization preserves the Newton receipt.");
    }

    private static async Task Positive(string first, string outcome)
    {
        object shared = Shared(first, outcome);
        object state = Get(shared, "NewtonTwoSites"), journey = Get(shared, "WesternJourney");
        Check((bool)Get(state, "Closed") && (bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!,
            "The actual completed Newton record survives the shared codec.");
        Check((string)Get(journey, "CurrentNodeId") == "ISAAC_NEWTON__KNOWLEDGE_AND_DOUBT__CORE"
            && ((IEnumerable<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_049"),
            "Newton adoption reaches the actual new node through its committed production edge.");
        var (f, run) = Fixture(shared);
        object?[] siteArgs = [null];
        Check((bool)typeof(SocratesQuestionCup).GetMethod("TryGetNewtonSite", Instance)!.Invoke(Cup(f), siteArgs)!
            && siteArgs[0]?.ToString() == Get(state, "CalibrationSite").ToString(), "The owned cup resolves the exact saved calibration site.");
        Check(TipCount(Cup(f)) > 0, "Native Newton hover-tip objects can be constructed from actual localization.");
        await Begin(f);
        bool attackFirst = siteArgs[0]?.ToString() == "Drop";
        CardPlay firstPlay = await Manual(f, attackFirst);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), firstPlay);
        string partial = Payload(f); CheckNativeReceipt(f, partial);
        RestorePayload(f, partial);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), firstPlay);
        await Manual(f, !attackFirst);
        int block = f.Player.Creature.Block;
        await EndTurn(f);
        Check(f.Player.Creature.Block == block + 6, $"{first}/{outcome} gives exactly 6 native Block after its calibrated manual pair.");
        string claimed = Payload(f); CheckNativeReceipt(f, claimed); RestorePayload(f, claimed);
        await EndTurn(f); await f.Relic.BeforeCombatStart(); await StartTurn(f); await EndTurn(f);
        Check(f.Player.Creature.Block == block + 6, "Duplicate turn-end and restored same-combat/turn hooks cannot pay twice.");
        f.PlayerState.IncrementTurnNumber(); await StartTurn(f); f.PlayerState.Energy = 40;
        await Manual(f, attackFirst); await Manual(f, !attackFirst); await EndTurn(f);
        Check(f.Player.Creature.Block == block + 12, "A genuine next turn earns its own calibrated reward.");
        Set(run.CurrentRoom!, "Id", 99); Set(run, "ActFloor", 5); await Begin(f);
        await Manual(f, attackFirst); await Manual(f, !attackFirst); await EndTurn(f);
        Check(f.Player.Creature.Block == block + 18, "An actual new native combat identity permits a fresh turn reward.");
    }

    private static async Task Negative(string label)
    {
        object shared = label switch
        {
            "open" => Shared("MeasureDrop", "ConditionRelation", close: false),
            "local" => Shared("MeasureDrop", "KeepLocal"),
            "declined" => Shared("MeasureDrop", "Decline"),
            _ => Shared("MeasureDrop", "ConditionRelation"),
        };
        var (f, _) = Fixture(shared); await Begin(f);
        if (label is "open" or "local" or "declined") Check(TipCount(Cup(f)) == 0, "Unfinished or nonadopted Newton choices do not expose a calibration tip.");
        if (label == "malformed") RestorePayload(f, "unknown Newton payload");
        int block = f.Player.Creature.Block;
        switch (label)
        {
            case "opposite": await Manual(f, false); await Manual(f, true); break;
            case "third": await Manual(f, true); await Manual(f, false); await Manual(f, true); break;
            case "automatic": await Automatic(f, true); await Automatic(f, false); break;
            case "mixed": await Manual(f, true); await Automatic(f, false); break;
            default: await Manual(f, true); await Manual(f, false); break;
        }
        await EndTurn(f);
        Check(f.Player.Creature.Block == block, $"Native {label} cannot fabricate Newton Block.");
        if (label == "malformed") Check(Payload(f) == "unknown Newton payload" && TipCount(Cup(f)) == 0,
            "Malformed native payload stays verbatim and suppresses the bonus and its tooltip.");
    }

    private static async Task Isolation()
    {
        var (first, firstRun) = Fixture(Shared("MeasureDrop", "ConditionRelation")); await Begin(first);
        CardPlay original = await Manual(first, true); string partial = Payload(first);
        object firstExecutor = RunManager.Instance.ActionExecutor;
        FieldInfo turnField = typeof(CombatManager).GetField("_turnState", Instance)!;
        object? firstTurn = turnField.GetValue(CombatManager.Instance);
        var (second, _) = Fixture(Shared("MeasureTide", "ConditionRelation")); await Begin(second);
        await Manual(second, false); string secondPartial = Payload(second);
        Check(!ReferenceEquals(first.Relic, second.Relic) && Payload(first) == partial,
            "Two actual ModelDb mutable cups keep separate saved calibration state.");
        Set(RunManager.Instance, "State", firstRun); Set(RunManager.Instance, "ActionExecutor", firstExecutor);
        Set(CombatManager.Instance, "_turnState", firstTurn);
        await first.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), original);
        await Manual(first, false); int block = first.Player.Creature.Block; await EndTurn(first);
        Check(first.Player.Creature.Block == block + 6 && Payload(second) == secondPartial,
            "A second mutable cup does not clear the first cup's CardPlay deduplication or share its claim.");
    }

    private static void CheckFreshNativeSelection()
    {
        const BindingFlags methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        foreach (Type type in Mod.GetTypes().Where(type => !type.IsAbstract && typeof(EventModel).IsAssignableFrom(type))) ModelDb.Inject(type);
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", methods)!.Invoke(null, [typeof(SocratesQuestionCup), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        object pool = tuple[3]!;
        var original = (EventModel)tuple[4]!;
        object shared = Shared("MeasureDrop", "Invitation");
        Put(shared, "NewtonTwoSites", null!);
        object behavior = Activator.CreateInstance(T("ActBehaviorState"), true)!;
        Put(behavior, "ActIndex", 2); Put(behavior, "CompletedCombatCount", 1);
        ((IDictionary)Get(shared, "ActBehaviorStates"))[2] = behavior;
        MapPointHistoryEntry predecessor = new() { MapPointType = MapPointType.Unknown,
            Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<DescartesMethodicReconstruction>() }] };
        MapPointHistoryEntry battle = new() { MapPointType = MapPointType.Monster,
            Rooms = [new() { RoomType = RoomType.Monster }] };
        List<List<MapPointHistoryEntry>> history = [new(), new() { predecessor }, new() { battle }];
        Set(run, "_mapPointHistory", history);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        string before = (string)Call("PhilosophyRunStateCodec", "Encode", shared);
        void Rejected(string reason) => Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original)
            && (string)Call("PhilosophyRunStateCodec", "Encode", shared) == before, reason);
        run.CurrentMapPoint!.PointType = MapPointType.Monster;
        Rejected("Newton cannot initialize on an actual non-Unknown map node.");
        run.CurrentMapPoint.PointType = MapPointType.Unknown;
        ((HashSet<ModelId>)run.VisitedEventIds).Add(ModelDb.GetId<NewtonTwoSites>());
        Rejected("Native visited Newton cannot create another person record or consume Fixed3.");
        ((HashSet<ModelId>)run.VisitedEventIds).Clear();
        history[1].Clear(); Rejected("A closed business record without actual act2 Descartes history cannot authorize Newton.");
        history[1].Add(predecessor);
        history[2].Clear(); Rejected("An aggregate combat count without an actual act3 battle cannot authorize Newton.");
        history[2].Add(battle);
        string rng = JsonSerializer.Serialize(run.Rng.ToSerializable());
        int queue = (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)!;
        EventModel chosen = run.Act.PullNextEvent(run);
        Check(chosen is NewtonTwoSites,
            $"Real native PullNextEvent must select Newton after act2 Descartes and a later act3 battle; got {chosen.GetType().Name}.");
        Check(run.VisitedEventIds.SetEquals([chosen.Id]) && !run.VisitedEventIds.Contains(original.Id)
            && JsonSerializer.Serialize(run.Rng.ToSerializable()) == rng
            && (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)! == queue,
            "Fresh Newton selection preserves native queue/RNG and visits exactly its returned identity.");
        object?[] currentArgs = [run, null];
        Check((bool)T("PhilosophyRunStateService").GetMethod("TryGet")!.Invoke(null, currentArgs)!, "Selected Newton keeps shared state bound.");
        shared = currentArgs[1]!;
        object state = Get(shared, "NewtonTwoSites"), journey = Get(shared, "WesternJourney");
        Check((bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!
            && Get(state, "Stage").ToString() == "Invitation" && (int)Get(journey, "LastFixedAct") == 2
            && !((IEnumerable<int>)Get(journey, "ResolvedQuestionActs")).Contains(3),
            "A fresh usable Newton invitation does not spend Fixed3 or a business Question3 slot before a choice.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", methods)!.Invoke(null, [run, fixture.Player, chosen, shared, 2]);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original), "Already visited native Newton cannot be selected again after room recovery.");
    }

    public static async Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        object priorExecutor = manager.ActionExecutor;
        bool priorShouldSave = manager.ShouldSave;
        object priorEventSynchronizer = manager.EventSynchronizer;
        object? priorNetService = manager.NetService;
        FieldInfo turnField = typeof(CombatManager).GetField("_turnState", Instance)!;
        object? priorTurn = turnField.GetValue(CombatManager.Instance);
        FieldInfo idsField = typeof(NetCombatCardDb).GetField("_idToCard", Instance)!;
        FieldInfo cardsField = typeof(NetCombatCardDb).GetField("_cardToId", Instance)!;
        object? priorIds = idsField.GetValue(NetCombatCardDb.Instance), priorCards = cardsField.GetValue(NetCombatCardDb.Instance);
        PropertyInfo locInstance = typeof(LocManager).GetProperty(nameof(LocManager.Instance), BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        object? priorLoc = locInstance.GetValue(null);
        FieldInfo formatter = typeof(LocManager).GetField("_smartFormatter", BindingFlags.NonPublic | BindingFlags.Static)!;
        PropertyInfo defaultFormatter = formatter.FieldType.Assembly.GetType("SmartFormat.Smart")!.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)!;
        object? priorFormatter = formatter.GetValue(null), priorDefault = defaultFormatter.GetValue(null);
        bool wasTest = TestMode.IsOn;
        List<Action> cacheRestore = SnapshotStaticState(typeof(ModelIdSerializationCache));
        List<Action> assemblyRestore = SnapshotStaticState(typeof(AssemblyInfo));
        try
        {
            TestMode.IsOn = true;
            foreach (Type type in new[] { typeof(SocratesQuestionCup), typeof(NewtonTwoSites), typeof(StrikeSilent), typeof(DefendSilent) }) ModelDb.Inject(type);
            AssemblyInfo.Init();
            AssemblyInfo.ModMap![Mod] = new Mod { path = "isolated_test_fixture",
                manifest = new ModManifest { id = "STS2Philosophers", affectsGameplay = true }, assemblies = [Mod] };
            ModelIdSerializationCache.ResetForTest(); ModelIdSerializationCache.Init(); InstallLoc();
            await CheckModelAndCallbacks();
            CheckFreshNativeSelection();
            foreach (string first in new[] { "MeasureDrop", "MeasureTide" })
                foreach (string outcome in new[] { "ConditionRelation", "RecheckOtherSite" }) await Positive(first, outcome);
            foreach (string failure in new[] { "opposite", "third", "automatic", "mixed", "open", "local", "declined", "malformed" }) await Negative(failure);
            await Isolation();
            Console.WriteLine("Newton native managed checks passed: exact EventRoom identity, locked/stale/pending callbacks, actual shared and SavedProperty roundtrips, both calibration sites, real 6 Block per qualifying turn, duplicate/automatic/third-card/bad-payload guards and mutable-cup isolation; no rendered UI, live player save or full playthrough.");
        }
        finally
        {
            foreach (Action restore in cacheRestore.Concat(assemblyRestore)) restore();
            Set(manager, "State", prior); Set(manager, "ActionExecutor", priorExecutor); Set(CombatManager.Instance, "_turnState", priorTurn);
            Set(manager, "ShouldSave", priorShouldSave); Set(manager, "EventSynchronizer", priorEventSynchronizer); Set(manager, "NetService", priorNetService);
            Set(NetCombatCardDb.Instance, "_idToCard", priorIds); Set(NetCombatCardDb.Instance, "_cardToId", priorCards);
            locInstance.SetValue(null, priorLoc); formatter.SetValue(null, priorFormatter); defaultFormatter.SetValue(null, priorDefault); TestMode.IsOn = wasTest;
        }
    }
}
