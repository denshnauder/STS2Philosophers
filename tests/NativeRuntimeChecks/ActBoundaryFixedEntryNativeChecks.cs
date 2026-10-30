using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Proves the act-boundary entry: with the journey at the second act's fixed
// position, a room is selected without any unknown map point, and the same
// run gets nothing once the position is spent. The run is an in-memory fixture.
internal static class ActBoundaryFixedEntryNativeChecks
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
        ModelDb.Inject(typeof(SpinozaCauseNetwork));
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Methods)!
            .Invoke(null, [typeof(DescartesThinkingLens), 1])!;
        var run = (RunState)tuple[1]!;
        Set(CombatManager.Instance, "_turnState", null);

        object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
        object journey = graph.GetType().GetMethod("Start")!.Invoke(graph,
            ["DESCARTES__SELF_AND_OTHER__CORE"])!;
        Put(journey, "LastFixedAct", 2);
        object doctrine = Activator.CreateInstance(T("CurrentDoctrine"), nonPublic: true)!;
        Put(doctrine, "ThinkerId", "DESCARTES");
        Put(doctrine, "DoctrineId", "DESCARTES_THINKING_LENS");
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "CurrentDoctrine", doctrine);
        Put(shared, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "Restore", run, shared);

        object?[] prepare = [run, null];
        bool selected = (bool)Call("ActFixedEntryHost", "TryPrepare", prepare)!;
        Check(selected, "The second act's fixed room must be selectable at the ancient boundary without a question point.");
        var room = (EventRoom)prepare[1]!;
        Check(room.CanonicalEvent is SpinozaCauseNetwork,
            "The boundary selects the room that owns this act's fixed edge, got " + room.CanonicalEvent.GetType().Name + ".");
        object?[] query = [run, null];
        Call("PhilosophyRunStateService", "TryGet", query);
        object current = Get(query[1]!, "SpinozaCauseNetwork");
        Check(current is not null && (bool)current.GetType().GetMethod("IsUsable")!.Invoke(current, null)!,
            "The boundary selection persists a usable room state for save recovery.");

        Put(journey, "LastFixedAct", 3);
        object spent = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Put(spent, "CurrentDoctrine", doctrine);
        Put(spent, "WesternJourney", journey);
        Call("PhilosophyRunStateService", "Restore", run, spent);
        object?[] again = [run, null];
        Check(!(bool)Call("ActFixedEntryHost", "TryPrepare", again)!,
            "A spent fixed position must not present a second room at the same boundary.");
        return Task.CompletedTask;
    }
}
