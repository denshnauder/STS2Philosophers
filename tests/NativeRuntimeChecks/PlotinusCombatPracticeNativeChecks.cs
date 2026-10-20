using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class PlotinusCombatPracticeNativeChecks
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Assembly Mod = typeof(SocratesBalanceWeight).Assembly;

    private static object Call(Type type, string method, params object[] args) =>
        type.GetMethod(method, Methods)!.Invoke(null, args)!;

    public static async Task Run()
    {
        TestMode.IsOn = true;
        NativeRuntimeFixture fixture = new(typeof(SocratesBalanceWeight));
        var manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        try
        {
            RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
            Set(run, "_players", new List<Player> { fixture.Player });
            Set(run, "_currentActIndex", 2);
            Set(manager, "State", run);

            Type graphType = Mod.GetType("STS2Philosophers.WesternRouteGraph")!;
            object graph = Call(graphType, "LoadEmbedded");
            object journey = graphType.GetMethod("Start")!.Invoke(graph,
                ["SOCRATES__VIRTUE_AND_HAPPINESS__CORE"])!;
            MethodInfo apply = graphType.GetMethod("TryApply")!;
            foreach (string edge in new[] { "WESTERN_EDGE_008", "WESTERN_EDGE_009" })
                Check((bool)apply.Invoke(graph, [journey, edge])!, $"Native route prepares {edge}.");
            Type eventType = Mod.GetType("STS2Philosophers.PlotinusCommonGroundState")!;
            object eventState = Call(eventType, "Create", "NATIVE:PLOTINUS:3");
            Type flow = Mod.GetType("STS2Philosophers.PlotinusCommonGroundFlow")!;
            Type actionType = Mod.GetType("STS2Philosophers.PlotinusGroundAction")!;
            foreach (string actionName in new[] { "AcceptInvitation", "ExamineCommon", "DeliverQualifiedMap", "Leave" })
            {
                object action = Enum.Parse(actionType, actionName);
                Check((bool)Call(flow, "TryPrepare", eventState, action), $"Native route prepares {actionName}.");
                Check((bool)Call(flow, "TryCommitPending", eventState, journey, graph),
                    $"Native route commits {actionName}.");
            }
            Type sharedType = Mod.GetType("STS2Philosophers.PhilosophyRunState")!;
            object shared = Activator.CreateInstance(sharedType, nonPublic: true)!;
            sharedType.GetProperty("WesternJourney")!.SetValue(shared, journey);
            sharedType.GetProperty("PlotinusCommonGround")!.SetValue(shared, eventState);
            Type service = Mod.GetType("STS2Philosophers.PhilosophyRunStateService")!;
            Call(service, "Restore", run, shared);

            bool eligible = (bool)typeof(SocratesBalanceWeight)
                .GetMethod("IsPlotinusRoute", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(fixture.Relic, null)!;
            Check(eligible, "The real relic sees only the saved, closed 044 route.");

            var skillA = fixture.AddCard<DefendSilent>();
            var attack = fixture.AddCard<StrikeSilent>();
            var skillB = fixture.AddCard<Survivor>();
            await fixture.Relic.BeforeCombatStart();
            await fixture.Relic.BeforeSideTurnStart(null!, CombatSide.Player,
                [fixture.Player.Creature], fixture.Combat);
            fixture.PlayerState.Energy = 9;
            await fixture.PayAndComplete(skillA, null);
            await fixture.PayAndComplete(attack, fixture.Enemy);
            await fixture.PayAndComplete(skillB, null);
            string before = ((SocratesBalanceWeight)fixture.Relic).PlotinusPracticePayload;
            Check(JsonNode.Parse(before)!["Plays"]!.AsArray().Count == 3,
                "Real native card callbacks record the exact three-card sequence.");
            Check(fixture.Player.Creature.Block == 0,
                "The native creature starts without block.");
            await fixture.Relic.AfterSideTurnEnd(null!, CombatSide.Player, [fixture.Player.Creature]);
            Check(fixture.Player.Creature.Block == 5,
                "The native turn-end command grants exactly five actual block.");
            string after = ((SocratesBalanceWeight)fixture.Relic).PlotinusPracticePayload;
            Check(JsonNode.Parse(after)!["Closed"]!.GetValue<bool>(),
                "The real end-of-turn hook closes and claims the sequence once.");
            await fixture.Relic.AfterSideTurnEnd(null!, CombatSide.Player, [fixture.Player.Creature]);
            Check(fixture.Player.Creature.Block == 5,
                "A repeated native turn-end hook grants no additional block.");
            Check(((SocratesBalanceWeight)fixture.Relic).PlotinusPracticePayload == after,
                "A repeated native turn-end hook does not grant a second reward.");
            Console.WriteLine("Plotinus native relic checks passed: closed saved route, real card callbacks, native turn-end block command and duplicate gate; no rendered UI/full game replay.");
        }
        finally { Set(manager, "State", prior); }
    }
}
