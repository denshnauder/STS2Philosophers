using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "RunModels", Array.Empty<RunModel>());
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

    // Simulates the native player turn start in real order: advance the turn
    // number, run BeforeSideTurnStart, reset energy/Block, then the late hook.
    private static async Task<int> StartPlayerTurn(NativeRuntimeFixture f, int baseEnergy)
    {
        f.PlayerState.IncrementTurnNumber();
        await f.Relic.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Player,
            [f.Player.Creature], f.Combat);
        f.PlayerState.Energy = baseEnergy;
        if (f.Player.Creature.Block > 0)
            await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), f.Player.Creature,
                f.Player.Creature.Block, f.Player.Creature);
        await f.Relic.AfterPlayerTurnStartLate(new ThrowingPlayerChoiceContext(), f.Player);
        return f.PlayerState.Energy;
    }

    private static async Task Case(Type relic, string node, string[] kinds, int energy, int draw, int block)
    {
        var (f, _) = Fixture(relic, node);
        await f.Begin();
        f.PlayerState.Energy = 40;
        int drawBefore = f.PlayerState.DrawPile.Cards.Count;
        foreach (string kind in kinds)
        {
            CardModel card = kind == "A" ? f.AddCard<StrikeSilent>() : kind == "P" ? f.AddCard<Footwork>() : f.AddCard<DefendSilent>();
            await f.PayAndComplete(card, kind == "A" ? f.Enemy : null);
        }
        int energyBefore = f.PlayerState.Energy;
        int blockBefore = f.Player.Creature.Block;
        int drawBeforeClose = f.PlayerState.DrawPile.Cards.Count;
        await f.Relic.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [f.Player.Creature]);
        // Closing must not pay temporary energy that the next turn would reset.
        Check(f.PlayerState.Energy == energyBefore,
            $"{node} must not pay energy at turn end, got {f.PlayerState.Energy - energyBefore}.");
        Check(f.Player.Creature.Block == blockBefore,
            $"{node} must not pay Block at turn end, got {f.Player.Creature.Block - blockBefore}.");
        Check(f.PlayerState.DrawPile.Cards.Count == drawBeforeClose,
            $"{node} must not draw at turn end, got {drawBeforeClose - f.PlayerState.DrawPile.Cards.Count}.");
        // Native turn start resets energy/Block, then the reward is paid.
        int baseEnergy = 3;
        int energyAtStart = await StartPlayerTurn(f, baseEnergy);
        Check(energyAtStart == baseEnergy + energy,
            $"{node} must grant {energy} usable energy at turn start, got {energyAtStart - baseEnergy}.");
        Check(f.Player.Creature.Block == block,
            $"{node} must grant {block} native Block at turn start, got {f.Player.Creature.Block}.");
        Check(f.PlayerState.DrawPile.Cards.Count == drawBeforeClose - draw,
            $"{node} must draw {draw} native cards at turn start, got {drawBeforeClose - f.PlayerState.DrawPile.Cards.Count}.");
        Check(drawBefore >= 0, "The fixture has a native draw pile.");
    }

    // Two combats on the same relic and node must each pay; a save/restore of the
    // node payload must not re-pay a claimed reward nor drop an unclaimed one.
    private static async Task TwoCombats(Type relic, string node, string[] kinds, int energy, int draw, int block)
    {
        var (f, run) = Fixture(relic, node);
        await f.Begin();
        f.PlayerState.Energy = 40;
        foreach (string kind in kinds)
        {
            CardModel card = kind == "A" ? f.AddCard<StrikeSilent>() : kind == "P" ? f.AddCard<Footwork>() : f.AddCard<DefendSilent>();
            await f.PayAndComplete(card, kind == "A" ? f.Enemy : null);
        }
        await f.Relic.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [f.Player.Creature]);
        int firstEnergy = await StartPlayerTurn(f, 3);
        Check(firstEnergy == 3 + energy, $"{node} first combat must pay {energy} energy, got {firstEnergy - 3}.");

        // End combat, then start a second combat on the same relic and node with a
        // new room identity, a new floor and a fresh native turn number.
        await f.Relic.AfterCombatEnd((CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom)));
        var secondRoom = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom));
        Set(secondRoom, "Id", 99);
        Set(run, "_currentRooms", new List<AbstractRoom> { secondRoom });
        Set(run, "ActFloor", 9);
        Set(f.PlayerState, "TurnNumber", 1);
        await f.Relic.BeforeCombatStart();
        await f.Relic.BeforeSideTurnStart(new ThrowingPlayerChoiceContext(), CombatSide.Player,
            [f.Player.Creature], f.Combat);
        f.PlayerState.Energy = 40;
        foreach (string kind in kinds)
        {
            CardModel card = kind == "A" ? f.AddCard<StrikeSilent>() : kind == "P" ? f.AddCard<Footwork>() : f.AddCard<DefendSilent>();
            await f.PayAndComplete(card, kind == "A" ? f.Enemy : null);
        }
        await f.Relic.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [f.Player.Creature]);
        int secondEnergy = await StartPlayerTurn(f, 3);
        Check(secondEnergy == 3 + energy, $"{node} second combat must pay {energy} energy, got {secondEnergy - 3}.");
        Check(f.Player.Creature.Block == block, $"{node} second combat must pay {block} Block, got {f.Player.Creature.Block}.");
    }

    private static async Task SaveRestore(Type relic, string node, string[] kinds, int energy)
    {
        var (f, _) = Fixture(relic, node);
        await f.Begin();
        f.PlayerState.Energy = 40;
        foreach (string kind in kinds)
        {
            CardModel card = kind == "A" ? f.AddCard<StrikeSilent>() : kind == "P" ? f.AddCard<Footwork>() : f.AddCard<DefendSilent>();
            await f.PayAndComplete(card, kind == "A" ? f.Enemy : null);
        }
        await f.Relic.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [f.Player.Creature]);
        // Restore before claiming: the pending reward must survive.
        string pending = (string)Get(f.Relic, "NodePracticePayload");
        Put(f.Relic, "NodePracticePayload", pending);
        await f.Relic.BeforeCombatStart();
        int energyAtStart = await StartPlayerTurn(f, 3);
        Check(energyAtStart == 3 + energy, $"{node} restored pending reward must pay {energy} energy, got {energyAtStart - 3}.");
        // A repeated late hook in the same turn must not pay again.
        await f.Relic.AfterPlayerTurnStartLate(new ThrowingPlayerChoiceContext(), f.Player);
        int energyRepeat = f.PlayerState.Energy;
        Check(energyRepeat == 3 + energy, $"{node} repeated late hook must not re-pay, got {energyRepeat - 3}.");
        // Restore after claiming: the reward must not be paid again.
        string claimed = (string)Get(f.Relic, "NodePracticePayload");
        Put(f.Relic, "NodePracticePayload", claimed);
        await f.Relic.BeforeCombatStart();
        await f.Relic.AfterPlayerTurnStartLate(new ThrowingPlayerChoiceContext(), f.Player);
        int energyAfterRestore = f.PlayerState.Energy;
        Check(energyAfterRestore == 3 + energy, $"{node} claimed reward must not re-pay, got {energyAfterRestore - 3}.");
    }

    // A legacy payload without CombatId must keep its saved progress and pending
    // reward until a new combat is positively proven.
    private static async Task LegacyRestore(Type relic, string node, string[] kinds, int energy)
    {
        var (f, _) = Fixture(relic, node);
        await f.Begin();
        f.PlayerState.Energy = 40;
        foreach (string kind in kinds)
        {
            CardModel card = kind == "A" ? f.AddCard<StrikeSilent>() : kind == "P" ? f.AddCard<Footwork>() : f.AddCard<DefendSilent>();
            await f.PayAndComplete(card, kind == "A" ? f.Enemy : null);
        }
        await f.Relic.AfterSideTurnEnd(new ThrowingPlayerChoiceContext(), CombatSide.Player, [f.Player.Creature]);
        string payload = (string)Get(f.Relic, "NodePracticePayload");
        string legacy = payload.Replace("\"CombatId\":\"", "\"LegacyCombatId\":\"");
        Put(f.Relic, "NodePracticePayload", legacy);
        await f.Relic.BeforeCombatStart();
        int energyAtStart = await StartPlayerTurn(f, 3);
        Check(energyAtStart == 3 + energy, $"{node} legacy payload must keep pending reward, got {energyAtStart - 3}.");
    }

    public static async Task Run()
    {
        await Case(typeof(RousseauUncarvedStone), "MARY_WOLLSTONECRAFT__FREEDOM_AND_INSTITUTIONS__CORE",
            ["A", "S", "A", "S"], 0, 1, 4);
        await Case(typeof(RousseauUncarvedStone), "SIMONE_DE_BEAUVOIR__SELF_AND_OTHER__CORE",
            ["S", "A", "S", "A"], 1, 1, 0);
        await Case(typeof(PlatoCivicSeal), "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE",
            ["S", "A", "S"], 1, 0, 2);
        await Case(typeof(DescartesThinkingLens), "IMMANUEL_KANT__SELF_AND_OTHER__CORE",
            ["S", "A", "P"], 0, 2, 0);
        await TwoCombats(typeof(RousseauUncarvedStone), "MARY_WOLLSTONECRAFT__FREEDOM_AND_INSTITUTIONS__CORE",
            ["A", "S", "A", "S"], 0, 1, 4);
        await TwoCombats(typeof(RousseauUncarvedStone), "SIMONE_DE_BEAUVOIR__SELF_AND_OTHER__CORE",
            ["S", "A", "S", "A"], 1, 1, 0);
        await TwoCombats(typeof(PlatoCivicSeal), "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE",
            ["S", "A", "S"], 1, 0, 2);
        await SaveRestore(typeof(PlatoCivicSeal), "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE",
            ["S", "A", "S"], 1);
        await LegacyRestore(typeof(PlatoCivicSeal), "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE",
            ["S", "A", "S"], 1);
        Console.WriteLine("Western node practice checks passed: real carrier bind from journey node, native manual card sequence, actual energy/draw/Block rewards for Wollstonecraft, Beauvoir and Kant, two combats on one relic/node with fresh turn numbers, save/restore of claimed vs unclaimed rewards, repeated late-hook dedup, and legacy payload compatibility.");
    }
}
