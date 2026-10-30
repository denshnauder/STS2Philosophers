using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Proves the act-boundary entry: with the journey at the second act's fixed
// position, a room is selected without any unknown map point, and the same
// run gets nothing once the position is spent. The run is an in-memory fixture.
internal static class ActBoundaryFixedEntryNativeChecks
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Assembly Mod = typeof(SpinozaCauseNetwork).Assembly;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) => T(type).GetMethods(Methods)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length).Invoke(null, args);
    private static object Get(object value, string name) => value.GetType().GetProperty(name, Methods)!.GetValue(value)!;
    private static void Put(object value, string name, object? item) => value.GetType().GetProperty(name, Methods)!.SetValue(value, item);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static Task Run()
    {
        ModelDb.Inject(typeof(SpinozaCauseNetwork));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE", [typeof(SpinozaCauseNetwork)], doctrine: true, position: 2);
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE", [typeof(SpinozaCauseNetwork)], doctrine: true, position: 1);
        ModelDb.Inject(typeof(EpicurusSharedMargin));
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__VIRTUE_AND_HAPPINESS__CORE", [typeof(EpicurusSharedMargin)], doctrine: false, position: 3, actIndex: 2, required: ["WESTERN_EDGE_009"]);
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__VIRTUE_AND_HAPPINESS__CORE", [typeof(EpicurusSharedMargin)], doctrine: false, position: 2, actIndex: 2, required: ["WESTERN_EDGE_009"]);
        ModelDb.Inject(typeof(MontesquieuBalance));
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(MontesquieuBalance)], doctrine: false, position: 2, route: "CHECKS_AND_JUSTICE");
        ModelDb.Inject(typeof(SmithSympathy));
        CheckBoundary(typeof(SocratesBalanceWeight), "PLATO__FREEDOM_AND_INSTITUTIONS__CORE",
            [typeof(SmithSympathy)], doctrine: false, position: 2, route: "SYMPATHY_AND_PUBLIC_ACTION");
        ModelDb.Inject(typeof(HusserlEpoche));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(HusserlEpoche)], doctrine: true, position: 2, route: "APPEARANCE_AND_INTERPRETATION");
        ModelDb.Inject(typeof(FregeSense));
        CheckBoundary(typeof(SocratesBalanceWeight), "ARISTOTLE__LANGUAGE_AND_MEANING__CORE",
            [typeof(FregeSense)], doctrine: false, position: 2, route: "FORMAL_LANGUAGE");
        ModelDb.Inject(typeof(LockeCargoIdentity));
        CheckBoundary(typeof(DescartesThinkingLens), "DESCARTES__SELF_AND_OTHER__CORE",
            [typeof(LockeCargoIdentity)], doctrine: true, position: 2, route: "EXPERIENCE_AND_SELF");
        ModelDb.Inject(typeof(MachiavelliFloodPreparation));
        CheckBoundary(typeof(RousseauUncarvedStone), "ROUSSEAU__HISTORY_AND_POWER__CORE", [typeof(MachiavelliFloodPreparation), typeof(WollstonecraftAdmission)], doctrine: false, position: 2);
        return Task.CompletedTask;
    }

    private static void CheckBoundary(Type relic, string node, Type[] expectedRooms, bool doctrine, int position, int actIndex = 1, string[]? required = null, string? route = null)
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [relic, actIndex])!;
        var run = (RunState)tuple[1]!;
        Set(CombatManager.Instance, "_turnState", null);

        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey;
        try { journey = graph.GetType().GetMethod("Start")!.Invoke(graph, [node])!; }
        catch (TargetInvocationException)
        {
            journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["SOCRATES__VIRTUE_AND_HAPPINESS__CORE"])!;
            Put(journey, "CurrentNodeId", node);
        }
        if (route is not null) Put(journey, "RouteId", route);
        var completed = (HashSet<string>)Get(journey, "CompletedEdgeIds");
        foreach (string edge in required ?? []) completed.Add(edge);
        Put(journey, "LastFixedAct", position);
        object? doctrineState = doctrine
            ? Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!
            : null;
        if (doctrineState is not null)
        {
            Put(doctrineState, "ThinkerId", "DESCARTES");
            Put(doctrineState, "DoctrineId", "DESCARTES_THINKING_LENS");
        }
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "CurrentDoctrine", doctrineState);
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "Restore", run, shared);

        object?[] prepare = [run, null];
        bool selected = (bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!;
        var edges = (System.Collections.IEnumerable)graph.GetType().GetMethod("GetEligibleEdges")!
            .Invoke(graph, [journey, "Fixed"])!;
        string open = string.Join(",", edges.Cast<object>().Select(edge => Get(edge, "EdgeId") + "@" + Get(edge, "Act")));
        Check(selected, string.Join("/", expectedRooms.Select(room => room.Name))
            + ": the second act's fixed room must be selectable at the ancient boundary without a question point. Open: " + open);
        var room = (EventRoom)prepare[1]!;
        Type? expected = expectedRooms.FirstOrDefault(candidate => candidate == room.CanonicalEvent.GetType());
        Check(expected is not null,
            "The boundary selects a room that owns this act's fixed edge, got " + room.CanonicalEvent.GetType().Name + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object current = Get(query[1]!, expected.Name);
        Check(current is not null && (bool)current.GetType().GetMethod("IsUsable")!.Invoke(current, null)!,
            "The boundary selection persists a usable room state for save recovery.");

        Put(journey, "LastFixedAct", actIndex + 2);
        object spent = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(spent, "CurrentDoctrine", doctrineState);
        Put(spent, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "Restore", run, spent);
        object?[] again = [run, null];
        Check(!(bool)Call("ActFixedEntryHost", "TryPrepare", again)!,
            "A spent fixed position must not present a second room at the same boundary.");
    }
}
