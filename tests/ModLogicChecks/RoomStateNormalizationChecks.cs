using System.Reflection;
using System.Text.Json.Serialization;
using STS2Philosophers;

// A room state has to normalise itself when a save is read back, or the room
// opens with empty collections and looks broken only on the resume path. Most
// states do that through IJsonOnDeserialized; the rest are reached by a
// hand-written list in PhilosophyRunState. A room added later can miss both,
// and nothing else notices, so this check proves every state is reached by one
// of the two.
internal static class RoomStateNormalizationChecks
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    // The record itself and the two per-act records live in lists, so they have
    // no single slot to walk to.
    private static bool IsExcluded(Type type) =>
        type.Name is "PhilosophyRunState" or "ActBehaviorState" or "CombatBehaviorState";

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

    public static void Run()
    {
        var types = typeof(PhilosophyRunState).Assembly.GetTypes()
            .Where(type => type.Namespace == "STS2Philosophers" && type.IsClass && !type.IsAbstract
                && !IsExcluded(type)
                && type.GetMethod("NormalizeAfterLoad", All) is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();
        Check(types.Count >= 50, $"Only {types.Count} room states normalise themselves after a load.");
        var selfNormalising = 0;
        var listed = 0;
        var missing = new List<string>();
        foreach (Type type in types)
        {
            if (typeof(IJsonOnDeserialized).IsAssignableFrom(type))
            {
                selfNormalising++;
                continue;
            }
            List<PropertyInfo>? path = FindPath(type);
            // Every normaliser at least repairs its own string identifiers, so a
            // null identifier that comes back filled proves the call reached it.
            List<PropertyInfo> identifiers = type.GetProperties(All)
                .Where(property => property.PropertyType == typeof(string) && property.CanWrite && property.CanRead)
                .ToList();
            if (path is null || identifiers.Count == 0)
            {
                missing.Add(type.Name + ": no path or no identifier");
                continue;
            }
            object state;
            try { state = Activator.CreateInstance(type, nonPublic: true)!; }
            catch (MissingMethodException) { missing.Add(type.Name + ": no ctor"); continue; }
            foreach (PropertyInfo identifier in identifiers) identifier.SetValue(state, null);
            var shared = new PhilosophyRunState();
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
            if (owner is null) { missing.Add(type.Name + ": no owner"); continue; }
            path[^1].SetValue(owner, state);
            shared.NormalizeAfterLoad();
            if (identifiers.All(identifier => identifier.GetValue(state) is null))
            {
                missing.Add(type.Name);
                continue;
            }
            listed++;
        }
        Check(missing.Count == 0,
            "These room states are never normalised when a save is read, so they come back empty: "
            + string.Join(", ", missing));
        Check(selfNormalising + listed == types.Count, "Every room state is accounted for.");
        Check(listed >= 15, $"Only {listed} room states rely on the run record's list.");
    }
}
