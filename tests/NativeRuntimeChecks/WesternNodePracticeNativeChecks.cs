using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Exercises the generic new-node practice on a real carrier relic in a real
// combat room: bind from the journey node, record native manual plays, close the
// turn, and assert the actual native energy/draw/block deltas.
internal static class WesternNodePracticeNativeChecks
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

    private static (NativeRuntimeFixture F, RunState Run) Fixture(Type relic, string node)
    {
        Set(RunManager.Instance, "State", null);
        var f = new NativeRuntimeFixture(relic);
        f.AddEnemy(8);
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["SOCRATES__VIRTUE_AND_HAPPINESS__CORE"])!;
        Put(journey, "CurrentNodeId", node);
        Put(journey, "LastFixedAct", 2);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
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

    private static async Task Case(Type relic, string node, string[] kinds, int energy, int draw, int block)
    {
        var (f, _) = Fixture(relic, node);
        await f.Begin();
        f.PlayerState.Energy = 40;
        int drawBefore = f.PlayerState.DrawPile.Cards.Count;
        foreach (string kind in kinds)
        {
            CardModel card = kind == "A" ? f.AddCard<StrikeSilent>() : f.AddCard<DefendSilent>();
            await f.PayAndComplete(card, kind == "A" ? f.Enemy : null);
        }
        int energyBefore = f.PlayerState.Energy;
        int blockBefore = f.Player.Creature.Block;
        int drawBeforeClose = f.PlayerState.DrawPile.Cards.Count;
        await f.Relic.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [f.Player.Creature]);
        Check(f.PlayerState.Energy == energyBefore + energy,
            $"{node} must grant {energy} native energy, got {f.PlayerState.Energy - energyBefore}.");
        Check(f.Player.Creature.Block == blockBefore + block,
            $"{node} must grant {block} native Block, got {f.Player.Creature.Block - blockBefore}.");
        Check(f.PlayerState.DrawPile.Cards.Count == drawBeforeClose - draw,
            $"{node} must draw {draw} native cards, got {drawBeforeClose - f.PlayerState.DrawPile.Cards.Count}.");
        Check(drawBefore >= 0, "The fixture has a native draw pile.");
    }

    public static async Task Run()
    {
        await Case(typeof(RousseauUncarvedStone), "MARY_WOLLSTONECRAFT__FREEDOM_AND_INSTITUTIONS__CORE",
            ["A", "S", "A", "S"], 0, 1, 4);
        await Case(typeof(RousseauUncarvedStone), "SIMONE_DE_BEAUVOIR__SELF_AND_OTHER__CORE",
            ["S", "A", "S", "A"], 1, 1, 0);
        await Case(typeof(PlatoCivicSeal), "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE",
            ["S", "A", "S"], 1, 0, 2);
        Console.WriteLine("Western node practice checks passed: real carrier bind from journey node, native manual card sequence, actual energy/draw/Block rewards for Wollstonecraft, Beauvoir and Kant.");
    }
}
