using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class BaconSampleLedgerNativeChecks
{
    private static readonly Assembly Mod = typeof(AristotleCategoryTablet).Assembly;
    private static Type TypeOf(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) => TypeOf(type)
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length)
        .Invoke(null, args)!;
    private static object Get(object state, string name) => state.GetType().GetProperty(name)!.GetValue(state)!;
    private static void Put(object state, string name, object value) => state.GetType().GetProperty(name)!.SetValue(state, value);
    private static object Reload(object state) => Call("PhilosophyRunStateCodec", "Decode", Call("PhilosophyRunStateCodec", "Encode", state));
    private static void Commit(string flow, string actionEnum, object state, string name, params object[] rest)
    {
        Check((bool)Call(flow, "TryPrepare", state, Enum.Parse(TypeOf(actionEnum), name))
            && (bool)Call(flow, "TryCommitPending", [state, .. rest]), $"Native Bacon predecessor/history commits {name}.");
    }

    private static object CreateShared(bool named, string outcome)
    {
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["ARISTOTLE__LANGUAGE_AND_MEANING__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!,
            "Bacon native history consumes the actual first-act question.");
        object gorgias = Call("GorgiasSpeechInquiryState", "Create", "GORGIAS:BACON:NATIVE:1");
        foreach (string action in new[] { "AcceptInvitation", "AssertContents", "SeparateBeliefFromProof", "RecordBoundary", "Leave" })
            Commit("GorgiasSpeechInquiryFlow", "GorgiasSpeechInquiryAction", gorgias, action, journey);
        object ockham = Call("OckhamNamesInquiryState", "Create", "OCKHAM:BACON:NATIVE:2", Get(gorgias, "InstanceId"));
        foreach (string action in new[] { "Accept", "CheckWorkers", named ? "UseNamedList" : "Annotate", "Close" })
            Commit("OckhamNamesInquiryFlow", "OckhamNamesAction", ockham, action, journey, gorgias, graph);
        object bacon = Call("BaconSampleLedgerState", "Create", "BACON:NATIVE:3", Get(ockham, "InstanceId"));
        string[] actions = outcome == "DeclineInvitation" ? [outcome, "Leave"]
            : ["AcceptInvitation", "CheckMarket", "TakeCounterSample", outcome, "Leave"];
        foreach (string action in actions)
            Commit("BaconSampleLedgerFlow", "BaconSampleLedgerAction", bacon, action, journey, ockham, gorgias, graph);
        object shared = Activator.CreateInstance(TypeOf("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey); Put(shared, "GorgiasSpeechInquiry", gorgias);
        Put(shared, "OckhamNamesInquiry", ockham); Put(shared, "BaconSampleLedger", bacon);
        return Reload(shared);
    }

    public static async Task Run()
    {
        TestMode.IsOn = true;
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        try
        {
            foreach (bool named in new[] { false, true })
            foreach (bool control in new[] { false, true })
            foreach (bool matching in new[] { false, true })
                await Scenario(named, control, matching);
            foreach (bool control in new[] { false, true })
            {
                await Scenario(named: true, control, matching: true, skipSecond: true);
                await Scenario(named: true, control, matching: true, skipWindow: true);
            }
            foreach (bool named in new[] { false, true })
            foreach (string outcome in new[] { "KeepPrediction", "DeclineInvitation" })
                await RetainedScenario(named, outcome);
            Console.WriteLine("Bacon native checks passed: real saved Gorgias/Ockham/Fixed3 chains, real 2/3-card draws from two different native card types, same-type failure, no-second/missed-window loss, automatic exclusion, old broad/named costs suppressed, retained old cost practice, saved partial/spent receipts, malformed-payload isolation and clone isolation; no rendered UI/full game replay.");
        }
        finally { Set(manager, "State", prior); }
    }

    private static (NativeRuntimeFixture Fixture, RunState Run) FixtureFor(object shared)
    {
        Set(RunManager.Instance, "State", null);
        NativeRuntimeFixture fixture = new(typeof(AristotleCategoryTablet));
        RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { fixture.Player }); Set(run, "_currentActIndex", 2);
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(fixture.Player, "_runState", run); Set(fixture.Combat, "RunState", run);
        Set(fixture.Player, "Deck", new CardPile(PileType.Deck));
        CombatRoom room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom));
        Set(room, "Id", 51); Set(run, "_currentRooms", new List<AbstractRoom> { room }); Set(run, "ActFloor", 4);
        Set(RunManager.Instance, "State", run);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        return (fixture, run);
    }

    private static void FillDrawPile(NativeRuntimeFixture fixture)
    {
        ModelDb.Inject(typeof(DefendSilent));
        for (int i = 0; i < 6; i++)
        {
            CardModel card = fixture.Combat.CreateCard<DefendSilent>(fixture.Player);
            fixture.PlayerState.DrawPile.AddInternal(card, silent: true);
        }
    }

    private static async Task Scenario(bool named, bool control, bool matching, bool skipSecond = false, bool skipWindow = false)
    {
        var (fixture, run) = FixtureFor(CreateShared(named, control ? "TakeControlSample" : "PublishLimited"));
        AristotleCategoryTablet relic = (AristotleCategoryTablet)fixture.Relic;
        Check(relic.BaconComparisonPayload == string.Empty, "A separate mutable tablet starts with no Bacon receipt.");
        FillDrawPile(fixture);
        var first = fixture.AddCard<StrikeSilent>(); first.EnergyCost.SetThisTurnOrUntilPlayed(2);
        CardModel second = matching ? fixture.AddCard<DefendSilent>() : fixture.AddCard<StrikeSilent>();
        second.EnergyCost.SetThisTurnOrUntilPlayed(2);
        await fixture.Begin(); fixture.PlayerState.Energy = 30;
        Check(relic.DisplayAmount == 2, "A new comparison advertises two pending manual cards.");
        if (control)
        {
            await fixture.PayAndComplete(fixture.AddCard<StrikeSilent>(), fixture.Enemy);
            await fixture.PayAndComplete(fixture.AddCard<DefendSilent>(), null);
            Check(fixture.PlayerState.DrawPile.Cards.Count == 6 && relic.DisplayAmount == 2,
                "The delayed comparison ignores all first-turn cards.");
            await fixture.NextTurn();
        }
        if (skipWindow)
        {
            await fixture.NextTurn();
            Check(relic.DisplayAmount == 0, "A missed target turn does not advertise an available comparison.");
        }
        string beforeAuto = relic.BaconComparisonPayload;
        await relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), new CardPlay
        {
            Player = fixture.Player, Card = first, Target = fixture.Enemy, IsAutoPlay = true,
            ResultPile = PileType.Discard, PlayIndex = 0, PlayCount = 1,
            Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
        });
        Check(relic.BaconComparisonPayload == beforeAuto, "An automatic card neither samples nor spends the comparison.");
        CardPlay firstPlay = await fixture.PayAndComplete(first, fixture.Enemy);
        Check(firstPlay.Resources.EnergySpent == 2 && relic.DisplayAmount == (skipWindow ? 0 : 1),
            "The first actual manual card pays full price and leaves one sample pending only in the target turn.");
        string partial = relic.BaconComparisonPayload;
        typeof(AristotleCategoryTablet).GetProperty("BaconComparisonPayload")!.SetValue(relic, partial);
        fixture.ReloadRelicPayload();
        await relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), firstPlay);
        Check(relic.BaconComparisonPayload == partial, "A repeated first-card callback cannot become the second sample after payload reload.");
        if (skipSecond) await fixture.NextTurn();
        int beforeHand = fixture.PlayerState.Hand.Cards.Count;
        Check(second.EnergyCost.GetWithModifiers(CostModifiers.All) == 2,
            "Bacon replaces both the original category discount and Ockham's named discount in the native displayed cost.");
        CardPlay secondPlay = await fixture.PayAndComplete(second, matching ? null : fixture.Enemy);
        int drawn = matching && !skipSecond && !skipWindow ? control ? 3 : 2 : 0;
        Check(secondPlay.Resources.EnergySpent == 2 && fixture.PlayerState.DrawPile.Cards.Count == 6 - drawn
            && fixture.PlayerState.Hand.Cards.Count == beforeHand - 1 + drawn && relic.DisplayAmount == 0,
            $"Native Bacon comparison control={control}, match={matching}, skipSecond={skipSecond}, skipWindow={skipWindow} actually draws {drawn} cards and pays full price.");
        string spent = relic.BaconComparisonPayload;
        typeof(AristotleCategoryTablet).GetProperty("BaconComparisonPayload")!.SetValue(relic, spent);
        Call("PhilosophyRunStateService", "Restore", run, Reload(CreateShared(named, control ? "TakeControlSample" : "PublishLimited")));
        await relic.BeforeCombatStart();
        await relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), secondPlay);
        Check(relic.BaconComparisonPayload == spent && fixture.PlayerState.DrawPile.Cards.Count == 6 - drawn,
            "Shared state reload, saved spent receipt and recovery combat hook cannot repay the draw.");
        var later = fixture.AddCard<StrikeSilent>();
        Check((await fixture.PayAndComplete(later, fixture.Enemy)).Resources.EnergySpent == 1
            && fixture.PlayerState.DrawPile.Cards.Count == 6 - drawn, "A later card cannot repair or repeat the two-card comparison.");
        typeof(AristotleCategoryTablet).GetProperty("BaconComparisonPayload")!.SetValue(relic, "broken");
        var malformed = fixture.AddCard<StrikeSilent>(); malformed.EnergyCost.SetThisTurnOrUntilPlayed(2);
        Check(malformed.EnergyCost.GetWithModifiers(CostModifiers.All) == 2
            && (await fixture.PayAndComplete(malformed, fixture.Enemy)).Resources.EnergySpent == 2
            && relic.BaconComparisonPayload == "broken" && relic.DisplayAmount == 0
            && fixture.PlayerState.DrawPile.Cards.Count == 6 - drawn,
            "Malformed payload stays verbatim and inert without falling back to an old discount.");
    }

    private static async Task RetainedScenario(bool named, string outcome)
    {
        var (fixture, _) = FixtureFor(CreateShared(named, outcome));
        var first = fixture.AddCard<StrikeSilent>(); first.EnergyCost.SetThisTurnOrUntilPlayed(2);
        var different = fixture.AddCard<DefendSilent>(); different.EnergyCost.SetThisTurnOrUntilPlayed(2);
        var same = fixture.AddCard<StrikeSilent>(); same.EnergyCost.SetThisTurnOrUntilPlayed(3);
        await fixture.Begin(); fixture.PlayerState.Energy = 30;
        Check((await fixture.PayAndComplete(first, fixture.Enemy)).Resources.EnergySpent == 2,
            "A retained third-act route starts its old practice normally.");
        Check((await fixture.PayAndComplete(different, null)).Resources.EnergySpent == (named ? 2 : 1)
            && (await fixture.PayAndComplete(same, fixture.Enemy)).Resources.EnergySpent == (named ? 1 : 2)
            && ((AristotleCategoryTablet)fixture.Relic).BaconComparisonPayload == string.Empty,
            "Both KeepPrediction and Decline preserve the actual predecessor's named/broad practice through its validated historical projection.");
    }
}
