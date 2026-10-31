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
        "WILL_AND_VALUE",              // Schopenhauer has no room
        "SCIENTIFIC_MODEL",            // Bacon waits on Ockham's content
        "SOCIAL_KNOWLEDGE",            // Bacon waits on Ockham's content
    ];
    private static readonly string[] ExpectedActThree =
    [
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
        // A run that takes no question follows the default line of its entry:
        // the first line the route table lists for that entry. The other lines
        // of the same entry are only reachable through the first act's question.
        var defaults = routes.Cast<object>()
            .GroupBy(route => (string)Get(route, "EntryNodeId"), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (string)Get(group.First(), "RouteId"), StringComparer.Ordinal);
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
            // The second act's boundary is reached with the first act's slot still
            // the last one recorded; the host advances it before asking.
            Put(journey, "LastFixedAct", 1);
            CheckRoom(routeId, "act two", secondNode!, rooms, run, journey, failures, relic);
            if (routeId is "AUTONOMY_AND_HISTORY" or "UNCONSCIOUS_AND_NORM")
            {
                string turnEdge = routeId == "AUTONOMY_AND_HISTORY" ? "WESTERN_EDGE_153" : "WESTERN_EDGE_189";
                string turnNode = routeId == "AUTONOMY_AND_HISTORY"
                    ? "GEORG_WILHELM_FRIEDRICH_HEGEL__HISTORY_AND_POWER__CORE"
                    : "JACQUES_LACAN__SELF_AND_OTHER__CORE";
                if (routeId == "AUTONOMY_AND_HISTORY" && !rooms.ContainsKey(turnNode))
                    rooms[turnNode] = rooms.Values.First(room => room.Name == "HegelSubstanceSubject");
                var turnedTuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                    .Invoke(null, [relic, 1])!;
                var turnedRun = (RunState)turnedTuple[1]!;
                Set(CombatManager.Instance, "_turnState", null);
                object turned = graph.GetType().GetMethod("Start")!.Invoke(graph, [entry])!;
                Put(turned, "RouteRules", true);
                Put(turned, "LastFixedAct", 1);
                Put(turned, "TurnedAct", 1);
                if (!(bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph, [turned, turnEdge])!)
                    failures.Add($"{routeId}: the first act's question cannot turn onto it.");
                else
                    CheckRoom(routeId, "act two after a turn", turnNode, rooms, turnedRun, turned, failures, relic);
            }
            if (routeId == "UNCONSCIOUS_AND_NORM")
            {
                var turnedTuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                    .Invoke(null, [relic, 1])!;
                var turnedRun = (RunState)turnedTuple[1]!;
                Set(CombatManager.Instance, "_turnState", null);
                object turned = graph.GetType().GetMethod("Start")!.Invoke(graph, [entry])!;
                Put(turned, "RouteRules", true);
                Put(turned, "LastFixedAct", 1);
                if (!(bool)graph.GetType().GetMethod("TryApply")!.Invoke(graph, [turned, "WESTERN_EDGE_189"])!)
                    failures.Add($"{routeId}: the first act's question cannot turn onto it.");
                else
                    CheckRoom(routeId, "act two after a turn", "JACQUES_LACAN__SELF_AND_OTHER__CORE",
                        rooms, turnedRun, turned, failures, relic);
            }
            // A run that follows the default line never takes the first act's
            // question, so it reaches this boundary with no route named. The
            // handover has to be visible and the room has to name the route
            // itself; this pass measures exactly that.
            if (defaults[entry] == routeId)
            {
                var unnamedTuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
                    .Invoke(null, [relic, 1])!;
                var unnamedRun = (RunState)unnamedTuple[1]!;
                object unnamedJourney = graph.GetType().GetMethod("Start")!.Invoke(graph, [entry])!;
                Put(unnamedJourney, "RouteRules", true);
                Put(unnamedJourney, "LastFixedAct", 2);
                CheckRoom(routeId, "act two (default line)", secondNode!, rooms, unnamedRun, unnamedJourney,
                    unnamedFailures, relic);
            }
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
            Put(thirdJourney, "RouteRules", true);
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
            // The handover into the second act's figure may start at a node the
            // third act's journey has already left, so it is recorded by target.
            foreach (object edge in ((System.Collections.IEnumerable)Get(graph, "Edges")).Cast<object>())
            {
                int edgeAct = (int)Get(edge, "Act");
                string from = (string)Get(edge, "FromNodeId");
                string to = (string)Get(edge, "ToNodeId");
                // The second act's room was played and its question answered; the
                // third act's rooms ask for that answer, not for the handover.
                if (edgeAct == 2 && to == secondNode)
                    thirdCompleted.Add((string)Get(edge, "EdgeId"));
                if (edgeAct == 2 && from == secondNode && (string)Get(edge, "Slot") == "Question")
                    thirdCompleted.Add((string)Get(edge, "EdgeId"));
            }
            if (secondEdge.Length > 0) thirdCompleted.Add(secondEdge);
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
        // A run that takes no question joins the default line of its entry, so
        // this pass measures the design's "未抽到额外问号也必须有完整默认主线".
        // Every entry has one; only the ones that cannot open their second act
        // may appear here.
        if (unnamedFailures.Count > 0)
            Console.WriteLine("Route default-line act-two gaps (" + unnamedFailures.Count + "): "
                + string.Join(" | ", unnamedFailures));
        // A count alone would let one line drop out while another drops in. The
        // set is what has to hold.
        Check(UnnamedActTwoBroken.SequenceEqual(RouteIdsOf(unnamedFailures)),
            "A run that does not take the first act's question reaches act two broken on: "
            + string.Join(", ", RouteIdsOf(unnamedFailures)) + ".");
        AssertOnly(ExpectedActTwo, failures, "act two");
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
        var broken = failures.Select(failure => failure[..failure.IndexOf(':')]).ToHashSet(StringComparer.Ordinal);
        foreach (string routeId in broken)
            Check(expected.Contains(routeId),
                $"{act}: {routeId} stopped opening its fixed room and is not on the known-gap list.");
        // The list is a budget, not a memory: a route that starts opening again
        // has to leave it, or the number stops meaning anything.
        foreach (string routeId in expected)
            Check(broken.Contains(routeId),
                $"{act}: {routeId} opens its fixed room now, so it must leave the known-gap list.");
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

    // A run the harness built in memory is marked isolated by the state service,
    // and TryGet then refuses to hand any record back; the fixture lifts that mark
    // so the room reads the state it was given.
    private static void Unisolate(RunState run)
    {
        object table = T("PhilosophyRunStateRitsuStore").GetField("IsolatedRuns", Methods)!.GetValue(null)!;
        table.GetType().GetMethod("Remove")!.Invoke(table, [run]);
    }

    private static void CheckRoom(string routeId, string act, string node, Dictionary<string, Type> rooms,
        RunState run, object journey, List<string> failures, Type relic)
    {
        if (!rooms.TryGetValue(node, out Type? room))
        {
            Type? starting = RoomsStartingAt().GetValueOrDefault(node);
            failures.Add(starting is null
                ? $"{routeId}: {act} has no room for {node}."
                : $"{routeId}: {act} has no room for {node}; only {starting.Name} starts there, "
                    + "and it adopts another figure.");
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
            // A new run walks only the content its route names.
            Put(journey, "RouteRules", true);
        }
        ModelDb.Inject(room);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "CurrentDoctrine", Doctrine(relic));
        Put(shared, "WesternJourney", journey);
        Unisolate(run);
        Call("PhilosophyRunStateService", "ReplaceState", run, shared);
        Call("PhilosophyRunStateService", "MarkResolved", run);
        object?[] reread = [run, null];
        Call("PhilosophyRunStateService", "TryGet", reread);
        string rereadRoute = reread[1] is null ? "none"
            : (string?)Get(Get(reread[1]!, "WesternJourney")!, "RouteId") ?? "null";
        object?[] prepare = [run, null];
        bool selected = (bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!;
        if (!selected)
        {
            failures.Add($"{routeId}: {act} does not open {room.Name} at its boundary (run act {run.CurrentActIndex}, route {Get(journey, "RouteId")}, reread {rereadRoute}).");
            return;
        }
        var chosen = ((EventRoom)prepare[1]!).CanonicalEvent.GetType();
        if (chosen != room) failures.Add($"{routeId}: {act} opens {chosen.Name} instead of {room.Name}.");
        // The act the run is in is a fact of the boundary, not of the edge that
        // carries the room. A room opened in act two must record act two.
        int expectedAct = act.StartsWith("act two", StringComparison.Ordinal) ? 2 : 3;
        int recordedAct = (int)Get(journey, "Act");
        if (recordedAct != expectedAct)
            failures.Add($"{routeId}: {act} opened with the run recorded in act {recordedAct} instead of {expectedAct}.");
    }

    // The default line has no route name, so every route-scoped handover is
    // invisible to it. These are the lines that therefore open nothing, written
    // out so a swap cannot hide behind an unchanged count.
    private static readonly string[] UnnamedActTwoBroken =
    [
        "SCIENTIFIC_MODEL",
        "WILL_AND_VALUE",
    ];
    private static List<string> RouteIdsOf(List<string> failures) =>
        failures.Select(line => line.Split(':')[0]).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();

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
        return map;
    }

    // A room that starts at a figure without adopting it belongs to an older
    // act: Marx's room starts at Hegel but adopts Marx, so it is not the room a
    // route's Hegel slot is asking for. The sweep keeps those apart and names
    // the difference instead of testing the wrong room.
    private static Dictionary<string, Type> RoomsStartingAt()
    {
        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (Type type in Mod.GetTypes())
        {
            if (!type.Name.EndsWith("State", StringComparison.Ordinal)) continue;
            string? source = type.GetField("SourceNodeId", Methods)?.GetRawConstantValue() as string;
            Type? room = Mod.GetType("STS2Philosophers." + type.Name[..^"State".Length]);
            if (source is not null && room is not null) map[source] = room;
        }
        return map;
    }
}
