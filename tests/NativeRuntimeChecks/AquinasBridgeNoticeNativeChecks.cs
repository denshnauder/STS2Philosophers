using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class AquinasBridgeNoticeNativeChecks
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Assembly Mod = typeof(PlatoCivicSeal).Assembly;
    private static object Call(Type type, string method, params object[] args) =>
        type.GetMethods(Static).Single(candidate => candidate.Name == method
            && candidate.GetParameters().Length == args.Length).Invoke(null, args)!;

    public static async Task Run()
    {
        TestMode.IsOn = true;
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        try
        {
            foreach (bool emergency in new[] { false, true })
            foreach (bool matchingFirst in new[] { false, true })
                await CheckScenario(emergency, matchingFirst);
            Console.WriteLine("Aquinas native practice passed: real 016->045 saved routes, native card payment and Block/Energy commands, off-rule first-card loss, automatic-card exclusion and saved duplicate gates; no rendered UI/full game replay.");
        }
        finally { Set(manager, "State", prior); }
    }

    private static async Task CheckScenario(bool emergency, bool matchingFirst)
    {
        // Fixture enemies query ascension during construction. Do not expose
        // the preceding scenario's deliberately minimal RunState to that code.
        Set(RunManager.Instance, "State", null);
        NativeRuntimeFixture fixture = new(typeof(PlatoCivicSeal));
        RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { fixture.Player });
        Set(run, "_currentActIndex", 2);
        Set(RunManager.Instance, "State", run);
        CombatRoom room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom));
        Set(room, "Id", 21);
        Set(run, "_currentRooms", new List<AbstractRoom> { room });
        Set(run, "ActFloor", 4);

        Type graphType = Mod.GetType("STS2Philosophers.WesternRouteGraph")!;
        object graph = Call(graphType, "LoadEmbedded");
        object journey = graphType.GetMethod("Start")!.Invoke(graph,
            ["PLATO__FREEDOM_AND_INSTITUTIONS__CORE"])!;
        Check((bool)graphType.GetMethod("TryApply")!.Invoke(graph, [journey, "WESTERN_EDGE_016"])!,
            "Aquinas native route begins with actual Aristotle institution 016.");
        Type stateType = Mod.GetType("STS2Philosophers.AquinasBridgeNoticeState")!;
        object state = Call(stateType, "Create", "AQUINAS:NATIVE:3");
        Type flow = Mod.GetType("STS2Philosophers.AquinasBridgeNoticeFlow")!;
        Type actionType = Mod.GetType("STS2Philosophers.AquinasBridgeNoticeAction")!;
        foreach (string name in new[] { "AcceptInvitation", "AuditPublication",
            emergency ? "VerifyException" : "PublishCorrection", "Leave" })
        {
            object action = Enum.Parse(actionType, name);
            Check((bool)Call(flow, "TryPrepare", state, action)
                && (bool)Call(flow, "TryCommitPending", state, journey, graph),
                $"Aquinas native route commits {name}.");
        }
        Type sharedType = Mod.GetType("STS2Philosophers.PhilosophyRunState")!;
        object shared = Activator.CreateInstance(sharedType, nonPublic: true)!;
        sharedType.GetProperty("WesternJourney")!.SetValue(shared, journey);
        sharedType.GetProperty("AquinasBridgeNotice")!.SetValue(shared, state);
        // Exercise the actual shared encoding, rather than passing unsaved state
        // directly to the relic's private eligibility method.
        Type codec = Mod.GetType("STS2Philosophers.PhilosophyRunStateCodec")!;
        shared = Call(codec, "Decode", Call(codec, "Encode", shared));
        Type service = Mod.GetType("STS2Philosophers.PhilosophyRunStateService")!;
        Call(service, "Restore", run, shared);
        object?[] args = [null];
        Check((bool)typeof(PlatoCivicSeal).GetMethod("TryGetAquinasRule",
                BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(fixture.Relic, args)!
            && args[0]?.ToString() == (emergency ? "Emergency" : "Published"),
            "The real civic relic sees the exact saved event-selected rule.");

        var skill = fixture.AddCard<DefendSilent>();
        var attack = fixture.AddCard<StrikeSilent>();
        await fixture.Begin();
        fixture.PlayerState.Energy = 9;
        PlatoCivicSeal relic = (PlatoCivicSeal)fixture.Relic;
        string beforeAuto = relic.AquinasPracticePayload;
        await relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), new CardPlay
        {
            Player = fixture.Player, Card = emergency ? attack : skill, IsAutoPlay = true,
            Target = emergency ? fixture.Enemy : null, ResultPile = PileType.Discard,
            Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
            PlayIndex = 0, PlayCount = 1,
        });
        Check(relic.AquinasPracticePayload == beforeAuto,
            "Automatic cards neither earn nor spend the first-manual-card chance.");
        bool attackFirst = emergency == matchingFirst;
        decimal initialEnergy = fixture.PlayerState.Energy;
        CardPlay first = await fixture.PayAndComplete(attackFirst ? attack : skill,
            attackFirst ? fixture.Enemy : null);
        decimal expectedEnergy = initialEnergy - first.Resources.EnergySpent
            + (emergency && matchingFirst ? 1 : 0);
        Check(fixture.PlayerState.Energy == expectedEnergy
            && fixture.Player.Creature.Block == (!emergency && matchingFirst ? 5 : 0),
            $"Native first card emergency={emergency}, matching={matchingFirst}: energy {fixture.PlayerState.Energy}, expected {expectedEnergy}, spent {first.Resources.EnergySpent}; block {fixture.Player.Creature.Block}, receipt {relic.AquinasPracticePayload}.");
        string receipt = relic.AquinasPracticePayload;
        typeof(PlatoCivicSeal).GetProperty("AquinasPracticePayload")!.SetValue(relic, receipt);
        // A native combat-start hook during recovery must not reopen the saved
        // opportunity. The existing civic carrier can reset its own runtime.
        await relic.BeforeCombatStart();
        await relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), first);
        Check(fixture.PlayerState.Energy == expectedEnergy
            && fixture.Player.Creature.Block == (!emergency && matchingFirst ? 5 : 0),
            "Restored receipt and repeated native callback cannot pay the first card twice.");
        if (!matchingFirst)
        {
            var match = emergency ? (MegaCrit.Sts2.Core.Models.CardModel)fixture.AddCard<StrikeSilent>()
                : fixture.AddCard<DefendSilent>();
            decimal before = fixture.PlayerState.Energy;
            CardPlay second = await fixture.PayAndComplete(match, emergency ? fixture.Enemy : null);
            Check(fixture.PlayerState.Energy == before - second.Resources.EnergySpent
                && fixture.Player.Creature.Block == 0,
                "A later matching card cannot recover the chance lost by an off-rule first manual card.");
        }
        typeof(PlatoCivicSeal).GetProperty("AquinasPracticePayload")!.SetValue(relic, "broken");
        decimal frozenEnergy = fixture.PlayerState.Energy;
        int frozenBlock = fixture.Player.Creature.Block;
        await relic.AfterCardPlayed(new ThrowingPlayerChoiceContext(), first);
        Check(relic.AquinasPracticePayload == "broken" && fixture.PlayerState.Energy == frozenEnergy
            && fixture.Player.Creature.Block == frozenBlock,
            "Invalid saved practice is preserved verbatim and cannot grant a reward.");
    }
}
