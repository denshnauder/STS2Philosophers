using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Drives HegelSubstanceSubjectPatch.TrySelectNative with a real RunState, real
// map history and a real WesternJourneyState. Nothing here passes a precomputed
// "source combat" boolean to the policy: every case goes through the production
// entry, including the edge eligibility and the act-3 combat gate.
internal static class HegelCrossActNativeChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private const string SpinozaNode = "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE";
    private const string EntryNode = "HERACLITUS__BEING_AND_CHANGE__CORE";
    private static Type T(string name) => typeof(HegelSubstanceSubject).Assembly.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) => T(type).GetMethods(Flags)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length).Invoke(null, args);
    private static object Get(object item, string name) => item.GetType().GetProperty(name, Flags)!.GetValue(item)!;
    private static void Put(object item, string name, object? value) => item.GetType().GetProperty(name, Flags)!.SetValue(item, value);

    public static Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        object? priorNetwork = manager.NetService;
        object priorSynchronizer = manager.EventSynchronizer, priorExecutor = manager.ActionExecutor;
        object? priorTurn = typeof(MegaCrit.Sts2.Core.Combat.CombatManager).GetField("_turnState", Flags)!
            .GetValue(MegaCrit.Sts2.Core.Combat.CombatManager.Instance);
        try
        {
            TestMode.IsOn = true;
            ModelDb.Inject(typeof(HegelSubstanceSubject));
            ModelDb.Inject(typeof(LeibnizMonadComparison));
            MissingRequiredEdge();
            CombatBeforePredecessor();
            SecondActFightWithoutThirdActFight();
            ThirdActFightWithoutSecondActFight();
            ThirdActFightAfterSecondActFight();
            Console.WriteLine("Hegel cross-act entry checks passed: the real HegelSubstanceSubjectPatch.TrySelectNative rejects a journey without WESTERN_EDGE_024, rejects a third-act history whose only fight precedes Leibniz, still requires at least one completed third-act normal combat, and selects Hegel 025 when a third-act fight exists with a second-act Leibniz event (either a second-act fight after it or the third-act fight itself).");
        }
        finally
        {
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave); Set(manager, "NetService", priorNetwork);
            Set(manager, "EventSynchronizer", priorSynchronizer); Set(manager, "ActionExecutor", priorExecutor);
            Set(MegaCrit.Sts2.Core.Combat.CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
        }
        return Task.CompletedTask;
    }

    private static (RunState Run, object Shared) Setup()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Flags)!
            .Invoke(null, [typeof(SocratesBalanceWeight), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        object shared = typeof(NativeQuestionNodeChecks).GetMethod("Shared", Flags)!.Invoke(null,
            ["None", EntryNode, fixture.Relic.Id.Entry])!;
        Call("PhilosophyRunStateService", "Restore", run, shared);
        object journey = Get(shared, "WesternJourney");
        Put(journey, "CurrentNodeId", SpinozaNode);
        Put(journey, "LastFixedAct", 2);
        return (run, shared);
    }

    private static void Edges(object shared, bool include024)
    {
        var completed = (ICollection<string>)Get(Get(shared, "WesternJourney"), "CompletedEdgeIds");
        completed.Clear();
        completed.Add("WESTERN_EDGE_023");
        if (include024) completed.Add("WESTERN_EDGE_024");
    }

    private static void ActCombat(object shared, int act, int count)
    {
        object behavior = Activator.CreateInstance(T("ActBehaviorState"), true)!;
        Put(behavior, "ActIndex", act);
        Put(behavior, "CompletedCombatCount", count);
        ((IDictionary)Get(shared, "ActBehaviorStates"))[act] = behavior;
    }

    private static MapPointHistoryEntry Leibniz() => new()
    {
        MapPointType = MapPointType.Unknown,
        Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<LeibnizMonadComparison>() }],
    };

    private static MapPointHistoryEntry Fight() => new()
    {
        MapPointType = MapPointType.Monster,
        Rooms = [new() { RoomType = RoomType.Monster }],
    };

    private static void History(RunState run, List<MapPointHistoryEntry> second, List<MapPointHistoryEntry> third) =>
        Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new(), second, third });

    private static EventModel? Select(RunState run) =>
        (EventModel?)T("HegelSubstanceSubjectPatch").GetMethod("TrySelectNative", Flags)!.Invoke(null, [run]);

    private static void MissingRequiredEdge()
    {
        var (run, shared) = Setup();
        Edges(shared, include024: false);
        ActCombat(shared, 2, 1);
        History(run, [Leibniz(), Fight()], [Fight()]);
        Check(Select(run) is null, "A journey without WESTERN_EDGE_024 cannot select Hegel 025.");
    }

    private static void CombatBeforePredecessor()
    {
        var (run, shared) = Setup();
        Edges(shared, include024: true);
        ActCombat(shared, 2, 1);
        History(run, [], [Fight(), Leibniz()]);
        Check(Select(run) is null,
            "A third-act fight that precedes Leibniz is not a fight after the predecessor.");
    }

    private static void SecondActFightWithoutThirdActFight()
    {
        var (run, shared) = Setup();
        Edges(shared, include024: true);
        ActCombat(shared, 2, 0);
        History(run, [Leibniz(), Fight()], []);
        Check(Select(run) is null,
            "A second-act fight after Leibniz cannot replace the required third-act combat.");
    }

    private static void ThirdActFightWithoutSecondActFight()
    {
        var (run, shared) = Setup();
        Edges(shared, include024: true);
        ActCombat(shared, 2, 1);
        History(run, [Leibniz()], [Fight()]);
        EventModel? chosen = Select(run);
        Check(chosen is HegelSubstanceSubject && Get(shared, "HegelSubstanceSubject") is not null,
            "The third-act fight itself counts as the fight after the second-act predecessor.");
    }

    private static void ThirdActFightAfterSecondActFight()
    {
        var (run, shared) = Setup();
        Edges(shared, include024: true);
        ActCombat(shared, 2, 1);
        History(run, [Leibniz(), Fight()], [Fight()]);
        EventModel? chosen = Select(run);
        Check(chosen is HegelSubstanceSubject && Get(shared, "HegelSubstanceSubject") is not null,
            "A second-act fight after Leibniz plus a third-act fight selects Hegel 025.");
    }
}
