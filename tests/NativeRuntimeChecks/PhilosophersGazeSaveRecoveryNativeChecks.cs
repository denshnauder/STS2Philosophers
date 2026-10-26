using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using HarmonyLib;
using STS2Philosophers;
using STS2RitsuLib.RunData;
using static NativeRuntimeFixture;

// Every native save and Ritsu document in this test belongs to an in-memory fixture.
// No game room is entered and no player save store is read or written.
internal static class PhilosophersGazeSaveRecoveryNativeChecks
{
    private static readonly Assembly Mod = typeof(PhilosophersGaze).Assembly;
    private static readonly Type Registry = typeof(RunSavedDataOptions).Assembly.GetType("STS2RitsuLib.RunData.RunSavedDataRegistry")!;
    private const BindingFlags Methods = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type T(string name) => Mod.GetType("STS2Philosophers." + name)!;
    private static object? Call(Type type, string method, params object?[] args) => type.GetMethods(Methods)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(null, args);
    private static object? Call(string type, string method, params object?[] args) => Call(T(type), method, args);
    private static object Get(object value, string name) => value.GetType().GetProperty(name, Methods)!.GetValue(value)!;
    private static string Encoded(object shared) => (string)Call("PhilosophyRunStateCodec", "Encode", shared)!;

    private sealed record Fixture(NativeRuntimeFixture Native, RunState Run, MapPointHistoryEntry History, object Shared);

