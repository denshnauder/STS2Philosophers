using System.Text.RegularExpressions;
using STS2Philosophers;

/// <summary>
/// Guards the gap that let twelve delivered rooms ship without dispatch or
/// registration: every state that owns a route completion edge must have its
/// patch reachable from the native question chain and its event registered.
/// </summary>
internal static class RoomIntegrationChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        string root = FindRepositoryRoot();
        string dispatch = File.ReadAllText(Path.Combine(root, "src", "Events", "NativeQuestionNodeEvents.cs"));
        string boundary = File.ReadAllText(Path.Combine(root, "src", "Events", "RouteEventMapBoundary.cs"));
        int rooms = 0;
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src", "Philosophy"), "*State.cs"))
        {
            string text = File.ReadAllText(file);
            if (!Regex.IsMatch(text, @"CompletionEdgeId = ""WESTERN_EDGE_\d+""")) continue;
            string name = Path.GetFileNameWithoutExtension(file)[..^"State".Length];
            string patchFile = Path.Combine(root, "src", "Patches", name + "Patch.cs");
            if (!File.Exists(patchFile)) continue;
            string patch = File.ReadAllText(patchFile);
            if (!patch.Contains("TrySelectNative(")) continue;
            rooms++;
            Check(dispatch.Contains($"{name}Patch.TrySelectNative(run)"),
                $"{name}Patch must be reachable from the native question dispatch chain.");
            if (patch.Contains("TrySelectExistingNative("))
                Check(dispatch.Contains($"{name}Patch.TrySelectExistingNative(run)"),
                    $"{name}Patch must restore its open instance through the native dispatch chain.");
            Check(boundary.Contains($"or {name}") || boundary.Contains($"or {name} "),
                $"{name} must be registered as a route event.");
            string eventFile = Path.Combine(root, "src", "Events", name + ".cs");
            if (File.Exists(eventFile))
            {
                Match active = Regex.Match(File.ReadAllText(eventFile), @"IsActiveEvent\(run, this, (\d+)\)");
                Match gate = Regex.Match(File.ReadAllText(Path.Combine(root, "src", "Philosophy", name + "TriggerPolicy.cs")),
                    @"CurrentActIndex != (\d+)");
                if (active.Success && gate.Success)
                    Check(active.Groups[1].Value == gate.Groups[1].Value,
                        $"{name} checks act index {active.Groups[1].Value} while its trigger gate inserts at {gate.Groups[1].Value}.");
            }
        }
        Check(rooms >= 45, $"The room integration scan must see the delivered rooms (saw {rooms}).");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "STS2Philosophers.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("The repository root is unavailable.");
    }
}
