using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class LeibnizIndependentClocksNativeChecks
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
            && (bool)Call(flow, "TryCommitPending", [state, .. rest]), $"Native Leibniz path commits {action}.");
    }

    private static string SpinozaId => $"SPINOZA_CAUSE_NETWORK:{new RunRngSet("123456789").Seed}:2";
    private static string LeibnizId => $"LEIBNIZ_INDEPENDENT_CLOCKS:{new RunRngSet("123456789").Seed}:3";

    private static object Shared(string probe, string ending, bool close = true)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["DESCARTES__SELF_AND_OTHER__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!,
            "Native Leibniz has the actual reserved first question.");
        object spinoza = Call("SpinozaCauseNetworkState", "Create", SpinozaId);
        foreach (string action in new[] { "Accept", "TraceChains", "Continue", "Reorder", "Leave" })
            Commit("SpinozaCauseNetworkFlow", "SpinozaCauseNetworkAction", spinoza, action, journey, graph);
        object state = Call("LeibnizHarmonyState", "Create", LeibnizId, Get(spinoza, "InstanceId"));
        string[] path = ending == "Invitation" ? []
            : ending == "Decline" ? [ending]
            : ["Accept", probe, "Continue", ending];
        foreach (string action in path.Concat(close && ending != "Invitation" ? ["Leave"] : Array.Empty<string>()))
            Commit("LeibnizHarmonyFlow", "LeibnizHarmonyAction", state, action, journey, spinoza, graph);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
        Put(doctrine, "ThinkerId", "DESCARTES"); Put(doctrine, "DoctrineId", "DESCARTES_THINKING_LENS");
        Put(shared, "CurrentDoctrine", doctrine); Put(shared, "WesternJourney", journey);
        Put(shared, "SpinozaCauseNetwork", spinoza); Put(shared, "LeibnizHarmony", state);
        return Reload(shared);
    }

    private static (NativeRuntimeFixture F, RunState Run) Fixture(object shared)
    {
        Set(RunManager.Instance, "State", null);
        Set(RunManager.Instance, "NetService", new NetSingleplayerGameService());
        var f = new NativeRuntimeFixture(typeof(DescartesThinkingLens));
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { f.Player }); Set(run, "_currentActIndex", 2);
        Set(run, "Rng", new RunRngSet("123456789")); Set(run, "ActFloor", 4);
        Set(run, "ExtraFields", new ExtraRunFields());
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(f.Player, "_runState", run); Set(f.Combat, "RunState", run); Set(f.Player, "Deck", new CardPile(PileType.Deck));
        var room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom)); Set(room, "Id", 98);
        Set(run, "_currentRooms", new List<AbstractRoom> { room }); Set(RunManager.Instance, "State", run);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        ModelDb.Inject(typeof(DefendSilent));
        for (int i = 0; i < 10; i++) f.PlayerState.DrawPile.AddInternal(f.Combat.CreateCard<DefendSilent>(f.Player), silent: true);
        return (f, run);
    }
    private static DescartesThinkingLens Lens(NativeRuntimeFixture f) => (DescartesThinkingLens)f.Relic;
    private static string DefaultPayload => (string)T("LeibnizCombatPracticeState").GetMethod("Encode")!
        .Invoke(Call("LeibnizCombatPracticeState", "CreateDefault"), null)!;
    private static string Payload(NativeRuntimeFixture f) => (string)Get(Lens(f), "LeibnizPracticePayload");
    private static void RestorePayload(NativeRuntimeFixture f, string payload) => Put(Lens(f), "LeibnizPracticePayload", payload);
    private static async Task Begin(NativeRuntimeFixture f) { await f.Begin(); f.PlayerState.Energy = 40; }
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
    private static int Block(NativeRuntimeFixture f) => (int)Get(f.Player.Creature, "Block");

    private static async Task CheckModelAndCallbacks()
    {
        EventModel canonical = ModelDb.Event<LeibnizIndependentClocks>();
        EventRoom original = new(canonical);
        var serial = original.ToSerializable();
        Check(serial.RoomType == RoomType.Event && Equals(serial.EventId, canonical.Id) && !serial.IsPreFinished,
            "Leibniz serializes its actual native EventId and open room flag.");
        EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(serial, null!)!;
        Check(ReferenceEquals(restored.CanonicalEvent, canonical) && !restored.IsPreFinished
            && Equals(restored.ToSerializable().EventId, canonical.Id), "Native room restore preserves exact Leibniz identity.");
        EventModel model = canonical.ToMutable();
        var initial = (IReadOnlyList<EventOption>)typeof(LeibnizIndependentClocks).GetMethod("GenerateInitialOptions", Instance)!.Invoke(model, null)!;
        Check(initial.Count == 1 && initial[0].IsLocked, "Leibniz initial choices stay locked before persistence.");
        MethodInfo generate = typeof(LeibnizIndependentClocks).GetMethod("GenerateOptions", Instance)!;
        Func<Task>? Callback(EventOption option) => (Func<Task>?)typeof(EventOption).GetProperty("OnChosen", Instance)!.GetValue(option);
        object state = Get(Shared("DelayOne", "Invitation"), "LeibnizHarmony");
        var options = (IReadOnlyList<EventOption>)generate.Invoke(model, [state])!;
        Check(options.Count == 2 && Callback(options[0]) is not null, "Leibniz invitation supplies actual native callbacks.");
        Put(state, "InstanceId", "stale-instance"); Set(model, "_ready", true);
        string before = JsonSerializer.Serialize(state, state.GetType());
        await Callback(options[0])!(); await Callback(options[0])!();
        Check(JsonSerializer.Serialize(state, state.GetType()) == before
            && (bool)typeof(LeibnizIndependentClocks).GetField("_ready", Instance)!.GetValue(model)!,
            "An obsolete invitation callback cannot change state or consume readiness.");
        object obsolete = Get(Shared("DelayOne", "Invitation"), "LeibnizHarmony");
        options = (IReadOnlyList<EventOption>)generate.Invoke(model, [obsolete])!;
        Put(obsolete, "Stage", Enum.Parse(T("LeibnizHarmonyStage"), "ClockChoice"));
        before = JsonSerializer.Serialize(obsolete, obsolete.GetType());
        await Callback(options[0])!(); await Callback(options[0])!();
        Check(JsonSerializer.Serialize(obsolete, obsolete.GetType()) == before,
            "A callback from an obsolete stage cannot change state.");
        object pending = Get(Shared("DelayOne", "Invitation"), "LeibnizHarmony");
        Check((bool)Call("LeibnizHarmonyFlow", "TryPrepare", pending, Enum.Parse(T("LeibnizHarmonyAction"), "Accept")),
            "A real prepared Leibniz choice exists for blocked recovery checks.");
        EventModel blocked = canonical.ToMutable();
        options = (IReadOnlyList<EventOption>)generate.Invoke(blocked, [pending])!;
        Set(blocked, "_blocked", true);
        before = JsonSerializer.Serialize(pending, pending.GetType());
        await Callback(options[0])!(); await blocked.AfterEventStarted();
        Check(JsonSerializer.Serialize(pending, pending.GetType()) == before,
            "Blocked native callbacks and startup preserve the pending Leibniz choice.");
    }

    private static void CheckFreshNativeSelection()
    {
        const BindingFlags methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        foreach (Type type in Mod.GetTypes().Where(type => !type.IsAbstract && typeof(EventModel).IsAssignableFrom(type))) ModelDb.Inject(type);
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", methods)!.Invoke(null, [typeof(DescartesThinkingLens), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        object pool = tuple[3]!;
        var original = (EventModel)tuple[4]!;
        object shared = Shared("DelayOne", "Invitation");
        Put(shared, "LeibnizHarmony", null!);
        object behavior = Activator.CreateInstance(T("ActBehaviorState"), true)!;
        Put(behavior, "ActIndex", 2); Put(behavior, "CompletedCombatCount", 1);
        ((IDictionary)Get(shared, "ActBehaviorStates"))[2] = behavior;
        MapPointHistoryEntry spinoza = new() { MapPointType = MapPointType.Unknown,
            Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<SpinozaCauseNetwork>() }] };
        MapPointHistoryEntry battle = new() { MapPointType = MapPointType.Monster,
            Rooms = [new() { RoomType = RoomType.Monster }] };
        List<List<MapPointHistoryEntry>> history = [new(), new() { spinoza }, new() { battle }];
        Set(run, "_mapPointHistory", history);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        string before = (string)Call("PhilosophyRunStateCodec", "Encode", shared);
        void Rejected(string reason) => Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original)
            && (string)Call("PhilosophyRunStateCodec", "Encode", shared) == before, reason);
        run.CurrentMapPoint!.PointType = MapPointType.Monster;
        Rejected("Leibniz cannot initialize on an actual non-Unknown map node.");
        run.CurrentMapPoint.PointType = MapPointType.Unknown;
        var relics = (List<RelicModel>)fixture.Player.Relics;
        var lens = relics.OfType<DescartesThinkingLens>().First();
        relics.Clear(); Rejected("Leibniz requires the actual owned DescartesThinkingLens carrier.");
        relics.Add(lens);
        history[2].Clear(); Rejected("An aggregate combat count without an actual act3 battle cannot authorize Leibniz.");
        history[2].Add(battle);
        history[1].Clear(); Rejected("A missing actual Spinoza history cannot authorize Leibniz.");
        history[1].Add(spinoza);
        object source = Get(shared, "WesternJourney");
        Put(source, "CurrentNodeId", "DESCARTES__SELF_AND_OTHER__CORE");
        before = (string)Call("PhilosophyRunStateCodec", "Encode", shared);
        Rejected("The wrong actual source route cannot initialize Leibniz.");
        Put(source, "CurrentNodeId", "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE");
        before = (string)Call("PhilosophyRunStateCodec", "Encode", shared);
        object crossRun = Reload(shared);
        Put(crossRun, "LeibnizHarmony", Call("LeibnizHarmonyState", "Create",
            $"LEIBNIZ_INDEPENDENT_CLOCKS:{new RunRngSet("987654321").Seed}:3",
            Get(Get(crossRun, "SpinozaCauseNetwork"), "InstanceId")));
        Call("PhilosophyRunStateService", "Restore", run, crossRun);
        string crossBefore = (string)Call("PhilosophyRunStateCodec", "Encode", crossRun);
        Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original)
            && (string)Call("PhilosophyRunStateCodec", "Encode", crossRun) == crossBefore,
            "A cross-run saved Leibniz instance cannot be adopted.");
        Call("PhilosophyRunStateService", "Restore", run, shared);
        string rng = JsonSerializer.Serialize(run.Rng.ToSerializable());
        int queue = (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)!;
        EventModel chosen = run.Act.PullNextEvent(run);
        Check(chosen is LeibnizIndependentClocks,
            $"Real native PullNextEvent must select Leibniz after an act3 battle; got {chosen.GetType().Name}.");
        Check(run.VisitedEventIds.SetEquals([chosen.Id]) && !run.VisitedEventIds.Contains(original.Id)
            && JsonSerializer.Serialize(run.Rng.ToSerializable()) == rng
            && (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)! == queue,
            "Fresh Leibniz selection preserves native queue/RNG and visits exactly its returned identity.");
        object?[] currentArgs = [run, null];
        Check((bool)T("PhilosophyRunStateService").GetMethod("TryGet")!.Invoke(null, currentArgs)!, "Selected Leibniz keeps shared state bound.");
        shared = currentArgs[1]!;
        object state = Get(shared, "LeibnizHarmony"), journey = Get(shared, "WesternJourney");
        Check((bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!
            && Get(state, "Stage").ToString() == "Invitation" && (int)Get(journey, "LastFixedAct") == 2,
            "A fresh usable Leibniz invitation does not spend Fixed3 before a choice.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", methods)!.Invoke(null, [run, fixture.Player, chosen, shared, 2]);
        object openResult = Shared("DelayOne", "Independent", close: false);
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", methods)!.Invoke(null, [run, fixture.Player, chosen, openResult, 2]);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original), "Already visited native Leibniz cannot be selected again after room recovery.");
    }

    private static async Task CheckIndependent()
    {
        object shared = Shared("DelayOne", "Independent");
        object state = Get(shared, "LeibnizHarmony"), journey = Get(shared, "WesternJourney");
        Check((bool)Get(state, "Closed") && (bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!,
            "The actual completed Leibniz record survives the shared codec.");
        Check((string)Get(journey, "CurrentNodeId") == "GOTTFRIED_WILHELM_LEIBNIZ__BEING_AND_CHANGE__HARMONY"
            && ((IEnumerable<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_050")
            && ((IEnumerable<string>)Get(journey, "SeenThinkerIds")).Contains("GOTTFRIED_WILHELM_LEIBNIZ"),
            "Leibniz adoption reaches the actual new node through its committed production edge.");
        var (f, run) = Fixture(shared);
        await Begin(f);
        int before = f.PlayerState.DrawPile.Cards.Count;
        await Automatic(f, true);
        await Automatic(f, false);
        Check(f.PlayerState.DrawPile.Cards.Count == before && Block(f) == 0,
            "Automatic plays grant no Leibniz draw or Block.");
        CardPlay firstSkill = await Manual(f, false);
        Check(f.PlayerState.DrawPile.Cards.Count == before, "The first manual Skill draws nothing.");
        CardPlay secondSkill = await Manual(f, false);
        Check(f.PlayerState.DrawPile.Cards.Count == before - 1, "The second manual Skill draws exactly 1.");
        string partial = Payload(f);
        var detached = (DescartesThinkingLens)RelicModel.FromSerializable(f.Relic.ToSerializable());
        Check((string)Get(detached, "LeibnizPracticePayload") == partial, "Actual native relic SavedProperty round-trips the draw receipt.");
        RestorePayload(f, partial);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), secondSkill);
        Check(f.PlayerState.DrawPile.Cards.Count == before - 1, "Repeated callback after saved receipt cannot draw again.");
        await f.Relic.BeforeCombatStart();
        await Manual(f, false);
        Check(f.PlayerState.DrawPile.Cards.Count == before - 1, "A recovery combat-start hook preserves the spent claim.");
        int blockBefore = Block(f);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
        Check(Block(f) == blockBefore, "The first manual Attack grants no Block.");
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
        Check(Block(f) == blockBefore + 4, "The second manual Attack grants exactly 4 Block.");
        Set(run.CurrentRoom!, "Id", 99); Set(run, "ActFloor", 5); await Begin(f);
        before = f.PlayerState.DrawPile.Cards.Count;
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
        Check(f.PlayerState.DrawPile.Cards.Count == before - 1, "A genuine new combat permits one fresh draw claim.");
    }

    private static async Task CheckCoordinated()
    {
        object shared = Shared("ObserveBoth", "Coordinated");
        var (f, _) = Fixture(shared);
        await Begin(f);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
        int energy = f.PlayerState.Energy;
        await f.NextTurn();
        Check(f.PlayerState.Energy == energy + 1, "The coordinated first turn grants exactly 1 Energy at turn two.");
        string partial = Payload(f);
        var detached = (DescartesThinkingLens)RelicModel.FromSerializable(f.Relic.ToSerializable());
        Check((string)Get(detached, "LeibnizPracticePayload") == partial, "Actual native relic SavedProperty round-trips the Energy receipt.");
        RestorePayload(f, partial);
        await f.StartTurn();
        Check(f.PlayerState.Energy == energy + 1, "A duplicate turn-start hook after receipt restore cannot add Energy again.");

        var (missing, _) = Fixture(Shared("ObserveBoth", "Coordinated"));
        await Begin(missing);
        await missing.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(missing, true));
        int missingEnergy = missing.PlayerState.Energy;
        await missing.NextTurn();
        Check(missing.PlayerState.Energy == missingEnergy, "A first turn missing a Skill grants no Energy at turn two.");
        await missing.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(missing, false));
        int afterLateSkill = missing.PlayerState.Energy;
        await missing.NextTurn();
        Check(missing.PlayerState.Energy == afterLateSkill, "A later Skill cannot retroactively grant Energy.");
    }

    private static async Task CheckGuardsAndIsolation()
    {
        Check((string)Get(ModelDb.Relic<DescartesThinkingLens>(), "LeibnizPracticePayload") == DefaultPayload
            && typeof(DescartesThinkingLens).GetField("_leibnizPractice", Instance)!
                .GetValue(ModelDb.Relic<DescartesThinkingLens>()) is null,
            "The canonical lens does not allocate a mutable Leibniz receipt.");
        foreach (string ending in new[] { "KeepSpinoza", "Decline", "Invitation" })
        {
            var (f, _) = Fixture(Shared("DelayOne", ending, close: ending != "Invitation"));
            await Begin(f);
            int before = f.PlayerState.DrawPile.Cards.Count;
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
            Check(f.PlayerState.DrawPile.Cards.Count == before - 1 && Payload(f) == DefaultPayload,
                $"Native {ending} keeps the old Spinoza draw and grants no Leibniz receipt.");
        }

        var (corrupt, _) = Fixture(Shared("DelayOne", "Independent"));
        await Begin(corrupt);
        RestorePayload(corrupt, "unknown Leibniz payload");
        int corruptBefore = corrupt.PlayerState.DrawPile.Cards.Count;
        await corrupt.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(corrupt, false));
        await corrupt.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(corrupt, false));
        Check(corrupt.PlayerState.DrawPile.Cards.Count == corruptBefore && Payload(corrupt) == "unknown Leibniz payload",
            "Malformed native payload stays verbatim and grants no reward.");
        RelicModel restoredBad = RelicModel.FromSerializable(corrupt.Relic.ToSerializable());
        Check((string)Get(restoredBad, "LeibnizPracticePayload") == "unknown Leibniz payload",
            "Actual native SavedProperty serialization preserves malformed payload verbatim.");

        var (first, firstRun) = Fixture(Shared("DelayOne", "Independent")); await Begin(first);
        await first.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(first, false));
        await first.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(first, false));
        string firstPayload = Payload(first);
        object firstExecutor = RunManager.Instance.ActionExecutor;
        FieldInfo turnField = typeof(CombatManager).GetField("_turnState", Instance)!;
        object? firstTurn = turnField.GetValue(CombatManager.Instance);
        var (second, _) = Fixture(Shared("ObserveBoth", "Coordinated")); await Begin(second);
        await second.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(second, true));
        await second.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(second, false));
        await second.NextTurn();
        string secondPayload = Payload(second);
        Check(!ReferenceEquals(first.Relic, second.Relic) && firstPayload != secondPayload && Payload(first) == firstPayload,
            "Two actual ModelDb mutable lenses keep separate saved Leibniz state.");
        Set(RunManager.Instance, "State", firstRun); Set(RunManager.Instance, "ActionExecutor", firstExecutor);
        Set(CombatManager.Instance, "_turnState", firstTurn);
        int firstBefore = first.PlayerState.DrawPile.Cards.Count;
        await first.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(first, false));
        Check(first.PlayerState.DrawPile.Cards.Count == firstBefore && Payload(second) == secondPayload,
            "A second mutable lens does not clear the first lens's deduplication or share its claim.");
        Check((string)Get(ModelDb.Relic<DescartesThinkingLens>(), "LeibnizPracticePayload") == DefaultPayload
            && typeof(DescartesThinkingLens).GetField("_leibnizPractice", Instance)!
                .GetValue(ModelDb.Relic<DescartesThinkingLens>()) is null,
            "Mutable lens actions leave the canonical receipt empty.");
    }

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
            foreach (Type type in new[] { typeof(DescartesThinkingLens), typeof(LeibnizIndependentClocks), typeof(SpinozaCauseNetwork), typeof(StrikeSilent), typeof(DefendSilent), typeof(WeakPower) }) ModelDb.Inject(type);
            AssemblyInfo.Init();
            AssemblyInfo.ModMap![Mod] = new Mod { path = "isolated_test_fixture",
                manifest = new ModManifest { id = "STS2Philosophers", affectsGameplay = true }, assemblies = [Mod] };
            ModelIdSerializationCache.ResetForTest(); ModelIdSerializationCache.Init(); InstallLoc();
            await CheckModelAndCallbacks();
            CheckFreshNativeSelection();
            await CheckIndependent();
            await CheckCoordinated();
            await CheckGuardsAndIsolation();
            Console.WriteLine("Leibniz native managed checks passed: real Unknown selection and EventRoom recovery, actual Block/draw/Energy, restored receipts and mutable lens isolation; no rendered UI, live player save or playthrough.");
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
