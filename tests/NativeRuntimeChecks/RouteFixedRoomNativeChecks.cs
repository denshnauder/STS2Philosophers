using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Proves every default line's fixed figures actually open a room at the act
// boundary their route puts them in. A route can walk the graph and still be
// unplayable when its room is gated to another act; this check reports each
// such room by name instead of failing on the first one.
internal static class RouteFixedRoomNativeChecks
{
    // Lines that are not implementation gaps: each one waits on a design
    // decision or on content that does not exist yet. Anything outside these
    // lists fails the check, so a room that stops opening can no longer slip
    // through as a printed number.
    private static readonly string[] ExpectedActTwo =
    [
        "NATURE_AND_BECOMING",         // Spinoza waits on a design decision (lens and doctrine)
        "WILL_AND_VALUE",              // Schopenhauer has no room
        "UNCONSCIOUS_AND_NORM",        // no fixed edge carries act two to Freud
        "CONSEQUENCE_AND_FREEDOM",     // Bentham has no incoming edge
        "SCIENTIFIC_MODEL",            // Bacon waits on Ockham's content
        "SOCIAL_KNOWLEDGE",            // Bacon waits on Ockham's content
    ];
    private static readonly string[] ExpectedActThree =
    [
        "AUTONOMY_AND_HISTORY",        // closing-figure placement, waiting on the design call
        "EDUCATION_AND_SITUATION",     // closing-figure placement
        "CHECKS_AND_JUSTICE",          // closing-figure placement
        "SCIENTIFIC_MODEL",            // closing-figure placement
        "CONSEQUENCE_AND_FREEDOM",     // Mill has no room
        "SYMPATHY_AND_PUBLIC_ACTION",  // Mill has no room
    ];

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
        Dictionary<string, Type> rooms = RoomsByNode();
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object table = T("WesternRouteTable").GetProperty("Instance", Methods)!.GetValue(null)!;
        var routes = (System.Collections.IEnumerable)table.GetType().GetProperty("Routes", Methods)!
            .GetValue(table)!;
        List<string> failures = [];
        List<string> thirdFailures = [];
        List<string> unnamedFailures = [];
        List<string> unnamedThirdFailures = [];
        List<List<MapPointHistoryEntry>> thirdHistory = [];
        foreach (object route in routes.Cast<object>())
        {
            string routeId = (string)Get(route, "RouteId");
            string entry = (string)Get(route, "EntryNodeId");
            string second = (string)Get(route, "ActTwoFigure");
            string third = (string)Get(route, "ActThreeFigure");
            if (second == third) continue;
            string entryThinker = entry.Split("__")[0];
            Type relic = EntryRelics.GetValueOrDefault(entryThinker, typeof(PlatoCivicSeal));
            var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                .Invoke(null, [relic, 1])!;
            var run = (RunState)tuple[1]!;
            Set(CombatManager.Instance, "_turnState", null);
            object journey;
            try { journey = graph.GetType().GetMethod("Start")!.Invoke(graph, [entry])!; }
            catch (TargetInvocationException)
            {
                failures.Add($"{routeId}: the entry node {entry} is not an approved entry.");
                continue;
            }
            Put(journey, "RouteId", routeId);
            // Act two opens from the entry node: the room itself commits the edge.
            string? secondEdge = FindEdge(graph, journey, 2, second, out string? secondNode);
            if (secondEdge is null)
            {
                failures.Add($"{routeId}: no fixed edge carries act two to {second}.");
                continue;
            }
            Put(journey, "LastFixedAct", 2);
            CheckRoom(routeId, "act two", secondNode!, rooms, run, journey, failures, relic);
            // A run that follows the default line never takes the first act's
            // question, so it reaches this boundary with no route named. The
            // handover has to be visible and the room has to name the route
            // itself; this pass measures exactly that.
            var unnamedTuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                .Invoke(null, [relic, 1])!;
            var unnamedRun = (RunState)unnamedTuple[1]!;
            object unnamedJourney = graph.GetType().GetMethod("Start")!.Invoke(graph, [entry])!;
            Put(unnamedJourney, "LastFixedAct", 2);
            CheckRoom(routeId, "act two (unnamed)", secondNode!, rooms, unnamedRun, unnamedJourney,
                unnamedFailures, relic);
            // Act three opens only after act two's room has been played out, and
            // its boundary belongs to the third act, so the fixture is rebuilt on
            // that act and the two handovers are replayed into it.
            var thirdTuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                .Invoke(null, [relic, 2])!;
            var thirdRun = (RunState)thirdTuple[1]!;
            Set(CombatManager.Instance, "_turnState", null);
            // The second act's room was played, and a fight followed it. Those
            // are facts of a real run that the third act's rooms may ask about,
            // so the fixture records them instead of the rooms ignoring them.
            ModelId? secondRoomId = rooms.TryGetValue(secondNode!, out Type? secondRoomType)
                ? (ModelId)typeof(ModelDb).GetMethods(Methods)
                    .Single(method => method.Name == "GetId" && method.IsGenericMethodDefinition
                        && method.GetParameters().Length == 0)
                    .MakeGenericMethod(secondRoomType).Invoke(null, null)!
                : null;
            thirdHistory = new List<List<MapPointHistoryEntry>>
            {
                new(),
                secondRoomId is null
                    ? new() { new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] } }
                    : new()
                    {
                        new() { MapPointType = MapPointType.Unknown,
                            Rooms = [new() { RoomType = RoomType.Event, ModelId = secondRoomId }] },
                        new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] },
                    },
                new() { new() { MapPointType = MapPointType.Monster, Rooms = [new() { RoomType = RoomType.Monster }] } },
            };
            Set(thirdRun, "_mapPointHistory", thirdHistory);
            object thirdJourney = graph.GetType().GetMethod("Start")!.Invoke(graph, [entry])!;
            Put(thirdJourney, "RouteId", routeId);
            // The act-one handover into the figure that opens act two is part of
            // the room chain act three inherits, so it is recorded as completed.
            string secondFrom = "";
            foreach (object edge in ((System.Collections.IEnumerable)Get(graph, "Edges")).Cast<object>())
                if ((string)Get(edge, "EdgeId") == secondEdge) secondFrom = (string)Get(edge, "FromNodeId");
            var thirdCompleted = (HashSet<string>)Get(thirdJourney, "CompletedEdgeIds");
            foreach (object edge in ((System.Collections.IEnumerable)Get(graph, "Edges")).Cast<object>())
            {
                string to = (string)Get(edge, "ToNodeId");
                if ((int)Get(edge, "Act") == 1 && (to == secondNode || to == secondFrom))
                    thirdCompleted.Add((string)Get(edge, "EdgeId"));
            }
            thirdCompleted.Add(secondEdge);
            ((HashSet<string>)Get(thirdJourney, "SeenThinkerIds")).Add(second);
            ((HashSet<int>)Get(thirdJourney, "ResolvedQuestionActs")).Add(2);
            Put(thirdJourney, "CurrentNodeId", secondNode);
            // At the boundary the previous act's slot is still the last one
            // recorded; the room itself advances it when it commits.
            Put(thirdJourney, "LastFixedAct", 2);
            string? thirdEdge = FindEdge(graph, thirdJourney, 3, third, out string? thirdNode);
            if (thirdEdge is null)
            {
                thirdFailures.Add($"{routeId}: no fixed edge carries act three to {third}.");
                continue;
            }
            CheckRoom(routeId, "act three", thirdNode!, rooms, thirdRun, thirdJourney, thirdFailures, relic);
            // The same act three boundary, with the route still unnamed: a run
            // that never took the first act's question has to reach it too.
            var unnamedThirdTuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                .Invoke(null, [relic, 2])!;
            var unnamedThirdRun = (RunState)unnamedThirdTuple[1]!;
            Set(unnamedThirdRun, "_mapPointHistory", new List<List<MapPointHistoryEntry>>(thirdHistory));
            object unnamedThird = graph.GetType().GetMethod("Start")!.Invoke(graph, [entry])!;
            foreach (string done in thirdCompleted) ((HashSet<string>)Get(unnamedThird, "CompletedEdgeIds")).Add(done);
            foreach (string seen in (HashSet<string>)Get(thirdJourney, "SeenThinkerIds"))
                ((HashSet<string>)Get(unnamedThird, "SeenThinkerIds")).Add(seen);
            ((HashSet<int>)Get(unnamedThird, "ResolvedQuestionActs")).Add(2);
            Put(unnamedThird, "CurrentNodeId", secondNode);
            Put(unnamedThird, "LastFixedAct", 2);
            CheckRoom(routeId, "act three (unnamed)", thirdNode!, rooms, unnamedThirdRun, unnamedThird,
                unnamedThirdFailures, relic);
        }
        // Reported, not enforced yet. Two things can make a line appear here: the
        // room is still gated to the act it was written for, or the fixture has no
        // combat history for this entry relic. Both are worth fixing, and the list
        // must shrink to zero before this becomes an assertion.
        if (failures.Count > 0)
            Console.WriteLine("Route room gaps (" + failures.Count + "): " + string.Join(" | ", failures));
        // Act three is measured separately: its rooms still expect the second
        // act's room to have been played out, which this fixture cannot do yet.
        if (thirdFailures.Count > 0)
            Console.WriteLine("Route act-three gaps (" + thirdFailures.Count + "): "
                + string.Join(" | ", thirdFailures));
        // Seventeen of twenty routes cannot reach their second act when the run
        // follows the default line: eleven open another figure's room and six open
        // none. The number is asserted so it cannot drift unnoticed, and the day
        // act one names the route it has to fall.
        if (unnamedFailures.Count > 0)
            Console.WriteLine("Route unnamed-act-two gaps (" + unnamedFailures.Count + "): "
                + string.Join(" | ", unnamedFailures));
        Check(unnamedFailures.Count == 17,
            $"A run that does not take the first act's question reaches act two with {unnamedFailures.Count} "
            + "routes broken, not the known seventeen.");
        AssertOnly(ExpectedActTwo, failures, "act two");
        if (unnamedThirdFailures.Count > 0)
            Console.WriteLine("Route unnamed-act-three gaps (" + unnamedThirdFailures.Count + "): "
                + string.Join(" | ", unnamedThirdFailures));
        // Eighteen of twenty routes open no third act at all without a route name:
        // the third act's rooms are all route-scoped, so an unnamed run sees none
        // of them. Asserted so the day act one names the route it has to fall.
        Check(unnamedThirdFailures.Count == 18,
            $"A run that does not take the first act's question reaches act three with {unnamedThirdFailures.Count} "
            + "routes broken, not the known eighteen.");
        AssertOnly(ExpectedActThree, thirdFailures, "act three");
        CheckFirstActBranch(graph);
        return Task.CompletedTask;
    }

    // A run without route identity still has to pick one first-act branch. The
    // branch is asked about with the node a room opens from, so exactly one
    // destination may answer yes; asking with a room's own act-two or act-three
    // edge could only ever answer no.
    private static void CheckFirstActBranch(object graph)
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 1])!;
        var run = (RunState)tuple[1]!;
        foreach (object node in ((System.Collections.IEnumerable)Get(graph, "Nodes")).Cast<object>())
        {
            string nodeId = (string)Get(node, "NodeId");
            object journey;
            try { journey = graph.GetType().GetMethod("Start")!.Invoke(graph, [nodeId])!; }
            catch (TargetInvocationException) { continue; }
            var branch = ((System.Collections.IEnumerable)graph.GetType().GetMethod("GetEligibleEdges")!
                .Invoke(graph, [journey, "Fixed"])!).Cast<object>()
                .Where(edge => (int)Get(edge, "Act") == 1)
                .ToList();
            var destinations = branch.Select(edge => (string)Get(edge, "ToNodeId")).Distinct().ToList();
            if (destinations.Count < 2) continue;
            int taken = destinations.Count(destination =>
                (bool)Call("WesternFirstActBranch", "IsOnBranch", run, journey, graph, destination)!);
            Check(taken == 1, $"{nodeId}: the first act must take exactly one branch, but {taken} answered yes.");
        }
    }

    private static void AssertOnly(string[] expected, List<string> failures, string act)
    {
        foreach (string failure in failures)
        {
            string routeId = failure[..failure.IndexOf(':')];
            Check(expected.Contains(routeId),
                $"{act}: {routeId} stopped opening its fixed room and is not on the known-gap list: {failure}");
        }
    }

    private static string? FindEdge(object graph, object journey, int act, string thinker, out string? target)
    {
        target = null;
        var edges = ((System.Collections.IEnumerable)graph.GetType().GetMethod("GetEligibleEdges")!
            .Invoke(graph, [journey, "Fixed"])!).Cast<object>();
        foreach (object edge in edges)
        {
            if ((int)Get(edge, "Act") != act) continue;
            string to = (string)Get(edge, "ToNodeId");
            object node = graph.GetType().GetMethod("GetNode")!.Invoke(graph, [to])!;
            if ((string)Get(node, "ThinkerId") != thinker) continue;
            target = to;
            return (string)Get(edge, "EdgeId");
        }
        return null;
    }

    private static void CheckRoom(string routeId, string act, string node, Dictionary<string, Type> rooms,
        RunState run, object journey, List<string> failures, Type relic)
    {
        if (!rooms.TryGetValue(node, out Type? room))
        {
            failures.Add($"{routeId}: {act} has no room for {node}.");
            return;
        }
        // Act one's question has been answered by the time act two opens; the
        // handover edge itself belongs to act one's own room and is not needed to
        // ask whether this act's boundary opens the figure's room.
        if (act == "act two")
        {
            object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
            string from = "";
            foreach (object edge in ((System.Collections.IEnumerable)graph.GetType().GetMethod("GetEligibleEdges")!
                .Invoke(graph, [journey, "Fixed"])!).Cast<object>())
            {
                if ((int)Get(edge, "Act") == 2 && (string)Get(edge, "ToNodeId") == node)
                    from = (string)Get(edge, "FromNodeId");
            }
            if (from.Length > 0)
                foreach (object edge in ((System.Collections.IEnumerable)graph.GetType().GetMethod("GetEligibleEdges")!
                    .Invoke(graph, [journey, "Fixed"])!).Cast<object>())
                {
                    if ((int)Get(edge, "Act") != 1 || (string)Get(edge, "ToNodeId") != from) continue;
                    graph.GetType().GetMethod("TryApply")!.Invoke(graph, [journey, (string)Get(edge, "EdgeId")]);
                    break;
                }
            ((HashSet<int>)Get(journey, "ResolvedQuestionActs")).Add(1);
            Put(journey, "LastFixedAct", 2);
            // Walking the handover may rewrite the route, so name it last.
            Put(journey, "RouteId", routeId);
        }
        ModelDb.Inject(room);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "CurrentDoctrine", Doctrine(relic));
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        object?[] reread = [run, null];
        Call("PhilosophyRunStateService", "TryGet", reread);
        string rereadRoute = reread[1] is null ? "none"
            : (string?)Get(Get(reread[1]!, "WesternJourney")!, "RouteId") ?? "null";
        object?[] prepare = [run, null];
        bool selected = (bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!;
        // The act's window holds two positions; a room may open at either, so the
        // other one is tried before the line is reported.
        if (!selected && act == "act three")
        {
            Put(journey, "LastFixedAct", 3);
            Call("PhilosophyRunStateService", "Restore", run, shared);
            prepare = [run, null];
            selected = (bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!;
        }
        if (!selected)
        {
            failures.Add($"{routeId}: {act} does not open {room.Name} at its boundary (run act {run.CurrentActIndex}, route {Get(journey, "RouteId")}, reread {rereadRoute}).");
            return;
        }
        var chosen = ((EventRoom)prepare[1]!).CanonicalEvent.GetType();
        if (chosen != room) failures.Add($"{routeId}: {act} opens {chosen.Name} instead of {room.Name}.");
    }

    private static readonly Dictionary<string, Type> EntryRelics = new(StringComparer.Ordinal)
    {
        ["DESCARTES"] = typeof(DescartesThinkingLens),
        ["PLATO"] = typeof(PlatoCivicSeal),
        ["SOCRATES"] = typeof(SocratesBalanceWeight),
        ["ARISTOTLE"] = typeof(AristotleCategoryTablet),
        ["HERACLITUS"] = typeof(HeraclitusLyre),
        ["ROUSSEAU"] = typeof(RousseauUncarvedStone),
    };

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

    private static object Doctrine(Type relic)
    {
        object state = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
        string id = Snake(relic.Name);
        Put(state, "ThinkerId", id.Split('_')[0]);
        Put(state, "DoctrineId", id);
        return state;
    }

    private static Dictionary<string, Type> RoomsByNode()
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        // A room owns the figure it adopts; the node it starts from is only a
        // fallback, because several rooms start at the same question node.
        foreach (Type type in Mod.GetTypes())
        {
            if (!type.Name.EndsWith("State", StringComparison.Ordinal)) continue;
            string? adopted = type.GetField("AdoptedNodeId", Methods)?.GetRawConstantValue() as string;
            Type? room = Mod.GetType("STS2Philosophers." + type.Name[..^"State".Length]);
            if (adopted is not null && room is not null) map[adopted] = room;
        }
        foreach (Type type in Mod.GetTypes())
        {
            if (!type.Name.EndsWith("State", StringComparison.Ordinal)) continue;
            string? source = type.GetField("SourceNodeId", Methods)?.GetRawConstantValue() as string;
            Type? room = Mod.GetType("STS2Philosophers." + type.Name[..^"State".Length]);
            if (source is not null && room is not null && !map.ContainsKey(source)) map[source] = room;
        }
        return map;
    }
}
