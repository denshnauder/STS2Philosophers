using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class CopernicusQuestionEntryNativeChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type T(string name) => typeof(HeraclitusLyre).Assembly.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object[] args) => T(type).GetMethods(Flags)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args);
    private static object Get(object item, string name) => item.GetType().GetProperty(name, Flags)!.GetValue(item)!;
    private static void Put(object item, string name, object value) => item.GetType().GetProperty(name, Flags)!.SetValue(item, value);
    private static string Encode(object shared) => (string)Call("PhilosophyRunStateCodec", "Encode", shared)!;
    private static object Current(RunState run)
    {
        object?[] args = [run, null];
        Check((bool)T("PhilosophyRunStateService").GetMethod("TryGet")!.Invoke(null, args)!, "Copernicus state is attached to the actual run.");
        return args[1]!;
    }
    private static object Reload(RunState run)
    {
        object shared = Call("PhilosophyRunStateCodec", "Decode", Encode(Current(run)))!;
        Call("PhilosophyRunStateService", "Restore", run, shared);
        return Get(Current(run), "CopernicusShiftedCenter");
    }
    private static bool Valid(object state) => (bool)state.GetType().GetMethod("IsUsable")!.Invoke(state, null)!;
    private static bool Bind(RunState run, object state) => (bool)Call("CopernicusNativeTravel", "TryBindQuestion", run, state)!;
    private static bool Matches(RunState run, object fork) => (bool)Call("CopernicusNativeTravel", "MatchesSource", run, fork)!;

    public static Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        object? priorNetwork = manager.NetService;
        object priorSynchronizer = manager.EventSynchronizer;
        object priorExecutor = manager.ActionExecutor;
        object? priorTurn = typeof(CombatManager).GetField("_turnState", Flags)!.GetValue(CombatManager.Instance);
        try
        {
            TestMode.IsOn = true;
            ModelDb.Inject(typeof(CopernicusShiftedCenter));
            var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Flags)!
                .Invoke(null, [typeof(HeraclitusLyre), 1])!;
            var fixture = (NativeRuntimeFixture)tuple[0]!;
            var run = (RunState)tuple[1]!;
            object pool = tuple[3]!;
            EventModel original = (EventModel)tuple[4]!;
            // Galileo's existing predecessor builder performs the actual
            // Democritus/Empedocles choices also required by Copernicus.
            object shared = typeof(AllRouteQuestionNodeChecks).GetMethod("BuildShared", Flags)!.Invoke(null,
                ["GalileoMeasuredPractice", "HERACLITUS__BEING_AND_CHANGE__CORE", fixture.Relic.Id.Entry, run])!;
            Call("PhilosophyRunStateService", "Restore", run, shared);
            ModelDb.Inject(typeof(EmpedoclesRootsInquiry));
            var history = (List<List<MapPointHistoryEntry>>)run.MapPointHistory;
            history[1].Add(new() { MapPointType = MapPointType.Unknown,
                Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<EmpedoclesRootsInquiry>() }] });
            string priorOnly = Encode(Current(run));
            Check(Call("CopernicusShiftedCenterPatch", "TrySelectNative", run) is null && Encode(Current(run)) == priorOnly,
                "A battle before the completed Empedocles event cannot replace the required actual later source combat.");
            history[1].Add(new() { MapPointType = MapPointType.Monster,
                Rooms = [new() { RoomType = RoomType.Monster }] });
            string historyBefore = JsonSerializer.Serialize(run.MapPointHistory);
            string visitedBefore = JsonSerializer.Serialize(run.VisitedMapCoords);
            string rngBefore = JsonSerializer.Serialize(run.Rng.ToSerializable());
            int queueBefore = (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)!;
            EventModel chosen = run.Act.PullNextEvent(run);
            Check(chosen is CopernicusShiftedCenter, "Actual native PullNextEvent selects Copernicus at the existing two-exit question.");
            object state = Get(Current(run), "CopernicusShiftedCenter");
            object fork = Get(state, "Fork");
            string pendingFork = JsonSerializer.Serialize(fork, fork.GetType());
            Check(Valid(state) && (int)Get(state, "Version") == 2 && Get(fork, "Source").ToString() == "QuestionPending"
                && (int)Get(fork, "HistoryCount") == run.MapPointHistory[1].Count + 1,
                "Selection creates a version-two pending intention for the future native EventRoom, without pretending it already exists.");
            Check(historyBefore == JsonSerializer.Serialize(run.MapPointHistory) && visitedBefore == JsonSerializer.Serialize(run.VisitedMapCoords)
                && rngBefore == JsonSerializer.Serialize(run.Rng.ToSerializable())
                && queueBefore == (int)pool.GetType().GetField("eventsVisited")!.GetValue(pool)!
                && run.VisitedEventIds.SetEquals([chosen.Id]) && !run.VisitedEventIds.Contains(original.Id),
                "Native selection owns the visited ID; it invents no coordinate/history, RNG draw or displaced event visit.");
            Check(!Bind(run, state), "A pending intention cannot bind before a real room and native history exist.");
            state = Reload(run); fork = Get(state, "Fork");
            Check(Valid(state) && Get(fork, "Source").ToString() == "QuestionPending"
                && (bool)Call("CopernicusNativeTravel", "MatchesPendingQuestion", run, fork)!,
                "A pending shared checkpoint round-trip retains its exact source intention before room creation.");

            EventRoom room = new(chosen); Set(room, "Id", 173);
            Set(run, "_currentRooms", new List<AbstractRoom> { room });
            Check(!Bind(run, state), "Creating only the room cannot substitute for the native appended event history.");
            history[1].Add(new() { MapPointType = MapPointType.Unknown,
                Rooms = [new() { RoomType = RoomType.Event, ModelId = chosen.Id }] });
            Check(Bind(run, state) && Valid(state) && Get(Get(state, "Fork"), "Source").ToString() == "QuestionEvent"
                && (int)Get(Get(state, "Fork"), "SourceRoomId") == 173 && Matches(run, Get(state, "Fork")),
                "Binding uses the actual Copernicus EventRoom and exactly its current native question/history.");
            EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(room.ToSerializable(), null!)!;
            Set(run, "_currentRooms", new List<AbstractRoom> { restored });
            state = Reload(run);
            string bound = JsonSerializer.Serialize(Get(state, "Fork"), Get(state, "Fork").GetType());
            Check(Bind(run, state) && Matches(run, Get(state, "Fork"))
                && bound == JsonSerializer.Serialize(Get(state, "Fork"), Get(state, "Fork").GetType()),
                "Native room plus shared-state restoration retain the bound table without capturing a new runtime room ID.");

            object accept = Enum.Parse(T("CopernicusShiftedCenterAction"), "AcceptInvitation");
            Check((bool)Call("CopernicusShiftedCenterFlow", "TryPrepare", state, accept)!, "The bound event prepares its real first choice.");
            state = Reload(run);
            Check(Get(state, "PendingAction").ToString() == "AcceptInvitation" && Bind(run, state)
                && bound == JsonSerializer.Serialize(Get(state, "Fork"), Get(state, "Fork").GetType()),
                "Prepared-choice checkpoint restoration keeps the pending action and original frozen fork.");
            shared = Current(run);
            object journey = Get(shared, "WesternJourney"), democritus = Get(shared, "DemocritusArrangement"), empedocles = Get(shared, "EmpedoclesRootsInquiry");
            object graph = Call("WesternRouteGraph", "LoadEmbedded")!;
            Check((bool)Call("CopernicusShiftedCenterFlow", "TryCommitPending", state, journey, democritus, empedocles, graph)!, "The restored prepared choice commits through its actual route flow.");
            foreach (string name in new[] { "RecenterOnSun", "PlaceObserver", "PlacePlanet", "PlaceApparent", "RecordResidual", "FollowTable" })
            {
                object action = Enum.Parse(T("CopernicusShiftedCenterAction"), name);
                Check((bool)Call("CopernicusShiftedCenterFlow", "TryPrepare", state, action)!
                    && (bool)Call("CopernicusShiftedCenterFlow", "TryCommitPending", state, journey, democritus, empedocles, graph)!, "The real comparison commits " + name);
            }
            state = Reload(run);
            Check(Valid(state) && (bool)Get(state, "TravelPending") && (int)Get(state, "SelectedExitIndex") == 1
                && Bind(run, state) && bound == JsonSerializer.Serialize(Get(state, "Fork"), Get(state, "Fork").GetType())
                && !(bool)Call("CopernicusNativeTravel", "HasArrived", run, state)!,
                "A restored selected exit remains an intention at the original room, rather than fabricated arrival or a recaptured table.");
            string stable = Encode(Current(run));
            Set(run, "_currentRooms", new List<AbstractRoom>());
            run.CurrentMapPoint!.PointType = MapPointType.Monster;
            Check(ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original) && Encode(Current(run)) == stable,
                "Neither a selected native table nor a later non-question entry can be rebound by event selection.");
            run.CurrentMapPoint.PointType = MapPointType.Unknown;

            object future = Call("CopernicusShiftedCenterState", "Create", Get(state, "InstanceId"),
                Get(state, "DemocritusInstanceId"), Get(state, "EmpedoclesInstanceId"),
                JsonSerializer.Deserialize(pendingFork, T("CopernicusForkSnapshot"))!)!;
            Put(future, "Version", 99);
            Put(Current(run), "CopernicusShiftedCenter", future);
            ((HashSet<ModelId>)run.VisitedEventIds).Clear();
            string invalid = Encode(Current(run));
            Check(!Valid(future) && ReferenceEquals(fixture.Relic.ModifyNextEvent(original), original) && Encode(Current(run)) == invalid,
                "A future version is rejected and preserved, rather than reinitialized as a new question table.");
            string legacyJson = bound.Replace("\"Source\":2", "\"Source\":0");
            object legacyFork = JsonSerializer.Deserialize(legacyJson, T("CopernicusForkSnapshot"))!;
            object legacy = Call("CopernicusShiftedCenterState", "Create", "LEGACY:COPERNICUS", "DEMOCRITUS:OLD", "EMPEDOCLES:OLD", legacyFork)!;
            Check((int)Get(legacy, "Version") == 1 && Valid(legacy) && Bind(run, legacy)
                && Get(Get(legacy, "Fork"), "Source").ToString() == "LegacyCombat"
                && JsonSerializer.Serialize(Get(legacy, "Fork"), Get(legacy, "Fork").GetType()) == legacyJson,
                "The legacy combat snapshot keeps version one and its original source mode; question binding never rewrites it.");
            Put(legacy, "Version", 2);
            Check(!Valid(legacy), "A version-two record cannot silently relabel a legacy combat fork.");
            MethodInfo entry = typeof(RunManager).GetMethod("EnterMapPointInternal", Flags)!;
            MethodBase[] nativeCalls = ((IEnumerable<MethodBase>)typeof(NativeQuestionNodeChecks)
                .GetMethod("References", Flags)!.Invoke(null, [entry])!).ToArray();
            int exitRooms = Array.FindIndex(nativeCalls, method => method.Name == "ExitCurrentRooms");
            int targetSave = Array.FindIndex(nativeCalls, method => method.Name == "SaveRun");
            Check(exitRooms >= 0 && targetSave > exitRooms,
                "Native target entry exits the source room before its pre-history checkpoint, so shared save protection cannot carry the old event into the selected exit.");
            Console.WriteLine("Copernicus question-entry checks passed: native PullNextEvent/visited/RNG/queue, real question pending versus room/history binding, native room/shared checkpoint round-trips, prepared and selected exit preservation, unknown versions and untouched legacy combat mode. No rendered UI, full native travel or player-save access.");
            return Task.CompletedTask;
        }
        finally
        {
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave); Set(manager, "NetService", priorNetwork);
            Set(manager, "EventSynchronizer", priorSynchronizer); Set(manager, "ActionExecutor", priorExecutor);
            Set(CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
        }
    }
}
