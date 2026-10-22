using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class MachiavelliFloodPreparationNativeChecks
{
    private static readonly Assembly Mod = typeof(RousseauUncarvedStone).Assembly;
    private static Type TypeOf(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) => TypeOf(type)
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args)!;
    public static async Task Run()
    {
        TestMode.IsOn = true;
        RunState? prior = RunManager.Instance.IsInProgress ? RunManager.Instance.DebugOnlyGetState() : null;
        try
        {
            foreach (bool bank in new[] { true, false })
            foreach (bool matches in new[] { true, false }) await Scenario(bank, matches);
            await Scenario(bank: true, matches: false, skipFirst: true);
            Console.WriteLine("Machiavelli native checks passed: saved actual Fixed2, first-turn preparation and lost chance, real turn-two Block/Energy commands, original convention suppressed, native undiscounted payment/no enemy Strength, automatic exclusion, restored reward and clone isolation; no rendered UI/full run.");
        }
        finally { Set(RunManager.Instance, "State", prior); }
    }
    private static async Task Scenario(bool bank, bool matches, bool skipFirst = false)
    {
        Set(RunManager.Instance, "State", null);
        NativeRuntimeFixture fixture = new(typeof(RousseauUncarvedStone));
        RousseauUncarvedStone relic = (RousseauUncarvedStone)fixture.Relic;
        Check(relic.MachiavelliPreparationPayload == string.Empty, "Independent mutable clones never inherit another run's receipt.");
        RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { fixture.Player }); Set(run, "_currentActIndex", 1);
        CombatRoom room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom)); Set(room, "Id", 21);
        Set(run, "_currentRooms", new List<AbstractRoom> { room }); Set(run, "ActFloor", 4);
        Set(RunManager.Instance, "State", run);
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["ROUSSEAU__HISTORY_AND_POWER__CORE"])!;
        object state = Call("MachiavelliFloodPreparationState", "Create", "MACHIAVELLI:NATIVE:2");
        foreach (string action in new[] { "AcceptInvitation", bank ? "ReinforceBank" : "CoordinateEvacuation", "AdoptPrepared", "Leave" })
            Check((bool)Call("MachiavelliFloodPreparationFlow", "TryPrepare", state, Enum.Parse(TypeOf("MachiavelliFloodPreparationAction"), action))
                && (bool)Call("MachiavelliFloodPreparationFlow", "TryCommitPending", state, journey, graph), "Actual history Fixed2 commits.");
        object shared = Activator.CreateInstance(TypeOf("PhilosophyRunState"), true)!;
        shared.GetType().GetProperty("WesternJourney")!.SetValue(shared, journey);
        shared.GetType().GetProperty("MachiavelliFloodPreparation")!.SetValue(shared, state);
        shared = Call("PhilosophyRunStateCodec", "Decode", Call("PhilosophyRunStateCodec", "Encode", shared));
        Call("PhilosophyRunStateService", "Restore", run, shared);
        var skill = fixture.AddCard<DefendSilent>(); var attack = fixture.AddCard<StrikeSilent>();
        await fixture.Begin(); fixture.PlayerState.Energy = 30;
        string before = relic.MachiavelliPreparationPayload;
        await relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), new CardPlay
        {
            Player = fixture.Player, Card = bank ? skill : attack, Target = bank ? null : fixture.Enemy,
            IsAutoPlay = true, ResultPile = PileType.Discard, PlayIndex = 0, PlayCount = 1,
            Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
        });
        Check(relic.MachiavelliPreparationPayload == before, "Automatic cards do not prepare or spend this combat's chance.");
        if (skipFirst)
        {
            await fixture.NextTurn();
            Check(fixture.PlayerState.Energy == 30 && fixture.Player.Creature.Block == 0 && relic.DisplayAmount == 0,
                "A turn with no first manual card gives no reward or misleading waiting counter.");
            await fixture.PayAndComplete(skill, null);
            await relic.AfterPlayerTurnStartLate(new ThrowingPlayerChoiceContext(), fixture.Player);
            Check(fixture.Player.Creature.Block == 0
                && JsonNode.Parse(relic.MachiavelliPreparationPayload)!["Receipts"]!.AsObject().Count == 0,
                "A second-turn card cannot retroactively prepare the first turn.");
            return;
        }
        bool skillFirst = bank == matches;
        var first = await fixture.PayAndComplete(skillFirst ? skill : attack, skillFirst ? null : fixture.Enemy);
        Check(relic.DisplayAmount == (matches ? 1 : 0) && fixture.Player.Creature.Block == 0
            && fixture.PlayerState.Energy == 30 - first.Resources.EnergySpent, "First-turn preparation grants no immediate reward.");
        if (!matches)
            await fixture.PayAndComplete(bank ? fixture.AddCard<DefendSilent>() : fixture.AddCard<StrikeSilent>(), bank ? null : fixture.Enemy);
        // Form an ordinary Strike convention as well. The selected new practice
        // must replace its price/Strength effect, even when that old state exists.
        await fixture.PayAndComplete(fixture.AddCard<StrikeSilent>(), fixture.Enemy);
        await fixture.PayAndComplete(fixture.AddCard<StrikeSilent>(), fixture.Enemy);
        typeof(RousseauUncarvedStone).GetProperty("MachiavelliPreparationPayload")!.SetValue(relic, relic.MachiavelliPreparationPayload);
        decimal energy = fixture.PlayerState.Energy;
        await fixture.NextTurn();
        Check(fixture.Player.Creature.Block == (bank && matches ? 8 : 0)
            && fixture.PlayerState.Energy == energy + (!bank && matches ? 2 : 0),
            "Real native commands grant the selected turn-two reward only after matching preparation.");
        var convention = fixture.AddCard<StrikeSilent>();
        Check(convention.EnergyCost.GetWithModifiers(CostModifiers.All) == 1
            && (await fixture.PayAndComplete(convention, fixture.Enemy)).Resources.EnergySpent == 1
            && (fixture.Enemy.GetPower<StrengthPower>()?.Amount ?? 0) == 0,
            "The old convention neither discounts native payment nor charges enemy Strength.");
        energy = fixture.PlayerState.Energy; int block = fixture.Player.Creature.Block;
        typeof(RousseauUncarvedStone).GetProperty("MachiavelliPreparationPayload")!.SetValue(relic, relic.MachiavelliPreparationPayload);
        await relic.AfterPlayerTurnStartLate(new ThrowingPlayerChoiceContext(), fixture.Player);
        await relic.BeforeCombatStart();
        await relic.AfterPlayerTurnStartLate(new ThrowingPlayerChoiceContext(), fixture.Player);
        Check(fixture.PlayerState.Energy == energy && fixture.Player.Creature.Block == block, "Saved receipt and repeated combat/turn callbacks cannot repay the reward.");
        typeof(RousseauUncarvedStone).GetProperty("MachiavelliPreparationPayload")!.SetValue(relic, "broken");
        await relic.AfterPlayerTurnStartLate(new ThrowingPlayerChoiceContext(), fixture.Player);
        Check(relic.MachiavelliPreparationPayload == "broken" && fixture.PlayerState.Energy == energy
            && fixture.Player.Creature.Block == block, "Unknown preparation payload remains verbatim and inert.");
    }
}
