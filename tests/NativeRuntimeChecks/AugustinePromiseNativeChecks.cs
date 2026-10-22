using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class AugustinePromiseNativeChecks
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Assembly Mod = typeof(PlatoCivicSeal).Assembly;

    private static object Call(Type type, string method, params object[] args) =>
        type.GetMethod(method, Methods)!.Invoke(null, args)!;

    public static async Task Run()
    {
        TestMode.IsOn = true;
        NativeRuntimeFixture fixture = new(typeof(PlatoCivicSeal));
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        try
        {
            RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
            Set(run, "_players", new List<Player> { fixture.Player });
            Set(run, "_currentActIndex", 1);
            Set(manager, "State", run);

            Type graphType = Mod.GetType("STS2Philosophers.WesternRouteGraph")!;
            object graph = Call(graphType, "LoadEmbedded");
            object journey = graphType.GetMethod("Start")!.Invoke(graph,
                ["PLATO__FREEDOM_AND_INSTITUTIONS__CORE"])!;
            Check((bool)graphType.GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!,
                "Native Augustine route reserves the first-act question.");
            Check((bool)graphType.GetMethod("TryCompleteReservedQuestion")!.Invoke(graph,
                [journey, "WESTERN_EDGE_019"])!, "Native Augustine route commits 019.");
            Type stateType = Mod.GetType("STS2Philosophers.AugustinePromiseState")!;
            object state = Call(stateType, "Create", "AUGUSTINE:NATIVE:2");
            Type flow = Mod.GetType("STS2Philosophers.AugustinePromiseFlow")!;
            Type actionType = Mod.GetType("STS2Philosophers.AugustinePromiseAction")!;
            foreach (string actionName in new[] { "AcceptInvitation", "PromisePublic", "RepairPublic", "Leave" })
            {
                object action = Enum.Parse(actionType, actionName);
                Check((bool)Call(flow, "TryPrepare", state, action), $"Native Augustine prepares {actionName}.");
                Check((bool)Call(flow, "TryCommitPending", state, journey, graph),
                    $"Native Augustine commits {actionName}.");
            }
            Type sharedType = Mod.GetType("STS2Philosophers.PhilosophyRunState")!;
            object shared = Activator.CreateInstance(sharedType, nonPublic: true)!;
            sharedType.GetProperty("WesternJourney")!.SetValue(shared, journey);
            sharedType.GetProperty("AugustinePromise")!.SetValue(shared, state);
            Type service = Mod.GetType("STS2Philosophers.PhilosophyRunStateService")!;
            Call(service, "Restore", run, shared);

            object?[] eligibility = [null];
            bool eligible = (bool)typeof(PlatoCivicSeal)
                .GetMethod("TryGetAugustineWork", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(fixture.Relic, eligibility)!;
            Check(eligible && eligibility[0]?.ToString() == "Public",
                "The real Plato relic sees the saved, closed 019->020 public route.");
            CombatRoom room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom));
            Set(room, "Id", 12);
            Set(run, "_currentRooms", new List<AbstractRoom> { room });
            Set(run, "ActFloor", 4);
            await fixture.Relic.BeforeCombatStart();
            await fixture.Relic.BeforeSideTurnStart(null!, MegaCrit.Sts2.Core.Combat.CombatSide.Player,
                [fixture.Player.Creature], fixture.Combat);
            Check(fixture.Player.Creature.Block == 0, "Native player begins without Block.");
            await fixture.Relic.AfterPlayerTurnStartLate(null!, fixture.Player);
            Check(fixture.Player.Creature.Block == 6,
                "Augustine's public repair grants exactly six native Block on turn one.");
            string publicReceipt = ((PlatoCivicSeal)fixture.Relic).AugustinePracticePayload;
            typeof(PlatoCivicSeal).GetProperty("AugustinePracticePayload")!
                .SetValue(fixture.Relic, publicReceipt);
            await fixture.Relic.AfterPlayerTurnStartLate(null!, fixture.Player);
            Check(fixture.Player.Creature.Block == 6,
                "Reloaded first-turn receipt cannot grant the public reward twice.");
            var separateRelic = (PlatoCivicSeal)ModelDb.GetById<RelicModel>(
                ModelDb.GetId<PlatoCivicSeal>()).ToMutable();
            Check(separateRelic.AugustinePracticePayload == string.Empty,
                "Separate civic relics cannot share mutable Augustine combat receipts through the canonical model.");

            object privateJourney = graphType.GetMethod("Start")!.Invoke(graph,
                ["PLATO__FREEDOM_AND_INSTITUTIONS__CORE"])!;
            Check((bool)graphType.GetMethod("TryReserveQuestion")!.Invoke(graph, [privateJourney, 1])!
                && (bool)graphType.GetMethod("TryCompleteReservedQuestion")!.Invoke(graph,
                    [privateJourney, "WESTERN_EDGE_019"])!, "Private branch retains real 019.");
            object privateState = Call(stateType, "Create", "AUGUSTINE:NATIVE:PRIVATE:2");
            foreach (string actionName in new[] { "AcceptInvitation", "PromisePublic", "RepairPrivate", "Leave" })
            {
                object action = Enum.Parse(actionType, actionName);
                Check((bool)Call(flow, "TryPrepare", privateState, action)
                    && (bool)Call(flow, "TryCommitPending", privateState, privateJourney, graph),
                    $"Private route commits {actionName}.");
            }
            sharedType.GetProperty("WesternJourney")!.SetValue(shared, privateJourney);
            sharedType.GetProperty("AugustinePromise")!.SetValue(shared, privateState);
            Call(service, "Restore", run, shared);
            Set(room, "Id", 13);
            fixture.PlayerState.Energy = 2;
            await fixture.Relic.AfterPlayerTurnStartLate(null!, fixture.Player);
            Check(fixture.PlayerState.Energy == 3,
                "Augustine's private repair grants one real native Energy on the first turn.");
            string privateReceipt = ((PlatoCivicSeal)fixture.Relic).AugustinePracticePayload;
            typeof(PlatoCivicSeal).GetProperty("AugustinePracticePayload")!
                .SetValue(fixture.Relic, privateReceipt);
            await fixture.Relic.AfterPlayerTurnStartLate(null!, fixture.Player);
            Check(fixture.PlayerState.Energy == 3,
                "Reloaded private receipt cannot grant a second Energy in the same combat.");
            Console.WriteLine("Augustine native practices passed: saved 019->020 route, public Block 0->6, private Energy 2->3, and save/replay duplicate gates.");
        }
        finally { Set(manager, "State", prior); }
    }
}
