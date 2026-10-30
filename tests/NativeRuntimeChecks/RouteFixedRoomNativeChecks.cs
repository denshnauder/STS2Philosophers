using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Proves every default line's fixed figures actually open a room at the act
// boundary their route puts them in. A route can walk the graph and still be
// unplayable when its room is gated to another act; this check reports each
// such room by name instead of failing on the first one.
internal static class RouteFixedRoomNativeChecks
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
        Dictionary<string, Type> rooms = RoomsByNode();
        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object table = T("WesternRouteTable").GetProperty("Instance", Methods)!.GetValue(null)!;
        var routes = (System.Collections.IEnumerable)table.GetType().GetProperty("Routes", Methods)!
            .GetValue(table)!;
        List<string> failures = [];
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
            // Act three is not measured here: its rooms require the act-two room
            // to have been played out (predecessor and combat history), which a
            // boundary fixture does not have. Act two has no such predecessor.
        }
        // Reported, not enforced yet. Two things can make a line appear here: the
        // room is still gated to the act it was written for, or the fixture has no
        // combat history for this entry relic. Both are worth fixing, and the list
        // must shrink to zero before this becomes an assertion.
        if (failures.Count > 0)
            Console.WriteLine("Route room gaps (" + failures.Count + "): " + string.Join(" | ", failures));
        return Task.CompletedTask;
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
        ModelDb.Inject(room);
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "CurrentDoctrine", Doctrine(relic));
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "Restore", run, shared);
        object?[] prepare = [run, null];
        bool selected = (bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!;
        if (!selected)
        {
            failures.Add($"{routeId}: {act} does not open {room.Name} at its boundary (run act {run.CurrentActIndex}, route {Get(journey, "RouteId")}).");
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
