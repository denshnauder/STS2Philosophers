using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class OckhamNamesInquiryNativeChecks
{
    private static readonly Assembly Mod = typeof(AristotleCategoryTablet).Assembly;
    private static Type TypeOf(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) =>
        TypeOf(type).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args)!;
    private static object ActionOf(string type, string value) => Enum.Parse(TypeOf(type), value);

    public static async Task Run()
    {
        TestMode.IsOn = true;
        RunState? prior = RunManager.Instance.IsInProgress ? RunManager.Instance.DebugOnlyGetState() : null;
        try
        {
            await Scenario(named: true, zeroFirst: false);
            await Scenario(named: false, zeroFirst: false);
            await Scenario(named: true, zeroFirst: true);
            Console.WriteLine("Ockham native checks passed: actual saved Gorgias/local critique, native price display/payment, narrow versus broad matching, different-cost same name, two-discount limit, zero first card, automatic exclusion and restored receipts; no rendered UI/full game replay.");
        }
        finally { Set(RunManager.Instance, "State", prior); }
    }
    private static async Task Scenario(bool named, bool zeroFirst)
    {
        Set(RunManager.Instance, "State", null);
        NativeRuntimeFixture fixture = new(typeof(AristotleCategoryTablet));
        RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { fixture.Player }); Set(run, "_currentActIndex", 1);
        Set(RunManager.Instance, "State", run);
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["ARISTOTLE__LANGUAGE_AND_MEANING__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!, "Native fixture begins with the real first question.");
        object gorgias = Call("GorgiasSpeechInquiryState", "Create", "GORGIAS:NATIVE:1");
        foreach (string action in new[] { "AcceptInvitation", "AssertContents", "SeparateBeliefFromProof", "RecordBoundary", "Leave" })
            Check((bool)Call("GorgiasSpeechInquiryFlow", "TryPrepare", gorgias, ActionOf("GorgiasSpeechInquiryAction", action))
                && (bool)Call("GorgiasSpeechInquiryFlow", "TryCommitPending", gorgias, journey), "Native predecessor history commits.");
        object state = Call("OckhamNamesInquiryState", "Create", "OCKHAM:NATIVE:2", "GORGIAS:NATIVE:1");
        foreach (string action in new[] { "Accept", "CheckWorkers", named ? "UseNamedList" : "Annotate", "Close" })
            Check((bool)Call("OckhamNamesInquiryFlow", "TryPrepare", state, ActionOf("OckhamNamesAction", action))
                && (bool)Call("OckhamNamesInquiryFlow", "TryCommitPending", state, journey, gorgias, graph), "Native local critique commits.");
        object shared = Activator.CreateInstance(TypeOf("PhilosophyRunState"), true)!;
        shared.GetType().GetProperty("WesternJourney")!.SetValue(shared, journey);
        shared.GetType().GetProperty("GorgiasSpeechInquiry")!.SetValue(shared, gorgias);
        shared.GetType().GetProperty("OckhamNamesInquiry")!.SetValue(shared, state);
        shared = Call("PhilosophyRunStateCodec", "Decode", Call("PhilosophyRunStateCodec", "Encode", shared));
        Call("PhilosophyRunStateService", "Restore", run, shared);
        var first = fixture.AddCard<StrikeSilent>(); first.EnergyCost.SetThisTurnOrUntilPlayed(zeroFirst ? 0 : 2);
        var different = fixture.AddCard<DefendSilent>(); different.EnergyCost.SetThisTurnOrUntilPlayed(2);
        var same = fixture.AddCard<StrikeSilent>(); same.EnergyCost.SetThisTurnOrUntilPlayed(3);
        var sameAgain = fixture.AddCard<StrikeSilent>(); sameAgain.EnergyCost.SetThisTurnOrUntilPlayed(2);
        var exhausted = fixture.AddCard<StrikeSilent>(); exhausted.EnergyCost.SetThisTurnOrUntilPlayed(2);
        await fixture.Begin(); fixture.PlayerState.Energy = 30;
        await fixture.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), new CardPlay
        {
            Player = fixture.Player, Card = first, Target = fixture.Enemy, IsAutoPlay = true,
            Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
            ResultPile = PileType.Discard, PlayIndex = 0, PlayCount = 1,
        });
        Check(JsonNode.Parse(fixture.Relic.PracticePayload)!["State"]!["CategoryBucket"]!.GetValue<int>() == -1,
            "An automatic card does not establish a name or category.");
        var firstPlay = await fixture.PayAndComplete(first, fixture.Enemy);
        Check(firstPlay.Resources.EnergySpent == (zeroFirst ? 0 : 2), "First card actually pays its full price.");
        fixture.ReloadRelicPayload();
        var otherPlay = await fixture.PayAndComplete(different, null);
        Check(otherPlay.Resources.EnergySpent == (zeroFirst || named ? 2 : 1),
            "Different name at the same cost is excluded only by the named practice.");
        decimal sameCost = zeroFirst ? 3 : named ? 1 : 2;
        Check(same.EnergyCost.GetWithModifiers(CostModifiers.All) == sameCost, "Native displayed cost agrees with the exact rule.");
        var samePlay = await fixture.PayAndComplete(same, fixture.Enemy);
        Check(samePlay.Resources.EnergySpent == sameCost, "Same name with changed cost uses native payment.");
        string receipt = fixture.Relic.PracticePayload;
        await fixture.Relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), samePlay);
        Check(fixture.Relic.PracticePayload == receipt, "Repeated live callback cannot consume a second discount.");
        fixture.ReloadRelicPayload();
        var againPlay = await fixture.PayAndComplete(sameAgain, fixture.Enemy);
        Check(againPlay.Resources.EnergySpent == (!zeroFirst && named ? 0 : 2),
            "Saved named practice retains exactly the second discount; old broad practice is already spent.");
        var exhaustedPlay = await fixture.PayAndComplete(exhausted, fixture.Enemy);
        Check(exhaustedPlay.Resources.EnergySpent == 2, "A third matching card receives no extra discount.");
        await fixture.NextTurn();
        var newFirst = fixture.AddCard<DefendSilent>(); newFirst.EnergyCost.SetThisTurnOrUntilPlayed(2);
        Check((await fixture.PayAndComplete(newFirst, null)).Resources.EnergySpent == 2,
            "A new turn begins without carrying the previous name.");
    }
}
