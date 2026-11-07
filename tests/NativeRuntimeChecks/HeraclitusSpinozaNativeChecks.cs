using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

// The natural-source Spinoza practice must pay on the new Lyre carrier, not
// only on DescartesThinkingLens: real Spinoza flow, real combat, native Weak/Draw.
internal static class HeraclitusSpinozaNativeChecks
{
    private static readonly Assembly Mod = typeof(SocratesQuestionCup).Assembly;
    private static readonly BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Instance | BindingFlags.Static;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) => T(type)
        .GetMethods(Methods).Single(m => m.Name == method && m.GetParameters().Length == args.Length)
        .Invoke(null, args)!;
    private static void Put(object obj, string name, object value) =>
        obj.GetType().GetProperty(name)!.SetValue(obj, value);
    private static object Get(object obj, string name) => obj.GetType().GetProperty(name)!.GetValue(obj)!;
    private static void Commit(string flow, string actionEnum, object state, string action, params object[] rest)
    {
        object value = Enum.Parse(T(actionEnum), action);
        Check((bool)Call(flow, "TryPrepare", state, value)
            && (bool)Call(flow, "TryCommitPending", [state, .. rest]), $"Spinoza flow commits {action}.");
    }

    private static object SpinozaState(bool reorder)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["DESCARTES__SELF_AND_OTHER__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!,
            "The Spinoza flow has its reserved question.");
        object state = Call("SpinozaCauseNetworkState", "Create", "SPINOZA_CAUSE_NETWORK:TEST:2");
        Commit("SpinozaCauseNetworkFlow", "SpinozaCauseNetworkAction", state, "Accept", journey, graph);
        Commit("SpinozaCauseNetworkFlow", "SpinozaCauseNetworkAction", state,
            reorder ? "TraceChains" : "BlameEnemy", journey, graph);
        Commit("SpinozaCauseNetworkFlow", "SpinozaCauseNetworkAction", state, "Continue", journey, graph);
        Commit("SpinozaCauseNetworkFlow", "SpinozaCauseNetworkAction", state,
            reorder ? "Reorder" : "Intervene", journey, graph);
        Commit("SpinozaCauseNetworkFlow", "SpinozaCauseNetworkAction", state, "Leave", journey, graph);
        return state;
    }

    private static (NativeRuntimeFixture F, RunState Run) Fixture(object spinoza)
    {
        Set(RunManager.Instance, "State", null);
        var f = new NativeRuntimeFixture(typeof(HeraclitusLyre));
        f.AddEnemy(8);
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["HERACLITUS__BEING_AND_CHANGE__CORE"])!;
        Put(journey, "CurrentNodeId", "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE");
        Put(journey, "LastFixedAct", 2);
        ((ICollection<string>)Get(journey, "CompletedEdgeIds")).Add("WESTERN_EDGE_023");
        ((ICollection<string>)Get(journey, "SeenThinkerIds")).Add("BARUCH_SPINOZA");
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
        Put(doctrine, "ThinkerId", "HERACLITUS");
        Put(doctrine, "DoctrineId", "HERACLITUS_LYRE");
        Put(shared, "CurrentDoctrine", doctrine);
        Put(shared, "WesternJourney", journey);
        Put(shared, "SpinozaCauseNetwork", spinoza);
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { f.Player });
        Set(run, "_currentActIndex", 2);
        Set(run, "Rng", new RunRngSet("123456789"));
        Set(run, "ActFloor", 8);
        Set(run, "ExtraFields", new ExtraRunFields());
        Set(run, "Modifiers", Array.Empty<ModifierModel>());
        Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(f.Player, "_runState", run);
        Set(f.Combat, "RunState", run);
        Set(f.Player, "Deck", new CardPile(PileType.Deck));
        var room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom));
        Set(room, "Id", 98);
        Set(run, "_currentRooms", new List<AbstractRoom> { room });
        Set(RunManager.Instance, "State", run);
        Set(RunManager.Instance, "NetService", new NetSingleplayerGameService());
        Call("PhilosophyRunStateService", "Restore", run, shared);
        ModelDb.Inject(typeof(DefendSilent));
        for (int i = 0; i < 8; i++)
            f.PlayerState.DrawPile.AddInternal(f.Combat.CreateCard<DefendSilent>(f.Player), silent: true);
        return (f, run);
    }

    private static decimal Weak(NativeRuntimeFixture f) =>
        f.Enemy.Powers.FirstOrDefault(p => p is WeakPower) is { } power ? Convert.ToDecimal(power.Amount) : 0m;

    public static async Task Run()
    {
        var (intervene, _) = Fixture(SpinozaState(reorder: false));
        await intervene.Begin();
        intervene.PlayerState.Energy = 40;
        await intervene.PayAndComplete(intervene.AddCard<StrikeSilent>(), intervene.Enemy);
        Check(Weak(intervene) == 1m, "The new Lyre carrier must apply 1 Weak for Intervene.");

        var (reorder, _) = Fixture(SpinozaState(reorder: true));
        await reorder.Begin();
        reorder.PlayerState.Energy = 40;
        int before = reorder.PlayerState.DrawPile.Cards.Count;
        await reorder.PayAndComplete(reorder.AddCard<DefendSilent>(), null);
        Check(reorder.PlayerState.DrawPile.Cards.Count == before - 1,
            "The new Lyre carrier must draw 1 for Reorder.");
        Console.WriteLine("Heraclitus Spinoza checks passed: real flow, new Lyre carrier, native Weak 1 and Draw 1.");
    }
}
