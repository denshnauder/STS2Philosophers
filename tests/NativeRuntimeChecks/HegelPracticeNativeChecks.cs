using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Runs the Hegel successor exercise on the real DescartesThinkingLens relic in a
// real combat room: manual cards, PlayerCmd block/draw, the saved payload and a
// detached reload. The event itself is built through the production flow, not a
// hand-set "adopted" boolean.
internal static class HegelPracticeNativeChecks
{
    private static readonly Assembly Mod = typeof(SocratesQuestionCup).Assembly;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Seed = "123456789";
    private static string RngSeed => new RunRngSet(Seed).Seed.ToString();
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
            && (bool)Call(flow, "TryCommitPending", [state, .. rest]), $"Native Hegel path commits {action}.");
    }

    private static object SpinozaState()
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["DESCARTES__SELF_AND_OTHER__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!,
            "Native Hegel has the actual reserved first question.");
        object state = Call("SpinozaCauseNetworkState", "Create", $"SPINOZA_CAUSE_NETWORK:{RngSeed}:2");
        foreach (string action in new[] { "Accept", "BlameEnemy", "Continue", "Intervene", "Leave" })
            Commit("SpinozaCauseNetworkFlow", "SpinozaCauseNetworkAction", state, action, journey, graph);
        return state;
    }

    private static object Shared(bool adopt, bool close = true, bool includeSpinoza = false)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["HERACLITUS__BEING_AND_CHANGE__CORE"])!;
        Put(journey, "CurrentNodeId", "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE");
        Put(journey, "LastFixedAct", 2);
        ((ICollection<string>)Get(journey, "CompletedEdgeIds")).Add("WESTERN_EDGE_024");
        object state = Call("HegelSubstanceSubjectState", "Create", $"HEGEL_SUBSTANCE_SUBJECT:{RngSeed}:3");
        if (adopt)
        {
            Commit("HegelSubstanceSubjectFlow", "HegelSubstanceSubjectAction", state, "InspectLedger", journey, graph);
            Commit("HegelSubstanceSubjectFlow", "HegelSubstanceSubjectAction", state, "AdoptSubject", journey, graph);
            if (close) Commit("HegelSubstanceSubjectFlow", "HegelSubstanceSubjectAction", state, "Leave", journey, graph);
        }
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
        Put(doctrine, "ThinkerId", "DESCARTES"); Put(doctrine, "DoctrineId", "DESCARTES_THINKING_LENS");
        Put(shared, "CurrentDoctrine", doctrine); Put(shared, "WesternJourney", journey);
        Put(shared, "HegelSubstanceSubject", state);
        if (includeSpinoza) Put(shared, "SpinozaCauseNetwork", SpinozaState());
        return Reload(shared);
    }

    private static (NativeRuntimeFixture F, RunState Run) Fixture(object shared)
    {
        Set(RunManager.Instance, "State", null);
        var f = new NativeRuntimeFixture(typeof(DescartesThinkingLens));
        f.AddEnemy(8);
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { f.Player }); Set(run, "_currentActIndex", 2);
        Set(run, "Rng", new RunRngSet(Seed)); Set(run, "ActFloor", 8);
        Set(run, "ExtraFields", new ExtraRunFields());
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(f.Player, "_runState", run); Set(f.Combat, "RunState", run); Set(f.Player, "Deck", new CardPile(PileType.Deck));
        var room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom)); Set(room, "Id", 98);
        Set(run, "_currentRooms", new List<AbstractRoom> { room }); Set(RunManager.Instance, "State", run);
        Set(RunManager.Instance, "NetService", new NetSingleplayerGameService());
        Call("PhilosophyRunStateService", "Restore", run, shared);
        ModelDb.Inject(typeof(DefendSilent));
        for (int i = 0; i < 8; i++) f.PlayerState.DrawPile.AddInternal(f.Combat.CreateCard<DefendSilent>(f.Player), silent: true);
        return (f, run);
    }

    private static DescartesThinkingLens Lens(NativeRuntimeFixture f) => (DescartesThinkingLens)f.Relic;
    private static string Payload(NativeRuntimeFixture f) => Lens(f).HegelPracticePayload;
    private static void RestorePayload(NativeRuntimeFixture f, string payload) => Put(Lens(f), "HegelPracticePayload", payload);
    private static async Task Begin(NativeRuntimeFixture f) { await f.Begin(); f.PlayerState.Energy = 40; }
    private static Task<CardPlay> Manual(NativeRuntimeFixture f, bool attack) => attack
        ? f.PayAndComplete(f.AddCard<StrikeSilent>(), f.Enemy)
        : f.PayAndComplete(f.AddCard<DefendSilent>(), null);
    private static int Block(NativeRuntimeFixture f) => f.Player.Creature.Block;
    private static decimal Weak(NativeRuntimeFixture f) =>
        f.Enemy.Powers.FirstOrDefault(p => p is WeakPower) is { } power ? Convert.ToDecimal(power.Amount) : 0m;

    private static async Task PlaySequence(NativeRuntimeFixture f)
    {
        foreach (bool attack in new[] { true, false, true, false })
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, attack));
    }

    private static async Task Reward()
    {
        var (f, _) = Fixture(Shared(adopt: true)); await Begin(f);
        int drawBefore = f.PlayerState.DrawPile.Cards.Count;
        foreach (bool attack in new[] { true, false, true })
            await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, attack));
        Check(Block(f) == 0m && Payload(f).Contains("\"Claimed\":true") == false,
            "The first three cards of the Hegel sequence pay nothing.");
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
        Check(Block(f) == 4m, "The fourth card grants 4 Block.");
        Check(f.PlayerState.DrawPile.Cards.Count == drawBefore - 1, "The fourth card draws 1.");
        Check(Payload(f).Contains("\"Claimed\":true"), "The reward is recorded in the saved payload.");
        Check(Lens(f).PracticePayload.Contains("Entry:"),
            "The lens's own Self-and-Other practice still records the same manual plays.");

        int drawAfter = f.PlayerState.DrawPile.Cards.Count;
        await PlaySequence(f);
        Check(Block(f) == 4m && f.PlayerState.DrawPile.Cards.Count == drawAfter,
            "A repeated sequence in the same combat pays nothing more.");
    }

    private static async Task SaveRecovery()
    {
        var (f, run) = Fixture(Shared(adopt: true)); await Begin(f);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
        string midPayload = Payload(f);
        var detached = (DescartesThinkingLens)RelicModel.FromSerializable(f.Relic.ToSerializable());
        Check(detached.HegelPracticePayload == midPayload && midPayload.Contains("Attack")
            && midPayload.Contains("Skill") && !midPayload.Contains("\"Claimed\":true"),
            $"A detached reload keeps the mid-sequence window without a claim (saved={midPayload}, reloaded={detached.HegelPracticePayload}).");

        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, false));
        string claimed = Payload(f);
        var claimedRelic = (DescartesThinkingLens)RelicModel.FromSerializable(f.Relic.ToSerializable());
        Check(claimedRelic.HegelPracticePayload == claimed && claimed.Contains("\"Claimed\":true"),
            "A detached reload keeps the spent claim for the same combat.");
        Set(run.CurrentRoom!, "Id", 99); Set(run, "ActFloor", 9); await Begin(f);
        int blockBefore = Block(f);
        await PlaySequence(f);
        Check(Block(f) == blockBefore + 4,
            $"A genuinely new combat permits one fresh Hegel reward (block {blockBefore}->{Block(f)}, payload={Payload(f)}).");

        var (restored, _) = Fixture(Shared(adopt: true)); await Begin(restored);
        RestorePayload(restored, midPayload);
        int drawBefore = restored.PlayerState.DrawPile.Cards.Count;
        await PlaySequence(restored);
        Check(Block(restored) == 4m && restored.PlayerState.DrawPile.Cards.Count == drawBefore - 1,
            "A restored mid-sequence payload still completes the four-card reward once.");
    }

    private static async Task Malformed()
    {
        var (f, _) = Fixture(Shared(adopt: true)); await Begin(f);
        RestorePayload(f, "unknown Hegel payload");
        await PlaySequence(f);
        Check(Block(f) == 0m && Payload(f) == "unknown Hegel payload",
            "A malformed payload stays verbatim and grants no reward.");
        Check(Lens(f).PracticePayload.Contains("Entry:"),
            "A malformed Hegel payload does not disable the lens's own base practice.");
    }

    private static async Task NoStacking()
    {
        var (f, _) = Fixture(Shared(adopt: true, includeSpinoza: true)); await Begin(f);
        await f.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), await Manual(f, true));
        Check(Weak(f) == 0m,
            "An adopted Hegel route never also pays Spinoza's first-attack Weak.");
        await PlaySequence(f);
        Check(Block(f) == 4m, "The Hegel reward is unaffected by the leftover Spinoza record.");
    }

    private static async Task Negatives()
    {
        var (kept, _) = Fixture(Shared(adopt: false)); await Begin(kept);
        await PlaySequence(kept);
        Check(Block(kept) == 0m, "A route that kept Spinoza cannot claim Hegel's practice.");

        var (open, _) = Fixture(Shared(adopt: true, close: false)); await Begin(open);
        await PlaySequence(open);
        Check(Block(open) == 0m, "An unclosed Hegel record cannot claim the practice.");
    }

    public static async Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        object? priorNetwork = manager.NetService;
        object priorSynchronizer = manager.EventSynchronizer, priorExecutor = manager.ActionExecutor;
        object? priorTurn = typeof(CombatManager).GetField("_turnState", Instance)!.GetValue(CombatManager.Instance);
        MethodInfo snapshot = typeof(ZenoCivicSealUpgradeNativeChecks)
            .GetMethod("SnapshotStaticState", BindingFlags.Static | BindingFlags.NonPublic)!;
        var cacheRestore = (List<Action>)snapshot.Invoke(null, [typeof(ModelIdSerializationCache)])!;
        var assemblyRestore = (List<Action>)snapshot.Invoke(null, [typeof(AssemblyInfo)])!;
        try
        {
            TestMode.IsOn = true;
            AssemblyInfo.Init();
            AssemblyInfo.ModMap![Mod] = new Mod { path = "isolated_test_fixture",
                manifest = new ModManifest { id = "STS2Philosophers", affectsGameplay = true }, assemblies = [Mod] };
            ModelDb.Inject(typeof(DescartesThinkingLens)); ModelDb.Inject(typeof(HegelSubstanceSubject));
            ModelIdSerializationCache.ResetForTest(); ModelIdSerializationCache.Init();
            ModelDb.Inject(typeof(DefendSilent)); ModelDb.Inject(typeof(StrikeSilent));
            await Reward();
            await SaveRecovery();
            await Malformed();
            await NoStacking();
            await Negatives();
            Console.WriteLine("Hegel practice native checks passed: real DescartesThinkingLens combat plays pay draw 1 and block 4 once for Attack/Skill/Attack/Skill, the saved payload survives a detached reload at both mid-sequence and claimed points, a new combat grants one fresh reward, malformed payloads freeze the practice without touching the base lens, and a kept or unclosed Hegel route cannot claim it or stack Spinoza's practice.");
        }
        finally
        {
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave); Set(manager, "NetService", priorNetwork);
            Set(manager, "EventSynchronizer", priorSynchronizer); Set(manager, "ActionExecutor", priorExecutor);
            Set(CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
            foreach (Action restore in assemblyRestore) restore();
            foreach (Action restore in cacheRestore) restore();
        }
    }

}
