using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Philosophers;

// Proves the first act's question actually opens at the entry and that answering
// it names the line. The graph can name every line and still leave a run with no
// room to answer in, so this check pulls the real native event.
internal static class RouteTurnQuestionNativeChecks
{
    private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Assembly Mod = typeof(DescartesThinkingLens).Assembly;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) => T(type).GetMethods(Methods)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length).Invoke(null, args);
    private static object Get(object value, string name) => value.GetType().GetProperty(name, Methods)!.GetValue(value)!;
    private static void Put(object value, string name, object? item) => value.GetType().GetProperty(name, Methods)!.SetValue(value, item);
    private static void Set(object value, string name, object? item) => value.GetType().GetField(name, Methods)!.SetValue(value, item);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static Task Run()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 0])!;
        var run = (RunState)tuple[1]!;
        ModelDb.Inject(T("RouteTurnQuestion"));
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, ["DESCARTES__SELF_AND_OTHER__CORE"])!;
        Put(journey, "RouteRules", true);
        Put(journey, "LastFixedAct", 1);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
        Put(doctrine, "ThinkerId", "DESCARTES");
        Put(doctrine, "DoctrineId", "DESCARTES_THINKING_LENS");
        Put(shared, "CurrentDoctrine", doctrine);
        Put(shared, "WesternJourney", journey);
        object table = T("PhilosophyRunStateRitsuStore").GetField("IsolatedRuns", Methods)!.GetValue(null)!;
        table.GetType().GetMethod("Remove")!.Invoke(table, [run]);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        run.CurrentMapPoint!.PointType = MapPointType.Unknown;

        EventModel chosen = run.Act.PullNextEvent(run);
        Check(chosen.GetType() == T("RouteTurnQuestion"),
            $"The entry's question must open as RouteTurnQuestion, got {chosen.GetType().Name}.");
        Check(run.VisitedEventIds.Contains(chosen.Id),
            "The question's identity must be visited without a second event.");

        // Answering is the graph's own step: the edge names the line.
        MethodInfo apply = graph.GetType().GetMethod("TryApply")!;
        Check((bool)apply.Invoke(graph, [journey, "WESTERN_EDGE_153"])!,
            "The question's Kant option must be an eligible first-act edge.");
        Check((string)Get(journey, "RouteId") == "AUTONOMY_AND_HISTORY",
            $"Answering with Kant must name autonomy, got {Get(journey, "RouteId")}.");
        Console.WriteLine("Route turn question: the entry opens RouteTurnQuestion and answering with Kant names "
            + "AUTONOMY_AND_HISTORY.");
        return Task.CompletedTask;
    }
}
