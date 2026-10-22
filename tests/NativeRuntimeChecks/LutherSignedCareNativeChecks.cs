using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class LutherSignedCareNativeChecks
{
    private static readonly Assembly Mod = typeof(PlatoCivicSeal).Assembly;
    private static Type TypeOf(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) => TypeOf(type)
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length)
        .Invoke(null, args)!;
    private static object Get(object instance, string property) =>
        instance.GetType().GetProperty(property)!.GetValue(instance)!;
    private static void Put(object instance, string property, object value) =>
        instance.GetType().GetProperty(property)!.SetValue(instance, value);
    private static object Reload(object shared) =>
        Call("PhilosophyRunStateCodec", "Decode", Call("PhilosophyRunStateCodec", "Encode", shared));
    private static object LutherAction(string name) => Enum.Parse(TypeOf("LutherSignedCareAction"), name);

    private static object CreateShared(object graph, bool publicWork)
    {
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["PLATO__FREEDOM_AND_INSTITUTIONS__CORE"])!;
        Check((bool)graph.GetType().GetMethod("TryReserveQuestion")!.Invoke(graph, [journey, 1])!
            && (bool)graph.GetType().GetMethod("TryCompleteReservedQuestion")!
                .Invoke(graph, [journey, "WESTERN_EDGE_019"])!,
            "Luther's native fixture commits the actual first-act 019 question.");
        object predecessor = Call("AugustinePromiseState", "Create", "AUGUSTINE:LUTHER:NATIVE:2");
        foreach (string name in new[] { "AcceptInvitation", "PromisePublic",
            publicWork ? "RepairPublic" : "RepairPrivate", "Leave" })
        {
            object action = Enum.Parse(TypeOf("AugustinePromiseAction"), name);
            Check((bool)Call("AugustinePromiseFlow", "TryPrepare", predecessor, action)
                && (bool)Call("AugustinePromiseFlow", "TryCommitPending", predecessor, journey, graph),
                $"Luther's real Augustine predecessor commits {name}.");
        }
        object shared = Activator.CreateInstance(TypeOf("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "WesternJourney", journey);
        Put(shared, "AugustinePromise", predecessor);
        Put(shared, "LutherSignedCare", Call("LutherSignedCareState", "Create",
            "LUTHER:NATIVE:2", Get(predecessor, "InstanceId")));
        return Reload(shared);
    }

    private static object Commit(object shared, object graph, string name)
    {
        Check((bool)Call("LutherSignedCareFlow", "TryPrepare", Get(shared, "LutherSignedCare"),
            LutherAction(name)), $"Luther prepares {name}.");
        shared = Reload(shared);
        object state = Get(shared, "LutherSignedCare");
        object journey = Get(shared, "WesternJourney");
        object predecessor = Get(shared, "AugustinePromise");
        Check((bool)Call("LutherSignedCareFlow", "TryCommitPending", state, journey, predecessor, graph),
            $"Luther commits {name} after shared pending-state reload.");
        Check(!(bool)Call("LutherSignedCareFlow", "TryCommitPending", state, journey, predecessor, graph),
            "A duplicate native-side commit cannot spend the question or switch twice.");
        shared = Reload(shared);
        Check((bool)Call("LutherSignedCareFlow", "IsRouteConsistent", Get(shared, "LutherSignedCare"),
            Get(shared, "WesternJourney"), Get(shared, "AugustinePromise")),
            "The shared decoded Luther record matches the actual journey and predecessor.");
        return shared;
    }

    private static bool HasAugustineWork(PlatoCivicSeal relic, bool publicWork)
    {
        object?[] args = [null];
        bool eligible = (bool)typeof(PlatoCivicSeal).GetMethod("TryGetAugustineWork",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(relic, args)!;
        if (eligible) Check(args[0]?.ToString() == (publicWork ? "Public" : "Private"),
            "The native relic reads the actual public/private predecessor, not the original promise.");
        return eligible;
    }

    private static async Task CheckFirstTurn(NativeRuntimeFixture fixture, RunState run,
        CombatRoom room, object shared, int roomId, bool publicWork, bool expectedReward)
    {
        Call("PhilosophyRunStateService", "Restore", run, Reload(shared));
        Set(room, "Id", roomId);
        PlatoCivicSeal relic = (PlatoCivicSeal)fixture.Relic;
        Check(HasAugustineWork(relic, publicWork) == expectedReward,
            "Native Augustine eligibility follows the saved current route.");
        int beforeBlock = fixture.Player.Creature.Block;
        fixture.PlayerState.Energy = 2;
        await fixture.Begin();
        int expectedBlock = beforeBlock + (expectedReward && publicWork ? 6 : 0);
        int expectedEnergy = 2 + (expectedReward && !publicWork ? 1 : 0);
        Check(fixture.Player.Creature.Block == expectedBlock && fixture.PlayerState.Energy == expectedEnergy,
            $"Saved Luther route reward={expectedReward}, public={publicWork}: real first-turn hooks yield Block {fixture.Player.Creature.Block}/{expectedBlock}, Energy {fixture.PlayerState.Energy}/{expectedEnergy}.");
        string receipt = relic.AugustinePracticePayload;
        typeof(PlatoCivicSeal).GetProperty("AugustinePracticePayload")!.SetValue(relic, receipt);
        // Replaying recovery hooks for the same room must not pay even a retained
        // Augustine rule twice. The switched path must remain inert as well.
        await relic.BeforeCombatStart();
        await fixture.StartTurn();
        Check(fixture.Player.Creature.Block == expectedBlock && fixture.PlayerState.Energy == expectedEnergy,
            "Relic receipt reload and repeated combat/turn hooks cannot repay the old bonus.");
    }

    public static async Task Run()
    {
        TestMode.IsOn = true;
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        try
        {
            // Sixteen complete event paths for each real predecessor branch:
            // decline, three retained signatures, and twelve performed-aid switches.
            foreach (bool publicWork in new[] { true, false })
            {
                await Scenario(publicWork, declaration: null, response: null, aid: null);
                foreach (string declaration in new[] { "ReadText", "CheckScope", "SignAll" })
                {
                    await Scenario(publicWork, declaration, "KeepSignature", aid: null);
                    foreach (string response in new[] { "RefuseAndHelp", "LimitAndHelp" })
                    foreach (string aid in new[] { "CarryWater", "CarryBedding" })
                        await Scenario(publicWork, declaration, response, aid);
                }
            }
            Console.WriteLine("Luther native checks passed: all sixteen saved event paths for both actual 019->020 public/private histories, pending-action reloads, no aid/no switch, retained real 6 Block/1 Energy, switched bonuses stopped after shared reload, and repeated native callback gates; no rendered UI/full game replay.");
        }
        finally { Set(manager, "State", prior); }
    }

    private static async Task Scenario(bool publicWork, string? declaration, string? response, string? aid)
    {
        // Enemy construction reads ascension. Do not expose the preceding
        // scenario's deliberately minimal run to a fresh fixture.
        Set(RunManager.Instance, "State", null);
        NativeRuntimeFixture fixture = new(typeof(PlatoCivicSeal));
        RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { fixture.Player });
        Set(run, "_currentActIndex", 1);
        CombatRoom room = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom));
        Set(run, "_currentRooms", new List<AbstractRoom> { room });
        Set(run, "ActFloor", 4);
        Set(RunManager.Instance, "State", run);
        object graph = Call("WesternRouteGraph", "LoadEmbedded");
        object shared = CreateShared(graph, publicWork);
        await CheckFirstTurn(fixture, run, room, shared, 31, publicWork, expectedReward: true);
        if (declaration is null)
            shared = Commit(shared, graph, "Decline");
        else
        {
            foreach (string name in new[] { "Accept", declaration, response! })
                shared = Commit(shared, graph, name);
            if (aid is not null)
            {
                object state = Get(shared, "LutherSignedCare");
                Check(!(bool)Get(state, "EdgeCommitted") && Get(state, "Aid").ToString() == "None"
                    && Get(Get(shared, "WesternJourney"), "CurrentNodeId").ToString()
                        == "AUGUSTINE_OF_HIPPO__FREEDOM_AND_INSTITUTIONS__CORE"
                    && !(bool)Call("LutherSignedCareFlow", "TryPrepare", state, LutherAction("Leave")),
                    "Refusing or limiting a signature cannot claim aid, switch route, or bypass service.");
                await CheckFirstTurn(fixture, run, room, shared, 32, publicWork, expectedReward: true);
                shared = Commit(shared, graph, aid);
            }
        }
        shared = Commit(shared, graph, "Leave");
        object journey = Get(shared, "WesternJourney");
        object final = Get(shared, "LutherSignedCare");
        bool switched = aid is not null;
        Check((bool)Get(final, "Closed") && (bool)Get(final, "EdgeCommitted") == switched
            && (int)Get(journey, "LastFixedAct") == 2
            && ((IEnumerable<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_047") == switched
            && !((IEnumerable<string>)Get(journey, "CompletedEdgeIds")).Contains("WESTERN_EDGE_021")
            && ((IEnumerable<int>)Get(journey, "ResolvedQuestionActs")).Contains(2) == (declaration is not null),
            "Native closed routes retain Fixed2, preserve decline's question, and commit only the actual 047 switch.");
        await CheckFirstTurn(fixture, run, room, shared, 33, publicWork, expectedReward: !switched);
    }
}