    private static Fixture Create(bool hasRelic = false)
    {
        Set(RunManager.Instance, "State", null);
        var native = new NativeRuntimeFixture(typeof(HeraclitusLyre));
        ModelDb.Inject(typeof(Ironclad)); Set(native.Player, "Character", ModelDb.Character<Ironclad>());
        if (!hasRelic) ((List<RelicModel>)native.Player.Relics).Clear();
        Set(CombatManager.Instance, "_turnState", null);
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player> { native.Player }); Set(run, "_currentActIndex", 0);
        Set(run, "Rng", new RunRngSet("ENC197_INITIAL_EVENT")); Set(run, "ActFloor", 1);
        Set(run, "_visitedMapCoords", new List<MapCoord> { new(0, 0) });
        Set(run, "_visitedEventIds", new HashSet<ModelId>());
        Set(run, "_currentRooms", new List<AbstractRoom>());
        Set(run, "Modifiers", Array.Empty<ModifierModel>()); Set(run, "BadgeModels", Array.Empty<BadgeModel>());
        Set(native.Player, "_runState", run); Set(native.Combat, "RunState", run);
        MapPointHistoryEntry history = new()
        {
            MapPointType = MapPointType.Ancient,
            Rooms = [new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<Neow>() },
                new() { RoomType = RoomType.Event, ModelId = ModelDb.GetId<PhilosophersGaze>() }],
            PlayerStats = [new() { PlayerId = native.Player.NetId }],
        };
        Set(run, "_mapPointHistory", new List<List<MapPointHistoryEntry>> { new() { history } });
        Set(RunManager.Instance, "State", run); Set(RunManager.Instance, "ShouldSave", true);
        Set(RunManager.Instance, "NetService", new NetSingleplayerGameService());
        object shared = Activator.CreateInstance(T("PhilosophyRunState"), nonPublic: true)!;
        Call("PhilosophersGazeActOneCandidatePolicy", "GetOrGenerate", shared, run.Rng.Seed);
        return new(native, run, history, shared);
    }

    private static string Json(Fixture f)
    {
        SerializableRun native = new() { SchemaVersion = 20,
            Players = [new() { NetId = f.Native.Player.NetId }],
            VisitedMapCoords = f.Run.VisitedMapCoords.ToList(),
            MapPointHistory = [f.Run.MapPointHistory[0].ToList()], PreFinishedRoom = null };
        JsonObject root = JsonNode.Parse(JsonSerializationUtility.ToJson(native))!.AsObject();
        var payload = (PhilosophyRunStateRitsuPayload)Call(typeof(PhilosophyRunStateRitsuPayload), "Bind", f.Shared)!;
        root["_ritsulib"] = new JsonObject { ["version"] = 1, ["run_saved_data"] = new JsonObject
        {
            ["STS2Philosophers"] = new JsonObject { ["PHILOSOPHY_RUN_STATE"] = new JsonObject
            {
                ["schema"] = 1, ["kind"] = "run", ["data"] = new JsonObject
                { ["SchemaVersion"] = 1, ["EncodedState"] = payload.EncodedState },
            } },
        } };
        return root.ToJsonString();
    }

    private static void Import(Fixture f, string? json = null)
    {
        string content = json ?? Json(f);
        var store = new InMemoryRunSaveStore(content);
        var loader = new RunSaveManager(2, store, new MigrationManager(store), false);
        var read = loader.LoadRunSave();
        Check(read.Success && read.SaveData is { PreFinishedRoom: null }, "The actual native loader reads the legacy null-room fixture.");
        SerializableRun save = read.SaveData!;
        Set(f.Run, "_mapPointHistory", save.MapPointHistory.Select(row => row.ToList()).ToList());
        Call(Registry, "AttachDocumentFromJson", save, content);
        Call("PhilosophyRunStateRitsuStore", "ObserveLoadedSave", save, content, "enc197/current_run.save");
        Call(Registry, "Import", save, f.Run);
        Call("PhilosophyRunStateRitsuStore", "ObserveRestoredRun", save, f.Run);
    }

    private static object CurrentShared(Fixture f)
    {
        object?[] args = [f.Run, null];
        Check((bool)Call("PhilosophyRunStateService", "TryGet", args)!, "The fixture's real Ritsu import resolves its business state.");
        return args[1]!;
    }
    private static bool Resolved(Fixture f) => (bool)Call("PhilosophersGazeSaveRecovery", "IsResolved", f.Native.Player)!;
    private static EventRoom? Recover(AbstractRoom? original = null)
    {
        object?[] args = [RunManager.Instance, original];
        Call("PhilosophersGazeSaveRecovery", "TryRestoreLegacyRoom", args);
        return args[1] as EventRoom;
    }

    private static void AddChoice(Fixture f, string key, string table = "events")
    {
        LocString title;
        if (table == "events")
        {
            EventModel local = ModelDb.Event<PhilosophersGaze>().ToMutable(); Set(local, "Owner", f.Native.Player);
            EventOption native = new(local, () => Task.CompletedTask, key[..^".title".Length], Array.Empty<IHoverTip>());
            title = native.HistoryName;
        }
        else title = new LocString(table, key);
        f.History.PlayerStats[0].EventChoices.Add(new() { Title = title });
    }

    private static void CheckOpenAndResolved()
    {
        var open = Create(); Import(open);
        string before = Encoded(CurrentShared(open));
        EventRoom room = Recover()!;
        Check(room is { IsPreFinished: false } && room.CanonicalEvent is PhilosophersGaze && !Resolved(open),
            "Completed Neow plus the actual old candidates restores the still-open invitation, never a new Neow.");
        Check(Encoded(CurrentShared(open)) == before, "Legacy recovery preserves the original three candidates and doctrine.");
        CheckRoom(room, open.Native.Player, closed: false);
        Check(Encoded(CurrentShared(open)) == before && open.Native.Player.Relics.Count == 0,
            "Opening an old unclaimed invitation grants nothing and does not regenerate the saved candidates.");

        var claimed = Create(hasRelic: true); Import(claimed);
        int count = claimed.Native.Player.Relics.Count;
        Check(Resolved(claimed) && Recover() is { IsPreFinished: true }, "The actually held doctrine relic closes a legacy invitation.");
        CheckRoom(Recover()!, claimed.Native.Player, closed: true);
        Check(claimed.Native.Player.Relics.Count == count, "Repeated native restoration cannot grant another relic.");

        var doctrine = Create();
        doctrine.Shared.GetType().GetMethod("RecordCurrentDoctrine")!.Invoke(doctrine.Shared, ["HERACLITUS", "HERACLITUS_LYRE", new[] { "WESTERN", "BEING_AND_CHANGE" }]);
        Import(doctrine);
        Check(Resolved(doctrine) && Recover() is { IsPreFinished: true } && doctrine.Native.Player.Relics.Count == 0,
            "A persisted doctrine fact prevents automatic backfill when the carrier is absent.");
    }

    private static void CheckHistory()
    {
        foreach (string suffix in new[] { "ACT_ONE_DECLINE_CONFIRM.options.CONFIRM", "KONGZI_VIEWPOINTS.options.DECLINE",
            "MOZI_VIEWPOINTS.options.DECLINE", "LAOZI_VIEWPOINTS.options.DECLINE", "WESTERN.options.DECLINE" })
        {
            var f = Create(); AddChoice(f, $"PHILOSOPHERS_GAZE.pages.{suffix}.title"); Import(f);
            Check(Resolved(f) && Recover() is { IsPreFinished: true }, "Only an actual terminal first-act decline closes a rewardless old invitation.");
        }
        foreach (string suffix in new[] { "INITIAL.options.DECLINE", "WESTERN.options.BACK_UNIFIED", "INITIAL.options.KONGZI",
            "WESTERN.options.HERACLITUS", "WESTERN.options.BEING_AND_CHANGE", "ACT_TWO_DECLINE_CONFIRM.options.CONFIRM" })
        {
            var f = Create(); AddChoice(f, $"PHILOSOPHERS_GAZE.pages.{suffix}.title"); Import(f);
            Check(!Resolved(f) && Recover() is { IsPreFinished: false },
                "Navigation and an asynchronous accept-click without an actual grant never prove completion.");
        }
        var foreign = Create(); AddChoice(foreign, "PHILOSOPHERS_GAZE.pages.WESTERN.options.DECLINE.title", "other_mod"); Import(foreign);
        Check(!Resolved(foreign), "A different localization table cannot impersonate this event's decline.");
        var otherPlayer = Create(); otherPlayer.History.PlayerStats[0].PlayerId++;
        AddChoice(otherPlayer, "PHILOSOPHERS_GAZE.pages.WESTERN.options.DECLINE.title"); Import(otherPlayer);
        Check(!Resolved(otherPlayer), "Another player's choice cannot close the local player's invitation.");
        var ambiguous = Create();
        AddChoice(ambiguous, "PHILOSOPHERS_GAZE.pages.WESTERN.options.BEING_AND_CHANGE.title");
        AddChoice(ambiguous, "PHILOSOPHERS_GAZE.pages.WESTERN.options.DECLINE.title"); Import(ambiguous);
        Check(!Resolved(ambiguous) && Recover() is { IsPreFinished: false },
            "An uncommitted async accept followed by a decline click does not fabricate a completed resolution.");
        EventModel blocked = ModelDb.Event<PhilosophersGaze>().ToMutable(); Set(blocked, "Owner", ambiguous.Native.Player);
        typeof(PhilosophersGaze).GetMethod("SetInitialEventState", Methods)!.Invoke(blocked, [false]);
        Check(!blocked.IsFinished && blocked.CurrentOptions is [{ IsLocked: true }],
            "Contradictory legacy clicks restore a locked page, preserving both the invitation and missing reward.");
        Check(!(bool)typeof(PhilosophersGaze).GetMethod("TryBeginResolution", Methods)!.Invoke(blocked, null)!,
            "An obsolete callback cannot bypass the contradictory-history recovery lock.");
    }

    private static void CheckGuards()
    {
        void Refused(Action<Fixture> mutate, string reason)
        {
            var f = Create(); mutate(f); Import(f);
            Check(Recover() is null, reason);
        }
        Refused(_ => Set(RunManager.Instance, "ShouldSave", false), "A nonpersistent run cannot be repaired.");
        Refused(_ => Set(RunManager.Instance, "NetService", null), "Missing network identity cannot authorize a singleplayer repair.");
        Refused(f => Set(f.Run, "_currentActIndex", 1), "The act-one legacy repair never intercepts act two.");
        Refused(f => Set(f.Run, "_players", new List<Player> { f.Native.Player, f.Native.Player }), "Multiplayer is not repaired by the singleplayer event path.");
        Refused(f => ((List<MapCoord>)f.Run.VisitedMapCoords).Add(new(0, 1)), "An already traveled run cannot be sent back to the first invitation.");
        Refused(f => f.History.MapPointType = MapPointType.Unknown, "A question node cannot impersonate the initial ancient node.");
        Refused(f => f.History.Rooms.RemoveAt(0), "A historical Gaze without the Neow predecessor is not fabricated into a repair.");
        Refused(f => f.History.Rooms.Add(new() { RoomType = RoomType.Shop, ModelId = new("EVENT", "OTHER") }),
            "An unrelated room stack remains unchanged.");
        Refused(f => ((System.Collections.IDictionary)Get(f.Shared, "GeneratedCandidates")).Clear(),
            "Absent old candidates never prove Neow was completed.");
        Refused(f => ((IList<string>)Get(((System.Collections.IDictionary)Get(f.Shared, "GeneratedCandidates"))["ACT_ONE_THINKERS"]!, "CandidateIds")).Clear(),
            "Malformed old candidates cannot trigger silent regeneration.");
        var explicitRoom = Create(); Import(explicitRoom);
        EventRoom existing = new(ModelDb.Event<Neow>());
        Check(ReferenceEquals(Recover(existing), existing), "An explicit native room is always preserved by reference.");
        var malformed = Create(); JsonObject json = JsonNode.Parse(Json(malformed))!.AsObject();
        json["_ritsulib"]!["run_saved_data"]!["STS2Philosophers"]!["PHILOSOPHY_RUN_STATE"]!["schema"] = 999;
        Import(malformed, json.ToJsonString());
        Check(Recover() is null && malformed.Native.Player.Relics.Count == 0, "An isolated future Ritsu slot is neither repaired nor awarded.");
        object document = Call(Registry, "Export", malformed.Run)!;
        JsonObject exported = (JsonObject)document.GetType().GetMethod("ToRootObject")!.Invoke(document, null)!;
        Check(JsonNode.DeepEquals(json["_ritsulib"]!["run_saved_data"]!["STS2Philosophers"],
            exported["_ritsulib"]!["run_saved_data"]!["STS2Philosophers"]), "The real exporter preserves the unsupported original slot.");
    }

    private static async Task CheckNativeHistoryTiming()
    {
        var f = Create(); Import(f);
        EventModel local = ModelDb.Event<PhilosophersGaze>().ToMutable(); Set(local, "Owner", f.Native.Player);
        TaskCompletionSource pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool completed = false;
        EventOption option = new(local, async () => { await pending.Task; completed = true; },
            "PHILOSOPHERS_GAZE.pages.WESTERN.options.BEING_AND_CHANGE", Array.Empty<IHoverTip>());
        EventOption decline = new(local,
            () => (Task)typeof(PhilosophersGaze).GetMethod("ConfirmActOneDecline", Methods)!.Invoke(local, null)!,
            "PHILOSOPHERS_GAZE.pages.WESTERN.options.DECLINE", Array.Empty<IHoverTip>());
        Set(local, "_currentOptions", new List<EventOption> { option, decline });
        var synchronizer = (EventSynchronizer)RuntimeHelpers.GetUninitializedObject(typeof(EventSynchronizer));
        Set(synchronizer, "_events", new List<EventModel> { local }); Set(synchronizer, "_playerCollection", f.Run);
        Set(synchronizer, "_localPlayerId", f.Native.Player.NetId); Set(synchronizer, "_pendingOptionTasks", new List<Task>());
        Type logger = typeof(EventModel).Assembly.GetType("MegaCrit.Sts2.Core.Logging.Logger")!;
        Type logType = typeof(EventModel).Assembly.GetType("MegaCrit.Sts2.Core.Logging.LogType")!;
        Set(synchronizer, "_logger", Activator.CreateInstance(logger, ["ENC197_NATIVE_HISTORY", Enum.ToObject(logType, 0)])!);
        Check((bool)typeof(PhilosophersGaze).GetMethod("TryBeginResolution", Methods)!.Invoke(local, null)!,
            "The actual Gaze resolution gate is held while a reward callback is pending.");
        typeof(EventSynchronizer).GetMethod("ChooseOptionForEvent", Methods)!.Invoke(synchronizer, [f.Native.Player, 0]);
        Check(!completed && f.Run.CurrentMapPointHistoryEntry!.PlayerStats[0].EventChoices.Count == 1,
            "The actual native event synchronizer writes the click history while its asynchronous reward is still pending.");
        EventOptionHistoryEntry chosen = f.Run.CurrentMapPointHistoryEntry!.PlayerStats[0].EventChoices[0];
        Check(chosen.Title.LocTable == option.HistoryName.LocTable && chosen.Title.LocEntryKey == option.HistoryName.LocEntryKey,
            "The native history stores the option's actual localized key, not its translated display text.");
        Check(!Resolved(f), "A native recorded accept-click without the completed reward cannot close an invitation.");
        typeof(EventSynchronizer).GetMethod("ChooseOptionForEvent", Methods)!.Invoke(synchronizer, [f.Native.Player, 1]);
        Check(!local.IsFinished && f.Run.CurrentMapPointHistoryEntry!.PlayerStats[0].EventChoices.Count == 2
            && !(bool)Call("PhilosophersGazeSaveRecovery", "IsResolved", f.Native.Player)!,
            "The native synchronizer can record a decline while an accept is pending, but the real resolution gate refuses completion.");
        Check((bool)Call("PhilosophersGazeSaveRecovery", "HasAmbiguousResolution", f.Native.Player)!,
            "The exact two native click records become a blocked legacy ambiguity instead of a lost reward.");
        typeof(PhilosophersGaze).GetMethod("EndResolution", Methods)!.Invoke(local, null);
        pending.SetResult();
        var tasks = (List<Task>)typeof(EventSynchronizer).GetField("_pendingOptionTasks", Methods)!.GetValue(synchronizer)!;
        await Task.WhenAll(tasks);
        Check(completed, "The native pending-option task still completes normally after the ordering check.");
    }

    private static void CheckPatchBinding()
    {
        MethodBase target = (MethodBase)Call("PhilosophersGazeSaveRecoveryPatch", "TargetMethod")!;
        Check(target.DeclaringType == typeof(RunManager) && target.Name == "LoadIntoLatestMapCoord"
            && target.GetParameters().Length == 1 && target.GetParameters()[0].ParameterType == typeof(AbstractRoom)
            && target.GetParameters()[0].Name == "preFinishedRoom", "The actual recovery prefix binds before native room reconstruction with its real parameter name.");
        var f = Create(); Import(f);
        object?[] args = [RunManager.Instance, null];
        Call("PhilosophersGazeSaveRecoveryPatch", "Prefix", args);
        Check(args[1] is EventRoom { CanonicalEvent: PhilosophersGaze, IsPreFinished: false },
            "The production prefix actually replaces a legacy null room with the still-open saved invitation.");
    }

    private static int _saveAttempts;
    private static void CountSaveAttempt() => _saveAttempts++;
    private static bool SkipProgressFixture() => false;

    private static async Task CheckRealSaveAndRetry()
    {
        MethodInfo snapshotState = typeof(ZenoCivicSealUpgradeNativeChecks).GetMethod("SnapshotStaticState", Methods)!;
        var restoreCache = (List<Action>)snapshotState.Invoke(null, [typeof(ModelIdSerializationCache)])!;
        var restoreAssemblies = (List<Action>)snapshotState.Invoke(null, [typeof(AssemblyInfo)])!;
        var f = Create(hasRelic: true); Import(f);
        Player player = f.Native.Player;
        ModelDb.Inject(typeof(Ironclad));
        Set(player, "Character", ModelDb.Character<Ironclad>());
        var rng = new MegaCrit.Sts2.Core.Random.PlayerRngSet(197);
        Set(player, "PlayerRng", rng); Set(player, "PlayerOdds", new MegaCrit.Sts2.Core.Odds.PlayerOddsSet(rng));
        Set(player, "RelicGrabBag", new RelicGrabBag()); Set(player, "Deck", new MegaCrit.Sts2.Core.Entities.Cards.CardPile(MegaCrit.Sts2.Core.Entities.Cards.PileType.Deck));
        Set(player, "ExtraFields", new ExtraPlayerFields()); Set(player, "UnlockState", new UnlockState([], [], 1));
        foreach (string name in new[] { "DiscoveredCards", "DiscoveredRelics", "DiscoveredPotions", "DiscoveredEnemies" }) Set(player, name, new List<ModelId>());
        Set(player, "DiscoveredEpochs", new List<string>());
        Set(f.Run, "Acts", Array.Empty<ActModel>()); Set(f.Run, "Odds", new MegaCrit.Sts2.Core.Odds.RunOddsSet(f.Run.Rng.UnknownMapPoint));
        Set(f.Run, "SharedRelicGrabBag", new RelicGrabBag()); Set(f.Run, "ExtraFields", new ExtraRunFields());
        EventModel local = ModelDb.Event<PhilosophersGaze>().ToMutable(); Set(local, "Owner", player);
        object synchronizer = RuntimeHelpers.GetUninitializedObject(typeof(EventSynchronizer));
        Set(synchronizer, "_events", new List<EventModel> { local }); Set(synchronizer, "_playerCollection", f.Run);
        Set(synchronizer, "_localPlayerId", player.NetId);
        Set(RunManager.Instance, "EventSynchronizer", synchronizer);
        EventRoom room = new(ModelDb.Event<PhilosophersGaze>());
        Set(f.Run, "_currentRooms", new List<AbstractRoom> { room });
        var store = new MemoryWrites();
        SaveManager mock = new(store, false);
        Set(mock, "_runSaveManager", new RunSaveManager(2, store, new MigrationManager(store), false));
        FieldInfo mockField = typeof(SaveManager).GetField("_mockInstance", Methods)!;
        object? priorMock = mockField.GetValue(null); mockField.SetValue(null, mock);
        var harmony = new Harmony("STS2Philosophers.ENC197.NativeSaveFixture");
        try
        {
            AssemblyInfo.Init();
            AssemblyInfo.ModMap![Mod] = new Mod { path = "isolated_enc197_memory_fixture",
                manifest = new ModManifest { id = "STS2Philosophers", affectsGameplay = true }, assemblies = [Mod] };
            ModelIdSerializationCache.ResetForTest(); ModelIdSerializationCache.Init();
            harmony.CreateClassProcessor(T("RouteEventMapBoundarySaveRoomPatch")).Patch();
            harmony.CreateClassProcessor(T("PhilosophyRunStateToSavePatch")).Patch();
            // Apply the real framework save callbacks to our private memory store.
            // The framework normally patches GodotFileIo/CloudSaveStore; neither
            // is used here, so no player file can be touched by this fixture.
            Type RitsuPatch(string name) => Registry.Assembly.GetType("STS2RitsuLib.RunData.Patches." + name)!;
            harmony.Patch(typeof(RunManager).GetMethod(nameof(RunManager.ToSave))!,
                postfix: new HarmonyMethod(RitsuPatch("RunSavedDataToSavePatch").GetMethod("Postfix", Methods)!));
            Type savePatch = RitsuPatch("RunSavedDataSaveRunPatch");
            harmony.Patch(typeof(RunSaveManager).GetMethod(nameof(RunSaveManager.SaveRun), [typeof(SerializableRun), typeof(bool)])!,
                prefix: new HarmonyMethod(savePatch.GetMethod("Prefix", Methods)!),
                postfix: new HarmonyMethod(savePatch.GetMethod("Postfix", Methods)!));
            var writePrefix = new HarmonyMethod(RitsuPatch("RunSavedDataSaveStoreWritePatch").GetMethod("Prefix", Methods)!);
            harmony.Patch(typeof(MemoryWrites).GetMethod(nameof(ISaveStore.WriteFile), [typeof(string), typeof(byte[])])!, prefix: writePrefix);
            harmony.Patch(typeof(MemoryWrites).GetMethod(nameof(ISaveStore.WriteFileAsync), [typeof(string), typeof(byte[])])!, prefix: writePrefix);
            harmony.Patch(typeof(SaveManager).GetMethod("SaveProgressFile", Methods)!,
                prefix: new HarmonyMethod(typeof(PhilosophersGazeSaveRecoveryNativeChecks).GetMethod(nameof(SkipProgressFixture), Methods)!));
            harmony.Patch(typeof(SaveManager).GetMethod(nameof(SaveManager.SaveRun), [typeof(AbstractRoom), typeof(bool)])!,
                prefix: new HarmonyMethod(typeof(PhilosophersGazeSaveRecoveryNativeChecks).GetMethod(nameof(CountSaveAttempt), Methods)!));
            SerializableRun snapshot = RunManager.Instance.ToSave(null!);
            Check(snapshot.PreFinishedRoom is { IsPreFinished: false } checkpoint && Equals(checkpoint.EventId, ModelDb.GetId<PhilosophersGaze>()),
                "The real Harmony-patched native ToSave(null) saves the current Gaze instead of dropping its room.");
            await mock.SaveRun(null!, false);
            var read = mock.LoadRunSave();
            Check(read.Success && read.SaveData!.PreFinishedRoom is { IsPreFinished: false },
                "The actual native writer and native loader preserve the routed Gaze DTO in memory.");
            typeof(PhilosophersGaze).GetMethod("SetInitialEventState", Methods)!.Invoke(local, [true]);
            int relics = player.Relics.Count; _saveAttempts = 0; store.FailWrites = true;
            await (Task)typeof(PhilosophersGaze).GetMethod("ConfirmCompletionAsync", Methods)!.Invoke(local, null)!;
            Check(room.IsPreFinished && !(bool)Get(local, "HasConfirmedCompletion") && _saveAttempts == 1,
                "An actual failed native write marks the room closed but cannot claim confirmed completion.");
            object?[] proceed = [Task.CompletedTask];
            bool original = (bool)Call("NeowProceedPatch", "Prefix", proceed)!;
            Check(!original && proceed[0] is Task, "Proceed is intercepted while completion persistence is unconfirmed.");
            await (Task)proceed[0]!;
            Check(_saveAttempts == 2 && player.Relics.Count == relics && !(bool)Get(local, "HasConfirmedCompletion"),
                "Proceed retries only native persistence after failure, without rerunning a relic grant or bypassing the block.");
            store.FailWrites = false;
            await (Task)typeof(PhilosophersGaze).GetMethod("ConfirmCompletionAsync", Methods)!.Invoke(local, null)!;
            Check((bool)Get(local, "HasConfirmedCompletion") && _saveAttempts == 3 && player.Relics.Count == relics,
                "The real native writer plus Ritsu save hooks confirm a finished Gaze only after its closed room and exact shared state read back.");
            var completedRead = mock.LoadRunSave();
            Check(completedRead.Status == ReadSaveStatus.Success && completedRead.SaveData!.PreFinishedRoom is { IsPreFinished: true } completedRoom
                && Equals(completedRoom.EventId, ModelDb.GetId<PhilosophersGaze>()),
                "The confirmed native save contains the closed Gaze checkpoint.");
            object?[] confirmedProceed = [Task.CompletedTask];
            Check((bool)Call("NeowProceedPatch", "Prefix", confirmedProceed)! && _saveAttempts == 3 && player.Relics.Count == relics,
                "After successful completion readback, Proceed permits native map opening without another save or reward.");
            EventModel nonpersistent = ModelDb.Event<PhilosophersGaze>().ToMutable(); Set(nonpersistent, "Owner", player);
            Set(synchronizer, "_events", new List<EventModel> { nonpersistent });
            typeof(PhilosophersGaze).GetMethod("SetInitialEventState", Methods)!.Invoke(nonpersistent, [true]);
            Set(RunManager.Instance, "ShouldSave", false);
            await (Task)typeof(PhilosophersGaze).GetMethod("ConfirmCompletionAsync", Methods)!.Invoke(nonpersistent, null)!;
            Check((bool)Get(nonpersistent, "HasConfirmedCompletion") && _saveAttempts == 3 && player.Relics.Count == relics,
                "A nonpersistent run completes in memory without writing a save or repeating a grant.");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id); mockField.SetValue(null, priorMock);
            foreach (Action restore in restoreCache) restore();
            foreach (Action restore in restoreAssemblies) restore();
        }
    }

    private sealed class MemoryWrites : ISaveStore
    {
        private readonly Dictionary<string, byte[]> _files = [];
        internal bool FailWrites;
        public string ReadFile(string path) => System.Text.Encoding.UTF8.GetString(_files.TryGetValue(path, out var bytes) ? bytes : throw new FileNotFoundException(path));
        public Task<string?> ReadFileAsync(string path) => Task.FromResult<string?>(ReadFile(path));
        public void WriteFile(string path, string content) => WriteFile(path, System.Text.Encoding.UTF8.GetBytes(content));
        public void WriteFile(string path, byte[] bytes) { if (FailWrites) throw new IOException("ENC197 deliberate in-memory write failure"); _files[path] = bytes.ToArray(); }
        public Task WriteFileAsync(string path, string content) { WriteFile(path, content); return Task.CompletedTask; }
        public Task WriteFileAsync(string path, byte[] bytes) { WriteFile(path, bytes); return Task.CompletedTask; }
        public bool FileExists(string path) => _files.ContainsKey(path);
        public bool DirectoryExists(string path) => true;
        public string GetFullPath(string path) => path;
        public DateTimeOffset GetLastModifiedTime(string path) => DateTimeOffset.UtcNow;
        public int GetFileSize(string path) => _files[path].Length;
        public string[] GetFilesInDirectory(string path) => _files.Keys.Where(key => key.StartsWith(path, StringComparison.Ordinal)).ToArray();
        public string[] GetDirectoriesInDirectory(string path) => [];
        public void DeleteFile(string path) => _files.Remove(path);
        public void RenameFile(string source, string destination) { _files[destination] = _files[source]; _files.Remove(source); }
        public void CreateDirectory(string path) { }
        public void DeleteDirectory(string path) { }
        public void DeleteTemporaryFiles(string path) { }
        public void SetLastModifiedTime(string path, DateTimeOffset value) { }
    }

    private static void CheckRoom(EventRoom room, Player player, bool closed)
    {
        var save = new SerializableRun { SchemaVersion = 20, PreFinishedRoom = room.ToSerializable() };
        string json = JsonSerializationUtility.ToJson(save);
        var store = new InMemoryRunSaveStore(json);
        var loader = new RunSaveManager(2, store, new MigrationManager(store), false);
        var loaded = loader.LoadRunSave();
        Check(loaded.Success && loaded.SaveData?.PreFinishedRoom is { } checkpoint
            && checkpoint.IsPreFinished == closed && Equals(checkpoint.EventId, ModelDb.GetId<PhilosophersGaze>()),
            "The native save loader preserves the exact Gaze identity and open/closed room flag.");
        EventRoom restored = (EventRoom)AbstractRoom.FromSerializable(loaded.SaveData!.PreFinishedRoom, player.RunState)!;
        EventModel local = restored.CanonicalEvent.ToMutable(); Set(local, "Owner", player);
        typeof(PhilosophersGaze).GetMethod("SetInitialEventState", Methods)!.Invoke(local, [restored.IsPreFinished]);
        Check(local.IsFinished == closed && (closed ? local.CurrentOptions.Count == 0 : local.CurrentOptions.Count == 4),
            "Actual Gaze restoration closes without ordinary-event exceptions or restores its saved invitation choices.");
    }

    private static void InitializeCharacterLocalization()
    {
        // EventOption eagerly formats native character details. Only fixture labels
        // are needed; do not initialize the game asset/settings localization loader.
        ModelDb.Inject(typeof(Ironclad)); CharacterModel character = ModelDb.Character<Ironclad>();
        var strings = new[] { "Title", "TitleObject", "PossessiveAdjective", "PronounObject", "PronounPossessive", "PronounSubject" }
            .Select(name => (LocString)typeof(CharacterModel).GetProperty(name, Methods)!.GetValue(character)!).ToArray();
        var tables = (Dictionary<string, LocTable>)typeof(LocManager).GetField("_tables", Methods)!.GetValue(LocManager.Instance)!;
        foreach (var group in strings.GroupBy(loc => loc.LocTable))
        {
            var translations = group.Select(loc => loc.LocEntryKey).Distinct().ToDictionary(key => key, _ => "fixture character");
            tables[group.Key] = (LocTable)Activator.CreateInstance(typeof(LocTable), Methods, null, [group.Key, translations, null], null)!;
        }
    }

    public static async Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        object? priorNetwork = manager.NetService, priorExecutor = manager.ActionExecutor;
        object? priorSynchronizer = manager.EventSynchronizer;
        PropertyInfo locSingleton = typeof(LocManager).GetProperty(nameof(LocManager.Instance), Methods)!;
        object? priorLoc = locSingleton.GetValue(null);
        FieldInfo formatter = typeof(LocManager).GetField("_smartFormatter", Methods)!;
        object? priorFormatter = formatter.GetValue(null);
        PropertyInfo defaultFormatter = formatter.FieldType.Assembly.GetType("SmartFormat.Smart")!.GetProperty("Default", Methods)!;
        object? priorDefaultFormatter = defaultFormatter.GetValue(null);
        object? priorTurn = typeof(CombatManager).GetField("_turnState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(CombatManager.Instance);
        try
        {
            TestMode.IsOn = true;
            ModelDb.Inject(typeof(PhilosophersGaze)); ModelDb.Inject(typeof(Neow));
            typeof(RouteEventMapBoundaryNativeChecks).GetMethod("InitializeLocalization", Methods)!.Invoke(null, null);
            InitializeCharacterLocalization();
            Call("PhilosophyRunStateRitsuStore", "Initialize");
            CheckOpenAndResolved(); CheckHistory(); CheckGuards(); CheckPatchBinding(); await CheckNativeHistoryTiming(); await CheckRealSaveAndRetry();
            Console.WriteLine("ENC197 Gaze recovery native checks passed: native ToSave routing and in-memory writer/loadback with real Ritsu save hooks, closed completion confirmation and Proceed release, failed-write retry without grants, nonpersistent completion, legacy null-room identity/flag recovery, unchanged candidates, carrier/doctrine and exact decline history, native history-before-async-reward timing, ambiguous-click lock, real load-prefix binding and singleplayer/initial-node/foreign-room/malformed isolation; no rendered game or player-save access.");
        }
        finally
        {
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave); Set(manager, "NetService", priorNetwork);
            Set(manager, "EventSynchronizer", priorSynchronizer); locSingleton.SetValue(null, priorLoc);
            formatter.SetValue(null, priorFormatter); defaultFormatter.SetValue(null, priorDefaultFormatter);
            Set(manager, "ActionExecutor", priorExecutor); Set(CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
        }
    }
}
