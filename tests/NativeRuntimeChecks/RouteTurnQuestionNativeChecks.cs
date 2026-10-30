using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Philosophers;

// Proves the first act's question actually opens at each entry that needs it and
// that answering it names the line. The graph can name every line and still leave
// a run with no room to answer in, so this check pulls the real native event.
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

    // entry, relic, the edge the answer takes, the line it must name, and whether
    // the shared figure needs the direction question to name the line.
    // entry, relic, the edge the answer takes, the line it must name, whether the
    // shared figure needs the direction question, and the entry's own rooms that a
    // run may already have played at an earlier question node.
    private static readonly (string Node, Type Relic, string Edge, string Route, bool Direction, string[] Prior)[] Cases =
    [
        ("DESCARTES__SELF_AND_OTHER__CORE", typeof(DescartesThinkingLens), "WESTERN_EDGE_153", "AUTONOMY_AND_HISTORY", false, []),
        ("DESCARTES__SELF_AND_OTHER__CORE", typeof(DescartesThinkingLens), "WESTERN_EDGE_189", "UNCONSCIOUS_AND_NORM", false, []),
        ("DESCARTES__SELF_AND_OTHER__CORE", typeof(DescartesThinkingLens), "WESTERN_EDGE_160", "EMBODIED_PERCEPTION", true, []),
        ("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE", typeof(SocratesQuestionCup), "WESTERN_EDGE_165", "SCIENTIFIC_MODEL", false, ["SextusOpposedAppearances"]),
        ("ROUSSEAU__HISTORY_AND_POWER__CORE", typeof(RousseauUncarvedStone), "WESTERN_EDGE_197", "SOCIAL_KNOWLEDGE", false, ["MillHarm"]),
    ];

    public static Task Run()
    {
        ModelDb.Inject(T("RouteTurnQuestion"));
        foreach (var item in Cases)
        {
            var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                .Invoke(null, [item.Relic, 0])!;
            var run = (RunState)tuple[1]!;
            object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
            object journey = graph.GetType().GetMethod("Start")!.Invoke(graph, [item.Node])!;
            Put(journey, "RouteRules", true);
            Put(journey, "LastFixedAct", 1);
            object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
            object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
            string id = Snake(item.Relic.Name);
            Put(doctrine, "ThinkerId", id.Split('_')[0]);
            Put(doctrine, "DoctrineId", id);
            Put(shared, "CurrentDoctrine", doctrine);
            Put(shared, "WesternJourney", journey);
            object table = T("PhilosophyRunStateRitsuStore").GetField("IsolatedRuns", Methods)!.GetValue(null)!;
            table.GetType().GetMethod("Remove")!.Invoke(table, [run]);
            Call("PhilosophyRunStateService", "ReplaceState", run, shared);
            Call("PhilosophyRunStateService", "MarkResolved", run);
            // A run reaches later question nodes with the entry's own rooms behind
            // it; those rooms keep their own node and are not asked again.
            foreach (string prior in item.Prior)
                ((HashSet<ModelId>)run.VisitedEventIds).Add((ModelId)typeof(ModelDb).GetMethods(Methods)
                    .Single(method => method.Name == "GetId" && method.IsGenericMethodDefinition
                        && method.GetParameters().Length == 0)
                    .MakeGenericMethod(T(prior)).Invoke(null, null)!);
            Set(run, "_currentRooms", new List<AbstractRoom>());
            run.CurrentMapPoint!.PointType = MapPointType.Unknown;

            EventModel chosen = run.Act.PullNextEvent(run);
            Check(chosen.GetType() == T("RouteTurnQuestion"),
                $"{item.Node}: the entry's question must open as RouteTurnQuestion, got {chosen.GetType().Name}.");
            Check(run.VisitedEventIds.Contains(chosen.Id),
                $"{item.Node}: the question's identity must be visited without a second event.");

            // A fork left open is still there after a load: the node it was asked
            // on is the only place the answer can be given, so losing it loses the
            // line. Closing the record is what makes it stop coming back.
            object?[] live = [run, null];
            Call("PhilosophyRunStateService", "TryGet", live);
            object record = Get(live[1]!, "RouteTurnQuestion")!;
            Check(record is not null && !(bool)Get(record, "Closed"),
                $"{item.Node}: presenting the question must leave an open record.");
            // A checkpoint may record the question without its room: the record is
            // then the only thing left, and it has to bring the question back.
            ((HashSet<ModelId>)run.VisitedEventIds).Clear();
            Check(Call("RouteTurnQuestionPatch", "TrySelectExistingNative", run) is not null,
                $"{item.Node}: an open question must resume on its own node.");
            Put(record, "Closed", true);
            Check(Call("RouteTurnQuestionPatch", "TrySelectExistingNative", run) is null,
                $"{item.Node}: an answered question must not come back.");
            Put(record, "Closed", false);

            // The fork is asked once per run: an answered record means the run
            // already chose, so the next question node must not ask it again.
            Put(record, "Closed", true);
            ((HashSet<ModelId>)run.VisitedEventIds).Clear();
            Check(Call("RouteTurnQuestionPatch", "TrySelectNative", run) is null,
                $"{item.Node}: an answered fork must not be asked again.");
            Put(record, "Closed", false);

            // Answering is the graph's own step: the edge names the line, and the
            // shared figure's edge is followed by the direction the answer gives.
            Check((bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph, [journey, item.Edge])!,
                $"{item.Node}: {item.Edge} must be an eligible first-act edge.");
            if (item.Direction)
                Check((bool)graph.GetType().GetMethod("TryNameRoute")!.Invoke(graph, [journey, item.Route])!,
                    $"{item.Node}: the direction must be nameable.");
            Check((string)Get(journey, "RouteId") == item.Route,
                $"{item.Node}: answering with {item.Edge} must name {item.Route}, got {Get(journey, "RouteId")}.");
            Console.WriteLine($"Route turn question: {item.Node} opens the question and {item.Edge} names {item.Route}.");
        }
        return Task.CompletedTask;
    }

    private static string Snake(string name)
    {
        var text = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && text.Length > 0) text.Append('_');
            text.Append(char.ToUpperInvariant(c));
        }
        return text.ToString();
    }
}
