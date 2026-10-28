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
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class SpinozaCauseNetworkNativeChecks
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
            && (bool)Call(flow, "TryCommitPending", [state, .. rest]), $"Native Spinoza path commits {action}.");
    }

    private static object Shared(string first, string outcome, bool close = true)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["DESCARTES__SELF_AND_OTHER__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!,
            "Native Spinoza has the actual reserved first question.");
        object state = Call("SpinozaCauseNetworkState", "Create", $"SPINOZA_CAUSE_NETWORK:{new RunRngSet("123456789").Seed}:2");
        string[] path = outcome == "Invitation" ? []
            : outcome == "Decline" ? [outcome]
            : ["Accept", first, "Continue", outcome];
        foreach (string action in path.Concat(close && outcome != "Invitation" ? ["Leave"] : Array.Empty<string>()))
            Commit("SpinozaCauseNetworkFlow", "SpinozaCauseNetworkAction", state, action, journey, graph);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
        Put(doctrine, "ThinkerId", "DESCARTES"); Put(doctrine, "DoctrineId", "DESCARTES_THINKING_LENS");
        Put(shared, "CurrentDoctrine", doctrine); Put(shared, "WesternJourney", journey);
        Put(shared, "SpinozaCauseNetwork", state);
        return Reload(shared);
    }

    private static (NativeRuntimeFixture F, RunState Run) Fixture(object shared)
    {
        Set(RunManager.Instance, "State", null);
        var f = new NativeRuntimeFixture(typeof(DescartesThinkingLens));
        f.AddEnemy(8);
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { f.Player }); Set(run, "_currentActIndex", 1);
        Set(run, "Rng", new RunRngSet("123456789")); Set(run, "ActFloor", 4);
        Set(run, "ExtraFields", new ExtraRunFields());
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(f.Player, "_runState", run); Set(f.Combat, "RunState", run); Set(f.Player, "Deck", new CardPile(PileType.Deck));
        var room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom)); Set(room, "Id", 98);
        Set(run, "_currentRooms", new List<AbstractRoom> { room }); Set(RunManager.Instance, "State", run);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        ModelDb.Inject(typeof(DefendSilent));
        for (int i = 0; i < 8; i++) f.PlayerState.DrawPile.AddInternal(f.Combat.CreateCard<DefendSilent>(f.Player), silent: true);
        return (f, run);
    }
    private static DescartesThinkingLens Lens(NativeRuntimeFixture f) => (DescartesThinkingLens)f.Relic;
    private static string Payload(NativeRuntimeFixture f) => Lens(f).SpinozaPracticePayload;
    private static void RestorePayload(NativeRuntimeFixture f, string payload) => Put(Lens(f), "SpinozaPracticePayload", payload);
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
    private static decimal Weak(NativeRuntimeFixture f) =>
        f.Enemy.Powers.FirstOrDefault(p => p is WeakPower) is { } power ? Convert.ToDecimal(power.Amount) : 0m;

    private static async Task CheckModelAndCallbacks()
    {
        EventModel canonical = ModelDb.Event<SpinozaCauseNetwork>();
        EventRoom original = new(canonical);
        var serial = original.ToSerializable();
        Check(serial.RoomType == RoomType.Event && Equals(serial.EventId, canonical.Id) && !serial.IsPreFinished,
            "Spinoza serializes its actual native EventId and open room flag.");
        EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(serial, null!)!;
        Check(ReferenceEquals(restored.CanonicalEvent, canonical) && !restored.IsPreFinished
            && Equals(restored.ToSerializable().EventId, canonical.Id), "Native room restore preserves exact Spinoza identity.");
        EventModel model = canonical.ToMutable();
        var initial = (IReadOnlyList<EventOption>)typeof(SpinozaCauseNetwork).GetMethod("GenerateInitialOptions", Instance)!.Invoke(model, null)!;
        Check(initial.Count == 1 && initial[0].IsLocked, "Spinoza initial choices stay locked before persistence.");
        MethodInfo generate = typeof(SpinozaCauseNetwork).GetMethod("GenerateOptions", Instance)!;
        Func<Task>? Callback(EventOption option) => (Func<Task>?)typeof(EventOption).GetProperty("OnChosen", Instance)!.GetValue(option);
        object state = Get(Shared("BlameEnemy", "Invitation"), "SpinozaCauseNetwork");
        var options = (IReadOnlyList<EventOption>)generate.Invoke(model, [state])!;
        Check(options.Count == 2 && Callback(options[0]) is not null, "Spinoza invitation supplies actual native callbacks.");
        Put(state, "InstanceId", "stale-instance"); Set(model, "_ready", true);
        string before = JsonSerializer.Serialize(state, state.GetType());
        await Callback(options[0])!(); await Callback(options[0])!();
        Check(JsonSerializer.Serialize(state, state.GetType()) == before
            && (bool)typeof(SpinozaCauseNetwork).GetField("_ready", Instance)!.GetValue(model)!,
            "An obsolete invitation callback cannot change state or consume readiness.");
        object obsolete = Get(Shared("BlameEnemy", "Invitation"), "SpinozaCauseNetwork");
        options = (IReadOnlyList<EventOption>)generate.Invoke(model, [obsolete])!;
        Put(obsolete, "Stage", Enum.Parse(T("SpinozaCauseNetworkStage"), "Attribution"));
        before = JsonSerializer.Serialize(obsolete, obsolete.GetType());
        await Callback(options[0])!(); await Callback(options[0])!();
        Check(JsonSerializer.Serialize(obsolete, obsolete.GetType()) == before,
            "A callback from an obsolete stage cannot change state.");
        object pending = Get(Shared("BlameEnemy", "Invitation"), "SpinozaCauseNetwork");
        Check((bool)Call("SpinozaCauseNetworkFlow", "TryPrepare", pending, Enum.Parse(T("SpinozaCauseNetworkAction"), "Accept")),
            "A real prepared Spinoza choice exists for blocked recovery checks.");
        EventModel blocked = canonical.ToMutable();
        options = (IReadOnlyList<EventOption>)generate.Invoke(blocked, [pending])!;
        Set(blocked, "_blocked", true);
        before = JsonSerializer.Serialize(pending, pending.GetType());
        await Callback(options[0])!(); await blocked.AfterEventStarted();
        Check(JsonSerializer.Serialize(pending, pending.GetType()) == before,
            "Blocked native callbacks and startup preserve the pending Spinoza choice.");
    }

    private static void CheckFreshNativeSelection()
    {
        const BindingFlags methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        foreach (Type type in Mod.GetTypes().Where(type => !type.IsAbstract && typeof(EventModel).IsAssignableFrom(type))) ModelDb.Inject(type);
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", methods)!.Invoke(null, [typeof(DescartesThinkingLens), 1])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        object pool = tuple[3]!;
        var original = (EventModel)tuple[4]!;
        object shared = Shared("BlameEnemy", "Invitation");
        Put(shared, "SpinozaCauseNetwork", null!);
        object behavior = Activator.CreateInstance(T("ActBehaviorState"), true)!;
        Put(behavior, "ActIndex", 1); Put(behavior, "CompletedCombatCount", 1);
        ((IDictionary)Get(shared, "ActBehaviorStates"))[1] = behavior;
        MapPointHistoryEntry battle = new() { MapPointType = MapPointType.Monster,
            Rooms = [new() { RoomType = RoomType.Monster }] };
        List<List<MapPointHistoryEntry>> history = [new(), new() { battle }];
        Set(run, "_mapPointHistory", history);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        string before = (string)Call("PhilosophyRunStateCodec", "Encode", shared);
        void Rejected(string reason) => Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original)
            && (string)Call("PhilosophyRunStateCodec", "Encode", shared) == before, reason);
        run.CurrentMapPoint!.PointType = MapPointType.Monster;
        Rejected("Spinoza cannot initialize on an actual non-Unknown map node.");
        run.CurrentMapPoint.PointType = MapPointType.Unknown;
        var relics = (List<RelicModel>)fixture.Player.Relics;
        var lens = relics.OfType<DescartesThinkingLens>().First();
        relics.Clear(); Rejected("Spinoza requires the actual owned DescartesThinkingLens carrier.");
        relics.Add(lens);
        history[1].Clear(); Rejected("An aggregate combat count without an actual act2 battle cannot authorize Spinoza.");
        history[1].Add(battle);
        object source = Get(shared, "WesternJourney");
        Put(source, "CurrentNodeId", "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        before = (string)Call("PhilosophyRunStateCodec", "Encode", shared);
        Rejected("The wrong actual source route cannot initialize Spinoza.");
        Put(source, "CurrentNodeId", "DESCARTES__SELF_AND_OTHER__CORE");
        before = (string)Call("PhilosophyRunStateCodec", "Encode", shared);
        object crossRun = Shared("BlameEnemy", "Invitation");
        Put(Get(crossRun, "SpinozaCauseNetwork"), "InstanceId", $"SPINOZA_CAUSE_NETWORK:{new RunRngSet("987654321").Seed}:2");
        Call("PhilosophyRunStateService", "Restore", run, crossRun);
        string crossBefore = (string)Call("PhilosophyRunStateCodec", "Encode", crossRun);
        Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original)
            && (string)Call("PhilosophyRunStateCodec", "Encode", crossRun) == crossBefore,
            "A cross-run saved Spinoza instance cannot be adopted.");
        Call("PhilosophyRunStateService", "Restore", run, shared);
        string rng = JsonSerializer.Serialize(run.Rng.ToSerializable());
        int queue = (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)!;
        EventModel chosen = run.Act.PullNextEvent(run);
        Check(chosen is SpinozaCauseNetwork,
            $"Real native PullNextEvent must select Spinoza after an act2 battle; got {chosen.GetType().Name}.");
        Check(run.VisitedEventIds.SetEquals([chosen.Id]) && !run.VisitedEventIds.Contains(original.Id)
            && JsonSerializer.Serialize(run.Rng.ToSerializable()) == rng
            && (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)! == queue,
            "Fresh Spinoza selection preserves native queue/RNG and visits exactly its returned identity.");
        object?[] currentArgs = [run, null];
        Check((bool)T("PhilosophyRunStateService").GetMethod("TryGet")!.Invoke(null, currentArgs)!, "Selected Spinoza keeps shared state bound.");
        shared = currentArgs[1]!;
        object state = Get(shared, "SpinozaCauseNetwork"), journey = Get(shared, "WesternJourney");
        Check((bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!
            && Get(state, "Stage").ToString() == "Invitation" && (int)Get(journey, "LastFixedAct") == 1,
            "A fresh usable Spinoza invitation does not spend Fixed2 before a choice.");
        typeof(NativeQuestionNodeChecks).GetMethod("CheckRoomRecovery", methods)!.Invoke(null, [run, fixture.Player, chosen, shared, 1]);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original), "Already visited native Spinoza cannot be selected again after room recovery.");
    }

    private static async Task Positive(string first, string outcome)
    {
        object shared = Shared(first, outcome);
        object state = Get(shared, "SpinozaCauseNetwork"), journey = Get(shared, "WesternJourney");
        Check((bool)Get(state, "Closed") && (bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!,
            "The actual completed Spinoza record survives the shared codec.");
        Check((string)Get(journey, "CurrentNodeId") == "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE"
            && ((IEnumerable<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_023")
            && ((IEnumerable<string>)Get(journey, "SeenThinkerIds")).Contains("BARUCH_SPINOZA"),
            "Spinoza adoption reaches the actual new node through its committed production edge.");
        var (f, run) = Fixture(shared);
        await Begin(f);
        if (outcome == "Intervene")
        {
            Creature other = f.Combat.Enemies.Single(enemy => enemy.CombatId == 8);
            CardPlay attack = await Manual(f, true);
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), attack);
            Check(Weak(f) == 1m, "Intervene applies exactly 1 Weak to the same living enemy.");
            Check(other.Powers.FirstOrDefault(p => p is WeakPower) is null, "An existing second enemy receives no Weak.");
            string partial = Payload(f);
            var detached = (DescartesThinkingLens)RelicModel.FromSerializable(f.Relic.ToSerializable());
            Check(detached.SpinozaPracticePayload == partial, "Actual native relic SavedProperty round-trips the Weak receipt.");
            RestorePayload(f, partial);
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), attack);
            Check(Weak(f) == 1m, "Repeated callback after saved receipt cannot add Weak.");
            await f.Relic.BeforeCombatStart();
            await Manual(f, true);
            Check(Weak(f) == 1m, "A recovery combat-start hook preserves the spent claim.");
            Set(run.CurrentRoom!, "Id", 99); Set(run, "ActFloor", 5); await Begin(f);
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
            Check(Weak(f) == 2m, "A genuine new combat permits one fresh Weak claim.");
        }
        else
        {
            int before = f.PlayerState.DrawPile.Cards.Count;
            CardPlay skill = await Manual(f, false);
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), skill);
            Check(f.PlayerState.DrawPile.Cards.Count == before - 1, "Reorder draws exactly 1 on the first manual Skill.");
            string partial = Payload(f);
            var detached = (DescartesThinkingLens)RelicModel.FromSerializable(f.Relic.ToSerializable());
            Check(detached.SpinozaPracticePayload == partial, "Actual native relic SavedProperty round-trips the draw receipt.");
            RestorePayload(f, partial);
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), skill);
            Check(f.PlayerState.DrawPile.Cards.Count == before - 1, "Repeated callback after saved receipt cannot draw again.");
            await f.Relic.BeforeCombatStart();
            await Manual(f, false);
            Check(f.PlayerState.DrawPile.Cards.Count == before - 1, "A recovery combat-start hook preserves the spent claim.");
            Set(run.CurrentRoom!, "Id", 99); Set(run, "ActFloor", 5); await Begin(f);
            before = f.PlayerState.DrawPile.Cards.Count;
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
            Check(f.PlayerState.DrawPile.Cards.Count == before - 1, "A genuine new combat permits one fresh draw claim.");
        }
    }

    private static async Task Negative(string failure)
    {
        object shared = failure switch
        {
            "open" => Shared("BlameEnemy", "Intervene", close: false),
            "corrected" => Shared("BlameEnemy", "CorrectRecord"),
            "declined" => Shared("BlameEnemy", "Decline"),
            "priorAttack" or "automatic" => Shared("BlameEnemy", "Reorder"),
            _ => Shared("BlameEnemy", "Intervene"),
        };
        var (f, _) = Fixture(shared); await Begin(f);
        if (failure == "malformed") RestorePayload(f, "unknown Spinoza payload");
        int before = f.PlayerState.DrawPile.Cards.Count;
        switch (failure)
        {
            case "priorAttack":
                await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
                await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
                Check(f.PlayerState.DrawPile.Cards.Count == before, "A prior manual Attack blocks the Reorder draw.");
                break;
            case "automatic":
                await Automatic(f, true); await Automatic(f, false);
                await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
                Check(f.PlayerState.DrawPile.Cards.Count == before - 1, "Automatic plays are ignored and the manual Skill still draws 1.");
                break;
            case "dead":
                Set(f.Enemy, "_currentHp", 0);
                Creature other = f.Combat.Enemies.Single(enemy => enemy.CombatId == 8);
                await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
                Check(other.Powers.FirstOrDefault(p => p is WeakPower) is null, "A dead target yields no Weak to another enemy.");
                await f.PayAndComplete(f.AddCard<StrikeSilent>(), other);
                Check(other.Powers.FirstOrDefault(p => p is WeakPower) is null, "The consumed claim does not transfer to a later real target.");
                break;
            default:
                await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
                await Manual(f, false);
                Check(Weak(f) == 0m && f.PlayerState.DrawPile.Cards.Count == before,
                    $"Native {failure} cannot fabricate Spinoza Weak or Draw.");
                if (failure == "malformed")
                {
                    var detached = (DescartesThinkingLens)RelicModel.FromSerializable(f.Relic.ToSerializable());
                    Check(detached.SpinozaPracticePayload == "unknown Spinoza payload" && Payload(f) == "unknown Spinoza payload",
                        "Malformed native payload stays verbatim and grants no reward.");
                }
                break;
        }
    }

    private static async Task Isolation()
    {
        Check(ModelDb.Relic<DescartesThinkingLens>().SpinozaPracticePayload == string.Empty,
            "The canonical lens does not allocate a mutable Spinoza receipt.");
        var (first, firstRun) = Fixture(Shared("BlameEnemy", "Intervene")); await Begin(first);
        CardPlay original = await Manual(first, true);
        await first.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), original);
        string firstPayload = Payload(first);
        object firstExecutor = RunManager.Instance.ActionExecutor;
        FieldInfo turnField = typeof(CombatManager).GetField("_turnState", Instance)!;
        object? firstTurn = turnField.GetValue(CombatManager.Instance);
        var (second, _) = Fixture(Shared("TraceChains", "Reorder")); await Begin(second);
        await second.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(second, false));
        string secondPayload = Payload(second);
        Check(!ReferenceEquals(first.Relic, second.Relic) && firstPayload != secondPayload && Payload(first) == firstPayload,
            "Two actual ModelDb mutable lenses keep separate saved Spinoza state.");
        Set(RunManager.Instance, "State", firstRun); Set(RunManager.Instance, "ActionExecutor", firstExecutor);
        Set(CombatManager.Instance, "_turnState", firstTurn);
        await first.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), original);
        Check(Weak(first) == 1m && Payload(second) == secondPayload,
            "A second mutable lens does not clear the first lens's deduplication or share its claim.");
        Check(ModelDb.Relic<DescartesThinkingLens>().SpinozaPracticePayload == string.Empty,
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
            foreach (Type type in new[] { typeof(DescartesThinkingLens), typeof(SpinozaCauseNetwork), typeof(StrikeSilent), typeof(DefendSilent), typeof(WeakPower) }) ModelDb.Inject(type);
            AssemblyInfo.Init();
            AssemblyInfo.ModMap![Mod] = new Mod { path = "isolated_test_fixture",
                manifest = new ModManifest { id = "STS2Philosophers", affectsGameplay = true }, assemblies = [Mod] };
            ModelIdSerializationCache.ResetForTest(); ModelIdSerializationCache.Init(); InstallLoc();
            await CheckModelAndCallbacks();
            CheckFreshNativeSelection();
            foreach (string first in new[] { "BlameEnemy", "TraceChains" })
                foreach (string outcome in new[] { "Intervene", "Reorder" }) await Positive(first, outcome);
            foreach (string failure in new[] { "priorAttack", "automatic", "dead", "open", "corrected", "declined", "malformed" }) await Negative(failure);
            await Isolation();
            Console.WriteLine("Spinoza native managed checks passed: real Unknown selection and event-room restoration, stale/duplicate guards, actual Weak and draw, saved receipts and mutable lens isolation; no rendered UI, live player save or playthrough.");
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
