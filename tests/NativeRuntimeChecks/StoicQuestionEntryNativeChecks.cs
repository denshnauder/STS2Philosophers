using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

internal static class StoicQuestionEntryNativeChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type T(string name) => typeof(SocratesBalanceWeight).Assembly.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) => T(type).GetMethods(Flags)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args);
    private static object Get(object item, string property) => item.GetType().GetProperty(property, Flags)!.GetValue(item)!;
    private static void Put(object item, string property, object? value) => item.GetType().GetProperty(property, Flags)!.SetValue(item, value);
    private static object E(string type, string value) => Enum.Parse(T(type), value);
    private static string Encode(object shared) => (string)Call("PhilosophyRunStateCodec", "Encode", shared)!;
    private static object Catalog => T("ZenoRouteValidationCatalogs").GetProperty("SaveRestore", Flags)!.GetValue(null)!;
    private static object Current(RunState run)
    {
        object?[] args = [run, null];
        Check((bool)T("PhilosophyRunStateService").GetMethod("TryGet")!.Invoke(null, args)!, "The special event uses the actual run state.");
        return args[1]!;
    }

    private static (NativeRuntimeFixture Fixture, RunState Run, EventModel Original, object Shared) Fixture()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Flags)!
            .Invoke(null, [typeof(SocratesBalanceWeight), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        object shared = typeof(NativeQuestionNodeChecks).GetMethod("Shared", Flags)!.Invoke(null,
            ["None", "SOCRATES__VIRTUE_AND_HAPPINESS__CORE", fixture.Relic.Id.Entry])!;
        Call("PhilosophyRunStateService", "Restore", run, shared);
        return (fixture, run, (EventModel)tuple[4]!, Current(run));
    }

    public static async Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        object? priorNetwork = manager.NetService;
        object priorSynchronizer = manager.EventSynchronizer, priorExecutor = manager.ActionExecutor;
        object? priorTurn = typeof(CombatManager).GetField("_turnState", Flags)!.GetValue(CombatManager.Instance);
        try
        {
            TestMode.IsOn = true;
            ModelDb.Inject(typeof(DiogenesStaySwitch)); ModelDb.Inject(typeof(ZenoAssentBoundary));
            CheckDiogenesSlotAndRecovery();
            CheckZenoNativeSelection();
            await CheckFrozenDestinationMismatch();
            await CheckRealPersistenceAndRetry();
            Console.WriteLine("Stoic question entry checks passed: real native PullNextEvent selects Diogenes/Zeno without an extra room, map/history/RNG or business-budget mutation; Diogenes v1/v2 codec and one/two-room recovery gates; exact native seed/coordinate identity; no Zeno claim before actual room startup; visited guards; counted rejection before any persistence retry; real TriggerAsync rejects a different frozen destination without mutating the claim. Real native SaveManager/RunSaveManager and Ritsu memory-store entry/close/readback/write-failure retry checks also passed. Map opening is stubbed; no rendered UI, old deferred room exit or player-save access.");
        }
        finally
        {
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave); Set(manager, "NetService", priorNetwork);
            Set(manager, "EventSynchronizer", priorSynchronizer); Set(manager, "ActionExecutor", priorExecutor);
            Set(CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
        }
    }

    private static void CheckDiogenesSlotAndRecovery()
    {
        var (fixture, run, original, shared) = Fixture();
        string beforeJourney = JsonSerializer.Serialize(Get(shared, "WesternJourney"));
        string rng = JsonSerializer.Serialize(run.Rng.ToSerializable()), history = JsonSerializer.Serialize(run.MapPointHistory);
        string coords = JsonSerializer.Serialize(run.VisitedMapCoords);
        EventModel chosen = run.Act.PullNextEvent(run);
        Check(chosen is DiogenesStaySwitch && run.CurrentRoomCount == 0 && run.VisitedEventIds.SetEquals([chosen.Id])
            && !run.VisitedEventIds.Contains(original.Id), "The actual native event hook selects Diogenes and records only its returned ID.");
        shared = Current(run);
        object entry = Get(shared, "DiogenesStaySwitchEntry"), node = Get(entry, "NativeNode");
        Check((int)Get(entry, "Version") == 2 && (int)Get(node, "ActIndex") == 2
            && (int)Get(node, "Row") == run.CurrentMapCoord!.Value.row && (int)Get(node, "Column") == run.CurrentMapCoord!.Value.col
            && entry.GetType().GetProperty("SourceRoomId")!.GetValue(entry) is null,
            "A new Diogenes entry freezes the actual third-act question coordinate with explicit version-two semantics.");
        Check(beforeJourney == JsonSerializer.Serialize(Get(shared, "WesternJourney"))
            && history == JsonSerializer.Serialize(run.MapPointHistory) && coords == JsonSerializer.Serialize(run.VisitedMapCoords)
            && rng == JsonSerializer.Serialize(run.Rng.ToSerializable()), "Selecting this practice does not spend Fixed/Question, fabricate history, move or reroll.");
        string encoded = Encode(shared);
        Check(Call("DiogenesStaySwitchEntrySession", "TrySelectNative", run) is null && Encode(shared) == encoded,
            "A visited Diogenes event cannot be allocated or claimed again.");
        ((HashSet<ModelId>)run.VisitedEventIds).Clear();
        Check(Call("DiogenesStaySwitchEntrySession", "TrySelectNative", run) is null && Encode(shared) == encoded,
            "A version-two exact pending room is never rebound to another future question slot.");
        object decoded = Call("PhilosophyRunStateCodec", "Decode", encoded)!;
        Check(Encode(decoded) == encoded, "The full shared codec preserves the native entry without changing its material or practice facts.");
        CheckDiogenesCodec(entry);

        EventRoom room = new(chosen);
        var roundTrip = (EventRoom)AbstractRoom.FromSerializable(room.ToSerializable(), null!)!;
        Check(ReferenceEquals(roundTrip.CanonicalEvent, chosen), "Native room serialization restores the selected Diogenes canonical identity.");
        Set(run, "_currentRooms", new List<AbstractRoom> { room });
        object session = Call("DiogenesStaySwitchEntrySession", "Get", RunManager.Instance)!;
        bool Matches(object record) => (bool)session.GetType().GetMethod("MatchesRoom", Flags)!.Invoke(session, [record, room])!;
        Check(Matches(entry), "The actual sole native Diogenes EventRoom matches its frozen coordinate.");
        var actualPoint = run.CurrentMapPoint!;
        actualPoint.PointType = MapPointType.Monster;
        Check(!Matches(entry), "An ordinary combat point cannot impersonate the saved question slot."); actualPoint.PointType = MapPointType.Unknown;
        var oldRng = run.Rng; Set(run, "Rng", new RunRngSet("987654321"));
        Check(!Matches(entry), "A different real run seed cannot reuse the saved Diogenes record."); Set(run, "Rng", oldRng);

        object legacy = EntryWith(entry, json => { json["Version"] = 1; json.Remove("NativeNode"); json["SourceRoomId"] = 17; });
        MapRoom oldBase = new(); Set(oldBase, "Id", 17);
        Set(run, "_currentRooms", new List<AbstractRoom> { oldBase, room });
        Check(Matches(legacy), "The old two-room Diogenes record still recognizes its exact original map base.");
        Set(oldBase, "Id", 18);
        Check(!Matches(legacy), "A different old base room cannot satisfy the frozen source ID.");
        Set(run, "_currentRooms", new List<AbstractRoom> { room });
        Check(Matches(legacy), "An old native saved sole EventRoom remains closable after its old base object was lost.");
        Put(shared, "DiogenesStaySwitchEntry", legacy);
        Set(run, "_currentRooms", new List<AbstractRoom>());
        Check(Call("DiogenesStaySwitchEntrySession", "TrySelectNative", run) is DiogenesStaySwitch
            && ReferenceEquals(Get(shared, "DiogenesStaySwitchEntry"), legacy),
            "An old shared-only invitation may resume at a real question without changing its original record.");
    }

    private static object EntryWith(object entry, Action<JsonObject> edit)
    {
        JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(entry, entry.GetType()))!.AsObject(); edit(json);
        return JsonSerializer.Deserialize(json.ToJsonString(), entry.GetType())!;
    }

    private static void CheckDiogenesCodec(object entry)
    {
        void Rejected(Action<JsonObject> edit)
        {
            bool rejected = false;
            try { EntryWith(entry, edit); } catch (JsonException) { rejected = true; }
            Check(rejected, "The Diogenes converter rejects an ambiguous or invalid entry version/node.");
        }
        Rejected(json => json.Remove("NativeNode"));
        Rejected(json => json["Version"] = 1);
        Rejected(json => json["Version"] = 3);
        Rejected(json => json["NativeNode"]!["Row"] = -1);
        Rejected(json => json["NativeNode"]!["ActIndex"] = 1);
        Rejected(json => json["SourceRoomId"] = 17);
        object legacy = EntryWith(entry, json => { json["Version"] = 1; json.Remove("NativeNode"); });
        Check((bool)Call("DiogenesStaySwitchEntryPolicy", "IsValid", legacy)!,
            "A missing new node field has explicit valid legacy semantics only for version one.");
    }

    private static object Commit(object prepared)
    {
        Check((bool)Get(prepared, "IsAccepted"), "The real Stoic state transition accepts its lawful predecessor.");
        object state = Get(prepared, "State"), pending = Get(state, "PendingOperation");
        object committed = Call("ZenoRouteStateService", "Commit", state, Get(pending, "OperationId"), Get(pending, "CandidateDigest"), Catalog)!;
        Check((bool)Get(committed, "IsAccepted"), "The real Stoic state transition commits its exact prepared candidate.");
        return Get(committed, "State");
    }

    private static object BuildZeno(object shared, RunState run, string stage, string? legacyDestination = null)
    {
        object node = Activator.CreateInstance(T("DiogenesNativeQuestionNode"), [2, run.CurrentMapCoord!.Value.row, run.CurrentMapCoord!.Value.col])!;
        object?[] create = [shared, run.Rng.Seed, node, null];
        Check((bool)T("DiogenesStaySwitchEntryPolicy").GetMethod("TryCreateNative")!.Invoke(null, create)!, "Build the actual eligible Diogenes source record.");
        object entry = create[3]!;
        object feature = Call("DiogenesStaySwitchEntryPolicy", "CreateInitialState", entry)!;
        feature = Commit(Call("ZenoRouteStateService", "PrepareSwitch", feature, 0L, Get(entry, "Material"), Catalog)!);
        entry = EntryWith(entry, json => json["Closed"] = true);
        Put(shared, "DiogenesStaySwitchEntry", entry);
        if (stage != "WaitingInterval")
            feature = Commit(Call("ZenoRouteStateService", "PrepareIntervalCompletion", feature,
                Get(Get(feature, "Route"), "Revision"), "COMPLETED_ACT_2_ROOM_16", Catalog)!);
        if (legacyDestination is not null)
        {
            object route = Get(feature, "Route");
            object prepared = Call("ZenoRouteStateService", "PrepareOpeningClaim", feature, Get(route, "Revision"),
                E("ZenoOpeningTrigger", "SafeBoundary"), E("ZenoResumeDestination", "Map"), legacyDestination,
                "ZENO_NATIVE_QUESTION_CHECK", Catalog)!;
            feature = stage == "PendingOpening" ? Get(prepared, "State") : Commit(prepared);
        }
        Call("PhilosophyRunStateService", "Restore", run, shared);
        CallOn(shared, "SetCurrentZenoRouteState", feature, Catalog);
        return feature;
    }

    private static object? CallOn(object instance, string method, params object?[] args) => instance.GetType().GetMethods(Flags)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(instance, args);

    private static void CheckZenoNativeSelection()
    {
        var (_, waitingRun, _, waitingShared) = Fixture();
        BuildZeno(waitingShared, waitingRun, "WaitingInterval");
        string waiting = Encode(waitingShared);
        Check(Call("ZenoRouteNativeTriggerRegistry", "TrySelectNative", waitingRun) is null && Encode(waitingShared) == waiting,
            "Switching alone cannot skip the real intervening-room requirement.");

        var (_, run, original, shared) = Fixture();
        object feature = BuildZeno(shared, run, "ReadyToClaim");
        string ready = Encode(shared), rng = JsonSerializer.Serialize(run.Rng.ToSerializable());
        EventModel chosen = run.Act.PullNextEvent(run);
        Check(chosen is ZenoAssentBoundary && run.CurrentRoomCount == 0 && run.VisitedEventIds.SetEquals([chosen.Id])
            && !run.VisitedEventIds.Contains(original.Id) && Encode(shared) == ready
            && Get(Get(feature, "Route"), "Stage").ToString() == "ReadyToClaim"
            && rng == JsonSerializer.Serialize(run.Rng.ToSerializable()),
            "Native Zeno selection owns one actual question, but does not claim/open until the selected real room starts.");
        Check(Call("ZenoRouteNativeTriggerRegistry", "TrySelectNative", run) is null && Encode(shared) == ready,
            "A visited native Zeno event cannot be selected again or change the business payload.");
        string id = (string)Call("ZenoRouteNativeTriggerRegistry", "NativeDestinationId", run)!;
        bool Matches(string candidate) => (bool)Call("ZenoRouteNativeTriggerRegistry", "MatchesNativeDestination", run, candidate)!;
        Check(Matches(id) && !Matches(id + "_EXTRA") && !Matches(id.Replace("ACT_2", "ACT_1"))
            && !Matches(id.Replace("ROW_0", "ROW_00")), "New native destinations require the exact normalized seed, act and actual question coordinate.");
        RunRngSet priorRng = run.Rng; Set(run, "Rng", new RunRngSet("OTHER_SEED"));
        Check(!Matches(id), "A native Zeno destination cannot bind to another actual run."); Set(run, "Rng", priorRng);
        run.CurrentMapPoint!.PointType = MapPointType.Shop;
        Check(!Matches(id), "A shop roll cannot masquerade as a native question event."); run.CurrentMapPoint.PointType = MapPointType.Unknown;
        EventRoom room = new(chosen);
        EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(room.ToSerializable(), null!)!;
        Check(ReferenceEquals(restored.CanonicalEvent, chosen), "The native room DTO preserves the chosen Stoic Zeno event.");
    }

    private static object Runtime(RunState run)
    {
        object?[] args = [run, Catalog, null];
        Check((bool)T("ZenoRouteRuntimeService").GetMethod("TryGetOrCreate")!.Invoke(null, args)!,
            "The real persistence runtime belongs to this exact native run.");
        return args[2]!;
    }

    private static async Task Execute(object runtime, object prepared)
    {
        Check((bool)Get(prepared, "IsAccepted"), "The actual saved transition accepts the current revision.");
        var task = (Task)CallOn(runtime, "ExecuteAsync", prepared, default(CancellationToken))!;
        await task;
        Check(Get(task.GetType().GetProperty("Result")!.GetValue(task)!, "Status").ToString() == "Completed",
            "The real native writer and Ritsu readback confirm the transition.");
    }

    private static EventModel Configure(RunState run, NativeRuntimeFixture fixture, EventRoom room)
    {
        EventModel local = room.CanonicalEvent.ToMutable(); Set(local, "Owner", fixture.Player);
        object synchronizer = RuntimeHelpers.GetUninitializedObject(typeof(RunManager).GetProperty("EventSynchronizer")!.PropertyType);
        Set(synchronizer, "_events", new List<EventModel> { local }); Set(synchronizer, "_playerCollection", run);
        Set(synchronizer, "_localPlayerId", fixture.Player.NetId); Set(synchronizer, "_pendingOptionTasks", new List<Task>());
        Set(RunManager.Instance, "EventSynchronizer", synchronizer);
        var configure = (Action<EventModel>?)typeof(EventRoom).GetProperty(nameof(EventRoom.OnStart), Flags)!.GetValue(room);
        Check(configure is not null, "Only a confirmed entry has a native OnStart configuration."); configure!(local);
        return local;
    }

    private static void CheckStored(NativeEventMemorySaveFixture saves, Type type, bool closed, object shared)
    {
        var loaded = saves.Manager.LoadRunSave();
        Check(loaded.Success && loaded.SaveData!.PreFinishedRoom is { } saved
            && Equals(saved.EventId, ModelDb.GetId(type)) && saved.IsPreFinished == closed,
            "The actual native loader reads the exact event and its real completion marker.");
        object read = Call("PhilosophyRunStateRitsuSaveReader", "Read", saves.Json)!;
        Check(Get(read, "Classification").ToString() == "Current" && (string)Get(read, "EncodedState") == Encode(shared),
            "The actual written Ritsu document matches the entire current business payload.");
    }

    private static async Task<bool> FailedWrite(Func<Task<bool>> action)
    {
        try { return await action(); } catch (System.IO.IOException) { return false; }
    }

    private static async Task CheckRealPersistenceAndRetry()
    {
        {
            var (fixture, run, _, _) = Fixture();
            EventModel selected = run.Act.PullNextEvent(run);
            Check(selected is DiogenesStaySwitch, "The persistence test begins at the real selected Diogenes slot.");
            EventRoom room = new(selected); Set(run, "_currentRooms", new List<AbstractRoom> { room });
            using var saves = new NativeEventMemorySaveFixture(fixture, run);
            object shared = Current(run), session = Call("DiogenesStaySwitchEntrySession", "Get", RunManager.Instance)!;
            saves.FailWrites = true;
            Check(!await FailedWrite(() => (Task<bool>)CallOn(session, "PrepareEventRoomAsync", room)!)
                && typeof(EventRoom).GetProperty(nameof(EventRoom.OnStart), Flags)!.GetValue(room) is null
                && !(bool)Get(Get(shared, "DiogenesStaySwitchEntry"), "Closed"),
                "A real failed entry write cannot configure or complete the Diogenes event.");
            saves.FailWrites = false;
            Check(await (Task<bool>)CallOn(session, "PrepareEventRoomAsync", room)!, "The same Diogenes entry retries successfully.");
            CheckStored(saves, typeof(DiogenesStaySwitch), false, shared);
            EventModel local = Configure(run, fixture, room);
            object runtime = Runtime(run), state = Get(runtime, "CurrentState");
            await Execute(runtime, Call("ZenoRouteStateService", "PrepareStay", state, Get(Get(state, "Route"), "Revision"), Catalog)!);
            saves.FailWrites = true;
            int proceed = saves.ProceedAttempts;
            Check(!await FailedWrite(() => (Task<bool>)CallOn(session, "CloseAsync")!) && saves.ProceedAttempts == proceed,
                "A failed Diogenes close checkpoint cannot open the map.");
            saves.FailWrites = false;
            Check(await (Task<bool>)CallOn(session, "CloseAsync")! && local.IsFinished && saves.ProceedAttempts == proceed + 1,
                "A confirmed Diogenes close finishes the actual event before requesting its own map.");
            CheckStored(saves, typeof(DiogenesStaySwitch), true, shared);
            string closed = Encode(shared); int writes = saves.SaveAttempts;
            Check(await (Task<bool>)CallOn(session, "CloseAsync")! && Encode(shared) == closed && saves.SaveAttempts == writes
                && run.CurrentRoomCount == 1 && run.VisitedMapCoords.Count == 1,
                "Repeating a confirmed close cannot change the route, write again, or travel to another coordinate.");
        }
        {
            var (fixture, run, _, shared) = Fixture(); BuildZeno(shared, run, "ReadyToClaim");
            EventModel selected = run.Act.PullNextEvent(run);
            Check(selected is ZenoAssentBoundary, "The persistence test begins at the real selected Zeno slot.");
            EventRoom room = new(selected); Set(run, "_currentRooms", new List<AbstractRoom> { room });
            using var saves = new NativeEventMemorySaveFixture(fixture, run);
            object?[] args = [RunManager.Instance, null];
            Check((bool)T("ZenoRouteNativeTriggerRegistry").GetMethod("TryGet")!.Invoke(null, args)!, "Create the actual native Zeno session.");
            object session = args[1]!;
            saves.FailWrites = true;
            Check(!await FailedWrite(() => (Task<bool>)CallOn(session, "PrepareRestoredEventRoomAsync", room, default(CancellationToken))!)
                && Get(Get(Get(shared, "ZenoRoutePayload"), "State"), "PendingOperation") is null,
                "The initial failed Zeno room save cannot claim an opening or expose options.");
            saves.FailWrites = false;
            Check(await (Task<bool>)CallOn(session, "PrepareRestoredEventRoomAsync", room, default(CancellationToken))!,
                "The same real native Zeno room confirms its claim and event startup.");
            object runtime = Runtime(run), state = Get(runtime, "CurrentState"), route = Get(state, "Route");
            Check(Get(route, "Stage").ToString() == "EventActive" && (bool)Call("ZenoRouteNativeTriggerRegistry", "MatchesNativeDestination", run, Get(route, "ResumeDestinationId"))!,
                "Opening establishment binds to this exact seed and question coordinate.");
            CheckStored(saves, typeof(ZenoAssentBoundary), false, shared);
            EventModel local = Configure(run, fixture, room);
            await Execute(runtime, Call("ZenoRouteStateService", "PrepareOutcome", state, Get(route, "Revision"), E("ZenoAssentOutcome", "NoAssent"), null, Catalog)!);
            string id = (string)Get(Get(Get(runtime, "CurrentState"), "Route"), "EventInstanceId");
            saves.FailWrites = true; int proceed = saves.ProceedAttempts;
            Check(!await FailedWrite(() => (Task<bool>)CallOn(session, "CloseAndResumeAsync", id, default(CancellationToken))!)
                && saves.ProceedAttempts == proceed, "A failed native Zeno close cannot resume or travel.");
            saves.FailWrites = false;
            Check(await (Task<bool>)CallOn(session, "CloseAndResumeAsync", id, default(CancellationToken))!
                && local.IsFinished && saves.ProceedAttempts == proceed + 1, "Zeno retries and confirms its same native close before opening the map.");
            CheckStored(saves, typeof(ZenoAssentBoundary), true, shared);
            Check(run.CurrentRoomCount == 1 && run.VisitedMapCoords.Count == 1, "The native Zeno close preserves the actual node without extra room or travel.");
        }
        Console.WriteLine("Stoic native memory-save checks passed: real Ritsu entry/transaction/close writes and native loads, deliberate failures and stable retries; no map rendering or player store.");
    }

    private static int _retryAttempts;
    private static void CountRetryAttempt()
    {
        _retryAttempts++;
        // This rejection test must never reach any save store, including if the
        // old production ordering regresses. The prefix records and stops it.
        throw new InvalidOperationException("ENC199 forbidden persistence retry before destination rejection.");
    }

    private static async Task CheckFrozenDestinationMismatch()
    {
        var harmony = new Harmony("STS2Philosophers.ENC199.RejectBeforeRetry");
        harmony.Patch(T("ZenoRoutePersistenceRuntime").GetMethod("RetryAsync")!,
            prefix: new HarmonyMethod(typeof(StoicQuestionEntryNativeChecks).GetMethod(nameof(CountRetryAttempt), Flags)!));
        try { await CheckFrozenDestinationMismatchCore(); }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static async Task CheckFrozenDestinationMismatchCore()
    {
        foreach (string stage in new[] { "OpeningClaimed", "PendingOpening" })
        {
            var (_, run, _, shared) = Fixture();
            BuildZeno(shared, run, stage, "MAP_ACT_2_ROOM_17");
            MapRoom currentBase = new(); Set(currentBase, "Id", 18);
            Set(run, "_currentRooms", new List<AbstractRoom> { currentBase });
            object?[] find = [RunManager.Instance, null];
            Check((bool)T("ZenoRouteNativeTriggerRegistry").GetMethod("TryGet")!.Invoke(null, find)!, "Restore the real legacy native session.");
            object session = find[1]!;
            Check((bool)Get(session, "CanResumeLegacyClaim"), "An old prepared or committed opening retains its frozen-transaction recovery gate.");
            // Use the current room's real plan. Supplying a frozen ID here
            // would hide the old production bug instead of reproducing it.
            object boundary = Activator.CreateInstance(T("ZenoRouteNativeMapBoundary"),
                [2, 1, 18, false, false, E("ZenoRouteObservedEventKind", "None")])!;
            object?[] args = [boundary, null, null];
            Check((bool)T("ZenoRouteNativeTriggerPolicy").GetMethod("TryCreateMapPlan")!.Invoke(null, args)!, "Create the real currently observed map plan.");
            string before = Encode(shared);
            // Pending opening used to run a persistence retry before checking
            // the actual destination; the new gate must reject before any save.
            int beforeRetries = _retryAttempts;
            var task = (Task)CallOn(session, "TriggerAsync", args[1], currentBase, default(CancellationToken))!;
            Exception? failure = null;
            try { await task; } catch (Exception error) { failure = error; }
            Check(_retryAttempts == beforeRetries,
                "ENC199: a mismatched legacy destination must be rejected before calling persistence RetryAsync.");
            if (failure is not null) throw failure;
            object status = task.GetType().GetProperty("Result")!.GetValue(task)!;
            Check(_retryAttempts == beforeRetries && status.ToString() == "Blocked" && Encode(shared) == before && run.CurrentRoomCount == 1
                && ReferenceEquals(run.CurrentRoom, currentBase) && CallOn(session, "ReadDestination", currentBase) is null,
                "The actual legacy TriggerAsync rejects a different current destination before retry, relabel, event creation or room entry.");
        }
    }
}
