using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class CopernicusShiftedCenterNativeChecks
{
    private static readonly Assembly Mod = typeof(HeraclitusLyre).Assembly;
    private static Type TypeOf(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object Call(string type, string method, params object[] args) => TypeOf(type)
        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length)
        .Invoke(null, args)!;
    private static object Get(object state, string name) => state.GetType().GetProperty(name)!.GetValue(state)!;
    private static void Put(object state, string name, object value) => state.GetType().GetProperty(name)!.SetValue(state, value);
    private static bool Capture(RunState run, out object? fork)
    {
        object?[] args = [run, null];
        bool captured = (bool)TypeOf("CopernicusNativeTravel").GetMethod("TryCapture")!.Invoke(null, args)!;
        fork = args[1]; return captured;
    }
    private static bool Matches(RunState run, object fork) => (bool)Call("CopernicusNativeTravel", "MatchesSource", run, fork);
    private static bool Arrived(RunState run, object state) => (bool)Call("CopernicusNativeTravel", "HasArrived", run, state);

    private sealed class ForkMap : ActMap
    {
        private readonly MapPoint[,] _grid = new MapPoint[3, 3];
        protected override MapPoint[,] Grid => _grid;
        public override MapPoint StartingMapPoint => Source;
        public override MapPoint BossMapPoint => Boss;
        internal MapPoint Source { get; } = new(1, 0) { PointType = MapPointType.Monster };
        internal MapPoint Left { get; } = new(0, 1) { PointType = MapPointType.Unknown };
        internal MapPoint Right { get; } = new(2, 1) { PointType = MapPointType.Unknown };
        private MapPoint Boss { get; } = new(1, 2) { PointType = MapPointType.Boss };
        internal ForkMap()
        {
            _grid[1, 0] = Source; _grid[0, 1] = Left; _grid[2, 1] = Right; _grid[1, 2] = Boss;
            Source.AddChildPoint(Left); Source.AddChildPoint(Right);
        }
    }

    public static async Task Run()
    {
        TestMode.IsOn = true;
        MapCoord ctorProbe = new(2, 1);
        Check(ctorProbe.col == 2 && ctorProbe.row == 1,
            "The current native coordinate constructor takes column before row.");
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        try
        {
            Set(manager, "State", null);
            NativeRuntimeFixture fixture = new(typeof(HeraclitusLyre));
            RunState run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
            Set(run, "_players", new List<Player> { fixture.Player }); Set(run, "_currentActIndex", 1);
            Set(run, "Rng", new RunRngSet("123456789")); Set(run, "ActFloor", 1);
            Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
            ModelDb.Inject(typeof(MultiplayerScalingModel));
            Set(run, "MultiplayerScalingModel", ModelDb.Singleton<MultiplayerScalingModel>());
            Set(fixture.Player, "Deck", new CardPile(PileType.Deck)); Set(fixture.Player, "_runState", run);
            Set(fixture.Combat, "RunState", run);
            ModelDb.Inject(typeof(AxebotsNormal)); Set(fixture.Combat, "_encounter", ModelDb.Encounter<AxebotsNormal>().ToMutable());
            CombatRoom sourceRoom = (CombatRoom)RuntimeHelpers.GetUninitializedObject(typeof(CombatRoom));
            Set(sourceRoom, "CombatState", fixture.Combat); Set(sourceRoom, "Id", 41); sourceRoom.MarkPreFinished();
            Set(run, "_currentRooms", new List<AbstractRoom> { sourceRoom });
            ForkMap map = new(); Set(run, "Map", map);
            var visited = new List<MapCoord> { map.Source.coord }; Set(run, "_visitedMapCoords", visited);
            List<List<MapPointHistoryEntry>> history = [[], [new() { MapPointType = MapPointType.Monster }]];
            Set(run, "_mapPointHistory", history); Set(manager, "State", run);
            Check(ReferenceEquals(run.CurrentMapPoint, map.Source), $"Native map grid addresses the exact source: coordinate {run.CurrentMapCoord}, point {run.CurrentMapPoint?.coord}.");
            Check(Capture(run, out object? fork) && fork is not null,
                $"The actual native two-exit source captures: act {run.CurrentActIndex}, rooms {run.CurrentRoomCount}, prefinished {sourceRoom.IsPreFinished}, victory {sourceRoom.IsVictoryRoom}, type {sourceRoom.RoomType}, source {map.Source.coord.row}:{map.Source.coord.col}, legal {string.Join(';', MapTravel.GetTravelablePointsFrom(run, map.Source).Select(p => $"{p.coord.row}:{p.coord.col}:{p.PointType}"))}.");
            Check(Matches(run, fork!), "The captured source matches its frozen table.");
            Set(sourceRoom, "Id", 42); Check(!Matches(run, fork!), "A live source-room identity drift rejects the table."); Set(sourceRoom, "Id", 41);
            map.Right.PointType = MapPointType.Boss;
            Check(!Capture(run, out _) && !Matches(run, fork!), "A Boss or changed type cannot be filtered out to preserve a stale table.");
            map.Right.PointType = MapPointType.Unknown;
            map.Source.RemoveChildPoint(map.Right);
            Check(!Capture(run, out _), "One ordinary child is not a two-exit fork.");
            ModelDb.Inject(typeof(WingedBoots));
            var boots = (WingedBoots)ModelDb.GetById<RelicModel>(ModelDb.GetId<WingedBoots>()).ToMutable();
            boots.Owner = fixture.Player; ((List<RelicModel>)fixture.Player.Relics).Add(boots);
            Check(boots.ShouldAllowFreeTravel() && map.Source.Children.Count == 1
                && MapTravel.GetTravelablePointsFrom(run, map.Source).Count() == 2
                && Capture(run, out object? freeFork) && freeFork is not null && Matches(run, fork!),
                "Real Winged Boots authorizes a non-child next-row point; capture uses the whole native legal set.");
            ((List<RelicModel>)fixture.Player.Relics).Remove(boots); map.Source.AddChildPoint(map.Right);
            object state = ReadyState(run, fork!);
            Check((bool)Get(state, "TravelPending") && !Arrived(run, state),
                "A closed saved table is an intention, not a native arrival receipt.");
            visited.Add(map.Right.coord); Set(run, "ActFloor", 2);
            Check(!Arrived(run, state), "An advanced native coordinate alone is the pre-history travel checkpoint, not arrival.");
            ModelDb.Inject(typeof(EmpedoclesRootsInquiry));
            EventRoom destination = new(ModelDb.Event<EmpedoclesRootsInquiry>());
            Set(destination, "Id", 41); // Runtime IDs can be reused after native save/load.
            history[1].Add(new() { MapPointType = MapPointType.Unknown,
                Rooms = [new() { RoomType = RoomType.Event, ModelId = destination.ModelId }] });
            Check(!Arrived(run, state), "Target history plus the old source room cannot complete arrival.");
            Set(run, "_currentRooms", new List<AbstractRoom> { destination });
            Check(Arrived(run, state), "Exact target visited/history indices and its normal room prove arrival, even with reused runtime ID.");
            visited[1] = map.Left.coord; Check(!Arrived(run, state), "The unselected outlet cannot satisfy the saved table."); visited[1] = map.Right.coord;
            visited.Add(map.BossMapPoint.coord); history[1].Add(new() { MapPointType = MapPointType.Boss });
            Check(Arrived(run, state), "Later legitimate progress preserves the indexed historical proof of the selected exit.");
            await CheckDeferredChoiceDrain(run, state);
            CheckBindings();
            Console.WriteLine("Copernicus native checks passed: whole native MapTravel set including real Winged Boots non-child access, snapshot/type/source drift, pending versus coordinate-only versus indexed history/room arrival, reused runtime IDs and later history; real deferred choice-task drain without self-wait; native MapCoord(column,row) and adapter argument order, public Proceed/Travel/EnterMapCoord binding and save-before-history ordering. No rendered map/full travel replay.");
        }
        finally { Set(manager, "State", prior); }
    }

    private static async Task CheckDeferredChoiceDrain(RunState run, object state)
    {
        RunManager manager = RunManager.Instance;
        object priorSynchronizer = manager.EventSynchronizer;
        bool priorShouldSave = manager.ShouldSave;
        List<AbstractRoom> priorRooms = run.CurrentRoom is { } current ? [current] : [];
        List<MapCoord> priorVisited = run.VisitedMapCoords.ToList();
        List<List<MapPointHistoryEntry>> priorHistory = run.MapPointHistory.Select(entries => entries.ToList()).ToList();
        var choice = new TaskCompletionSource();
        try
        {
            // AwaitPendingOptionTasks itself is native. Only its networking shell
            // and an option still held by the synchronizer are bootstrapped here.
            object synchronizer = RuntimeHelpers.GetUninitializedObject(typeof(RunManager)
                .GetProperty("EventSynchronizer")!.PropertyType);
            ModelDb.Inject(typeof(CopernicusShiftedCenter));
            EventModel owner = ModelDb.Event<CopernicusShiftedCenter>().ToMutable();
            Set(synchronizer, "_events", new List<EventModel> { owner });
            Set(synchronizer, "_playerCollection", run); Set(synchronizer, "_localPlayerId", run.Players[0].NetId);
            Set(synchronizer, "_pendingOptionTasks", new List<Task> { choice.Task });
            Set(manager, "EventSynchronizer", synchronizer);
            Set(manager, "ShouldSave", false); // Core safely rejects before any map/UI operation.
            object fork = Get(state, "Fork");
            Set(run, "_visitedMapCoords", priorVisited.Take((int)Get(fork, "VisitedCount")).ToList());
            var sourceHistory = priorHistory.Select(entries => entries.ToList()).ToList();
            sourceHistory[1] = sourceHistory[1].Take((int)Get(fork, "HistoryCount")).ToList();
            Set(run, "_mapPointHistory", sourceHistory);
            EventRoom room = new(ModelDb.Event<CopernicusShiftedCenter>());
            Set(run, "_currentRooms", new List<AbstractRoom> { room });
            object shared = Activator.CreateInstance(TypeOf("PhilosophyRunState"), nonPublic: true)!;
            Put(shared, "CopernicusShiftedCenter", state);
            Call("PhilosophyRunStateService", "Restore", run, shared);
            Check(Matches(run, fork), "A sole restored native Copernicus room matches its exact saved Fork despite the old combat object being absent.");
            int blocked = 0;
            Action<string> onBlocked = _ => blocked++;
            Call("CopernicusNativeTravel", "QueueTravel", run, owner, state, onBlocked);
            Call("CopernicusNativeTravel", "QueueTravel", run, owner, state, onBlocked);
            object table = TypeOf("CopernicusNativeTravel").GetField("Runtimes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            object?[] lookup = [run, null];
            Check((bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, lookup)!, "Queued travel has one tracked runtime.");
            object runtime = lookup[1]!;
            FieldInfo activeField = runtime.GetType().GetField("Active", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Check(activeField.GetValue(runtime) is null && !choice.Task.IsCompleted && blocked == 0,
                "QueueTravel returns to the choice callback before starting navigation.");
            var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
            await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, Godot.SceneTree.SignalName.ProcessFrame);
            Task<bool>? active = activeField.GetValue(runtime) as Task<bool>;
            Check(active is { IsCompleted: false } && !choice.Task.IsCompleted && blocked == 0,
                "After the real CallDeferred boundary the tracked travel waits for the synchronizer's pending choice, not for itself.");
            choice.SetResult();
            Check(!await active! && blocked == 1,
                "Completing the original choice releases the single queued core attempt and its expected blocked-fixture callback.");
        }
        finally
        {
            choice.TrySetResult();
            Set(manager, "EventSynchronizer", priorSynchronizer); Set(manager, "ShouldSave", priorShouldSave);
            Set(run, "_currentRooms", priorRooms);
            Set(run, "_visitedMapCoords", priorVisited); Set(run, "_mapPointHistory", priorHistory);
        }
    }

    private static object ReadyState(RunState run, object fork)
    {
        object state = Call("CopernicusShiftedCenterState", "Create",
            $"COPERNICUS_SHIFTED_CENTER:{run.Rng.Seed}:2", "DEMOCRITUS:NATIVE:2", "EMPEDOCLES:NATIVE:2", fork);
        foreach (string action in new[] { "AcceptInvitation", "RecenterOnSun", "PlaceObserver", "PlacePlanet", "PlaceApparent", "RecordResidual", "FollowTable" })
        {
            object value = Enum.Parse(TypeOf("CopernicusShiftedCenterAction"), action);
            Check((bool)Call("CopernicusShiftedCenterFlow", "TryPrepare", state, value), "The saved comparison follows legal page actions.");
            Call("CopernicusShiftedCenterFlow", "Apply", state, value);
        }
        object shared = Activator.CreateInstance(TypeOf("PhilosophyRunState"), nonPublic: true)!;
        Put(shared, "CopernicusShiftedCenter", state);
        shared = Call("PhilosophyRunStateCodec", "Decode", Call("PhilosophyRunStateCodec", "Encode", shared));
        return Get(shared, "CopernicusShiftedCenter");
    }

    private static void CheckBindings()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        MemberInfo[] adapter = Calls(TypeOf("CopernicusNativeTravel").GetMethod("ResumeCoreAsync", flags)!).ToArray();
        Check(adapter.OfType<MethodBase>().Any(m => m.DeclaringType == typeof(NEventRoom) && m.Name == "Proceed")
            && adapter.OfType<MethodBase>().Any(m => m.DeclaringType == typeof(NMapScreen) && m.Name == "TravelToMapCoord"),
            "The production adapter calls public native event Proceed and map travel.");
        int column = Array.FindIndex(adapter, m => m.DeclaringType == TypeOf("CopernicusForkExit") && m.Name == "get_Column");
        int row = Array.FindIndex(adapter, m => m.DeclaringType == TypeOf("CopernicusForkExit") && m.Name == "get_Row");
        Check(column >= 0 && row > column && adapter.OfType<ConstructorInfo>().Any(c => c.DeclaringType == typeof(MapCoord)),
            "The adapter passes the selected column then row to the real native coordinate constructor.");
        MemberInfo[] queued = Calls(TypeOf("CopernicusNativeTravel").GetMethod("RunQueuedAsync", flags)!).ToArray();
        int drain = Array.FindIndex(queued, m => m.Name == "AwaitPendingOptionTasks");
        Check(drain >= 0 && Array.FindIndex(queued, m => m.Name == "ResumeCoreAsync") > drain,
            "The deferred production task awaits native pending choices before entering any room-exiting travel.");
        MemberInfo[] travel = Calls(typeof(NMapScreen).GetMethod("TravelToMapCoord", flags)!).ToArray();
        Check(travel.OfType<MethodBase>().Any(m => m.DeclaringType == typeof(RunManager) && m.Name == "EnterMapCoord"),
            "The actual native map travel reaches the real RunManager coordinate entry.");
        MemberInfo[] entry = Calls(typeof(RunManager).GetMethod("EnterMapCoord", flags)!).ToArray();
        Check(Array.FindIndex(entry, m => m.Name == "AddVisitedMapCoord") < Array.FindIndex(entry, m => m.Name == "EnterMapCoordInternal"),
            "Native coordinate recording precedes room entry; current coordinate alone cannot prove arrival.");
        MemberInfo[] point = Calls(typeof(RunManager).GetMethod("EnterMapPointInternal", flags)!).ToArray();
        int save = Array.FindIndex(point, m => m.Name == "SaveRun");
        int append = Array.FindIndex(point, m => m.Name == "AppendToMapPointHistory");
        Check(save >= 0 && append > save && point.OfType<MethodBase>().Any(m => m.Name == "EnterRoom"),
            "The current native navigation checkpoint saves the selected coordinate before appending target history/entering its room.");
    }

    private static IEnumerable<MemberInfo> Calls(MethodInfo method)
    {
        foreach (MemberInfo member in DirectCalls(method)) yield return member;
        if (method.GetCustomAttribute<AsyncStateMachineAttribute>() is { } machine)
            foreach (MemberInfo member in DirectCalls(machine.StateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic)!))
                yield return member;
    }
    private static IEnumerable<MemberInfo> DirectCalls(MethodInfo method)
    {
        Dictionary<ushort, OpCode> codes = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode))
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(op => (ushort)op.Value);
        byte[] il = method.GetMethodBody()!.GetILAsByteArray()!;
        for (int i = 0; i < il.Length;)
        {
            ushort value = il[i++]; if (value == 0xfe) value = (ushort)(0xfe00 | il[i++]);
            OpCode op = codes[value];
            if (op.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok)
            {
                int token = BitConverter.ToInt32(il, i); i += 4;
                yield return method.Module.ResolveMember(token)!;
            }
            else i += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4,
            };
        }
    }
}
