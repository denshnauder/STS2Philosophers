using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class EventPortraitNativeChecks
{
    public static void Run(string pckPath)
    {
        TestMode.IsOn = true;
        pckPath = Path.GetFullPath(pckPath);
        Check(File.Exists(pckPath), $"The requested portrait content pack does not exist: {pckPath}");
        Check(ProjectSettings.LoadResourcePack(pckPath, replaceFiles: true),
            $"Godot could not mount the requested content pack: {pckPath}");
        Type[] events = typeof(PhilosophersGaze).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.ContainsGenericParameters && typeof(EventModel).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        Check(events.Any(type => type == typeof(PhilosophersGaze))
            && events.Any(type => type == typeof(ZenoCivicChallenge)),
            "Portrait verification includes both PhilosophersGaze and the actual reported Zeno event.");
        PropertyInfo pathProperty = typeof(EventModel).GetProperty("InitialPortraitPath",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        List<string> failures = [];
        int passed = 0;
        foreach (Type type in events)
        {
            string path = "<unresolved>";
            try
            {
                ModelDb.Inject(type);
                EventModel canonical = ModelDb.GetById<EventModel>(ModelDb.GetId(type));
                path = (string)pathProperty.GetValue(canonical)!;
                Check(!string.IsNullOrWhiteSpace(path), $"{type.Name} has no initial portrait path.");
                // A source PNG existing in a pack is insufficient: Godot must be
                // able to resolve its import/remap to a real texture in the pack.
                Texture2D? loaded = ResourceLoader.Load<Texture2D>(path);
                Check(loaded is not null && loaded.GetWidth() > 0 && loaded.GetHeight() > 0,
                    $"{type.Name} cannot load a nonempty portrait texture at {path}.");
                Texture2D portrait = canonical.ToMutable().CreateInitialPortrait();
                Check(portrait is not null && portrait.GetWidth() > 0 && portrait.GetHeight() > 0,
                    $"{type.Name}'s real EventModel.CreateInitialPortrait did not return a nonempty texture.");
                passed++;
            }
            catch (Exception exception)
            {
                Exception cause = exception is TargetInvocationException { InnerException: not null } invocation
                    ? invocation.InnerException! : exception;
                failures.Add($"{type.Name}: {path}: {cause.GetType().Name}: {cause.Message}");
            }
        }
        Check(failures.Count == 0,
            $"Event portrait native verification failed ({passed}/{events.Length} loaded): {string.Join("; ", failures)}");
        Console.WriteLine($"Event portrait native checks passed: all {passed} concrete mod EventModels, including PhilosophersGaze and ZenoCivicChallenge, load nonempty imported textures and create native initial portraits from {pckPath}; no game or rendered UI.");
    }
}
