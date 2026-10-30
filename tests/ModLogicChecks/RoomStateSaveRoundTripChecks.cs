using System.Reflection;
using STS2Philosophers;

// A room that cannot be written and read back is a room that disappears when
// the player saves and resumes. Every room state is built through its own
// factory here, put on the run record, encoded and decoded, and the decoded
// copy has to stay as usable as the fresh one.
internal static class RoomStateSaveRoundTripChecks
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
                : parameter.ParameterType == typeof(string) ? $"ROUNDTRIP:{type.Name}:1" : null)
            .ToArray();
        if (arguments.Any(argument => argument is null)) return null;
        return create.Invoke(null, arguments);
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

    private static object? OwnerFor(PhilosophyRunState shared, List<PropertyInfo> path)
    {
        object? owner = shared;
        for (int step = 0; step < path.Count - 1; step++)
        {
            object? nested;
            try { nested = Activator.CreateInstance(path[step].PropertyType, nonPublic: true); }
            catch (MissingMethodException) { return null; }
            if (nested is null) return null;
            path[step].SetValue(owner, nested);
            owner = nested;
        }
        return owner;
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

    public static void Run()
    {
        var types = typeof(PhilosophyRunState).Assembly.GetTypes()
            .Where(type => type.Namespace == "STS2Philosophers" && type.IsClass && !type.IsAbstract
                && type.Name.EndsWith("State", StringComparison.Ordinal)
                && type.GetMethod("IsUsable", All) is { } method && method.GetParameters().Length == 0
                && type.Name != "PhilosophyRunState")
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();
        Check(types.Count >= 60, $"Only {types.Count} room states can be round-tripped.");
        WesternJourneyState journey = WesternRouteGraph.LoadEmbedded().Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
        var lost = new List<string>();
        var skipped = new List<string>();
        var checkedCount = 0;
        foreach (Type type in types)
        {
            List<PropertyInfo>? path = FindPath(type);
            if (path is null) { skipped.Add(type.Name + ": no path"); continue; }
            object? state;
            try { state = Create(type); }
            catch (TargetInvocationException) { skipped.Add(type.Name + ": factory threw"); continue; }
            if (state is null) { skipped.Add(type.Name + ": no factory"); continue; }
            MethodInfo usable = type.GetMethod("IsUsable", All)!;
            bool fresh;
            try { fresh = (bool)usable.Invoke(state, null)!; }
            catch (Exception) { skipped.Add(type.Name + ": unusable factory"); continue; }
            if (!fresh) { skipped.Add(type.Name + ": factory leaves it unusable"); continue; }
            var shared = new PhilosophyRunState { WesternJourney = journey };
            object? owner = OwnerFor(shared, path);
            if (owner is null) { skipped.Add(type.Name + ": no owner"); continue; }
            path[^1].SetValue(owner, state);
            PhilosophyRunState decoded;
            try { decoded = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared)); }
            catch (Exception error) { lost.Add(type.Name + ": " + error.GetType().Name); continue; }
            object? restored = Walk(decoded, path);
            if (restored is null) { lost.Add(type.Name + ": dropped"); continue; }
            if (!(bool)usable.Invoke(restored, null)!)
            {
                lost.Add(type.Name + ": unusable after the round trip");
                continue;
            }
            checkedCount++;
        }
        Console.WriteLine($"Room state round trip: {checkedCount} states survive a save and reload; {skipped.Count} need their own checks.");
        Check(lost.Count == 0, "These room states do not survive a save and reload: " + string.Join(", ", lost));
        Check(checkedCount >= 40,
            $"Only {checkedCount} room states round-tripped; skipped: " + string.Join(", ", skipped));
    }
}
