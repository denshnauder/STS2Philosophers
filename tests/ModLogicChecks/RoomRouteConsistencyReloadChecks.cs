using System.Reflection;
using STS2Philosophers;

// A room is resumed after a reload only when IsRouteConsistent still holds for
// it. A field that does not survive the save can flip that answer, and the room
// then refuses to come back even though the player never did anything wrong.
// Every room state is therefore asked the same question before and after a
// save, and both answers have to agree.
internal static class RoomRouteConsistencyReloadChecks
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static object? Create(Type type)
    {
        MethodInfo? create = type.GetMethods(All)
            .Where(method => method.ReturnType == type && method.GetParameters().Length > 0)
            .OrderBy(method => method.Name is "Create" or "CreateAt" ? 0 : 1)
            .ThenBy(method => method.GetParameters().Length)
            .FirstOrDefault();
        if (create is null) return null;
        object?[] arguments = create.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(int) ? (object?)2
                : parameter.ParameterType == typeof(string) ? $"CONSISTENCY:{type.Name}:1" : null)
            .ToArray();
        if (arguments.Any(argument => argument is null)) return null;
        try { return create.Invoke(null, arguments); }
        catch (TargetInvocationException) { return null; }
    }

    private static List<PropertyInfo>? FindPath(Type target)
    {
        var queue = new Queue<(Type Type, List<PropertyInfo> Path)>();
        queue.Enqueue((typeof(PhilosophyRunState), []));
        var seen = new HashSet<Type> { typeof(PhilosophyRunState) };
        while (queue.Count > 0)
        {
            (Type current, List<PropertyInfo> path) = queue.Dequeue();
            foreach (PropertyInfo property in current.GetProperties(All))
            {
                if (!property.CanWrite || property.GetIndexParameters().Length > 0) continue;
                Type next = property.PropertyType;
                List<PropertyInfo> nextPath = [.. path, property];
                if (next == target) return nextPath;
                if (next.Assembly != typeof(PhilosophyRunState).Assembly || next.IsValueType
                    || !seen.Add(next) || nextPath.Count >= 3) continue;
                queue.Enqueue((next, nextPath));
            }
        }
        return null;
    }

    private static object? Walk(object? owner, List<PropertyInfo> path)
    {
        object? node = owner;
        foreach (PropertyInfo property in path)
        {
            if (node is null) return null;
            node = property.GetValue(node);
        }
        return node;
    }

    private static bool? Verdict(MethodInfo method, object state, WesternJourneyState journey)
    {
        try { return (bool)method.Invoke(null, [state, journey])!; }
        catch (TargetInvocationException) { return null; }
    }

    public static void Run()
    {
        var types = typeof(PhilosophyRunState).Assembly.GetTypes()
            .Where(type => type.Namespace == "STS2Philosophers" && type.IsClass && !type.IsAbstract
                && type.Name.EndsWith("State", StringComparison.Ordinal))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();
        var flows = typeof(PhilosophyRunState).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(All))
            .Where(method => method.Name == "IsRouteConsistent" && method.IsStatic
                && method.GetParameters().Length == 2
                && method.GetParameters()[1].ParameterType == typeof(WesternJourneyState))
            .ToDictionary(method => method.GetParameters()[0].ParameterType, method => method);
        Check(flows.Count >= 50, $"Only {flows.Count} rooms can be asked whether a route still holds.");
        var flipped = new List<string>();
        var compared = 0;
        var skipped = new List<string>();
        foreach (Type type in types)
        {
            if (!flows.TryGetValue(type, out MethodInfo? flow)) continue;
            List<PropertyInfo>? path = FindPath(type);
            if (path is null) { skipped.Add(type.Name + ": no path"); continue; }
            object? state = Create(type);
            if (state is null) { skipped.Add(type.Name + ": no factory"); continue; }
            WesternJourneyState journey = WesternRouteGraph.LoadEmbedded().Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
            var shared = new PhilosophyRunState { WesternJourney = journey };
            object? owner = shared;
            for (int step = 0; step < path.Count - 1; step++)
            {
                object? nested;
                try { nested = Activator.CreateInstance(path[step].PropertyType, nonPublic: true); }
                catch (MissingMethodException) { nested = null; }
                if (nested is null) { owner = null; break; }
                path[step].SetValue(owner, nested);
                owner = nested;
            }
            if (owner is null) { skipped.Add(type.Name + ": no owner"); continue; }
            path[^1].SetValue(owner, state);
            bool? before = Verdict(flow, state, journey);
            PhilosophyRunState decoded;
            string first;
            try
            {
                first = PhilosophyRunStateCodec.Encode(shared);
                decoded = PhilosophyRunStateCodec.Decode(first);
            }
            catch (Exception error) { flipped.Add(type.Name + ": " + error.GetType().Name); continue; }
            // Saving a loaded record has to produce the same bytes: any field that
            // is lost on the way in shows up here.
            if (PhilosophyRunStateCodec.Encode(decoded) != first)
            {
                flipped.Add(type.Name + ": the record changes when it is saved again");
                continue;
            }
            object? restored = Walk(decoded, path);
            if (restored is null) { flipped.Add(type.Name + ": dropped"); continue; }
            bool? after = Verdict(flow, restored, decoded.WesternJourney!);
            if (before != after)
            {
                flipped.Add($"{type.Name}: {before?.ToString() ?? "threw"} before, {after?.ToString() ?? "threw"} after");
                continue;
            }
            compared++;
        }
        Console.WriteLine($"Route consistency after reload: {compared} rooms answer the same before and after a save; "
            + $"{skipped.Count} need their own checks.");
        Check(flipped.Count == 0, "These rooms do not come back the same after a reload: " + string.Join(", ", flipped));
        Check(compared >= 50, $"Only {compared} rooms were compared.");
    }
}
