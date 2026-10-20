using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using STS2Philosophers;
using STS2RitsuLib.RunData;
using static NativeRuntimeFixture;

// Test-only reflection reaches Ritsu's real importer/exporter because those
// integration boundaries are internal. Production code never calls them.
// Every save and run here is an in-memory fixture; no player save store is used.
internal static class PhilosophyRunStateRitsuRuntimeChecks
{
    private static readonly Assembly Mod = typeof(PhilosophyRunStateRitsuPayload).Assembly;
    private static readonly Type Store = Mod.GetType("STS2Philosophers.PhilosophyRunStateRitsuStore")!;
    private static readonly Type Service = Mod.GetType("STS2Philosophers.PhilosophyRunStateService")!;
    private static readonly Type Registry = typeof(RunSavedDataOptions).Assembly.GetType("STS2RitsuLib.RunData.RunSavedDataRegistry")!;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static Task Run()
    {
        Call(Store, "Initialize");
        string valid = ValidJson();
        foreach (var mutation in new Action<JsonObject>[]
        {
            slot => slot["schema"] = 2,
            slot => slot["schema"] = "future",
            slot => slot["data"]!["EncodedState"] = 17,
            slot => slot["data"]!["SchemaVersion"] = 2,
            slot => slot["data"]!["SchemaVersion"] = new JsonObject(),
            slot => slot["data"]!["EncodedState"] = "",
            slot => slot["data"]!.AsObject().Remove("EncodedState"),
            slot => slot["data"]!["EncodedState"] = "NOT_HEX",
            slot => slot["kind"] = "player",
        })
        {
            var json = JsonNode.Parse(valid)!.AsObject();
            var originalSlot = Slot(json)!;
            mutation(originalSlot);
            CheckIsolated(json.ToJsonString(), originalSlot);
        }

        MissingAndNewRunsStayAvailable();
        ValidStateContinuesSavingAfterResolution(valid);
        UnknownSourceAndBackupStayIsolated(valid);
        NativeRunResultRetainsExactSource(valid);
        NonObjectSlotsRefuseSaving();
        IsolatedLegacyMarkersRemainPreserved();
        SaveReadHookTargetsNativeStores();
        NativeLoaderPatchesObserveActualResult(valid);
        Console.WriteLine("Ritsu integration checks passed: real Import -> shared service -> real Export preserves invalid object slots, explicitly rejects unsafe native saves, keeps missing/new runs healthy, and saves later valid mutations; no player files or full game replay.");
        return Task.CompletedTask;
    }

    private static void CheckIsolated(string json, JsonObject originalSlot)
    {
        RunState run = Import(json);
        Check(!(bool)Call(Service, "IsAvailable", run)!, "A present unsupported/corrupt Ritsu slot must isolate philosophy features.");
        object?[] lookup = [run, null];
        Check(!(bool)Service.GetMethod("TryGet", Static)!.Invoke(null, lookup)! && lookup[1] is null,
            "TryGet must not expose a new writable state for an isolated run.");
        object placeholder = Call(Service, "GetOrCreate", run)!;
        Check(!(bool)Call(Store, "TryBind", run, placeholder)!, "Isolation must reject a direct attempt to replace the slot.");
        object emptyCandidates = Call(Service, "GetOrGenerateActOneCandidates", run)!;
        var candidates = (System.Collections.ICollection)emptyCandidates.GetType().GetProperty("CandidateIds")!.GetValue(emptyCandidates)!;
        Check(candidates.Count == 0, "Isolation must not generate a replacement invitation.");
        JsonObject exported = Export(run);
        Check(JsonNode.DeepEquals(originalSlot, Slot(exported)),
            "Real Ritsu export must preserve the unsupported original slot without normalization, deletion or replacement.");
        Check(!(bool)placeholder.GetType().GetProperty("HasData")!.GetValue(placeholder)!,
            "An isolated placeholder must not create gameplay facts.");
        CheckSaveGuard(run, allowsSave: true);
    }

    private static void MissingAndNewRunsStayAvailable()
    {
        RunState missing = Import("{}");
        Check((bool)Call(Service, "IsAvailable", missing)!, "A proven absent slot remains a supported legacy/new-feature run.");
        CheckSaveGuard(missing, allowsSave: true);
        Check(Slot(Export(missing)) is null, "Resolving an empty missing slot must not manufacture saved data.");
        object shared = Call(Service, "GetOrCreate", missing)!;
        Record(shared, "FIRST");
        RunSavePrefix(missing);
        Check(Slot(Export(missing)) is not null, "The first legitimate business mutation must create the Ritsu slot.");

        RunState fresh = NewRun();
        Check((bool)Call(Service, "IsAvailable", fresh)!, "A newly constructed run needs no loaded-save snapshot.");
        CheckSaveGuard(fresh, allowsSave: true);
        Check(Slot(Export(fresh)) is null, "An empty new run must not be misclassified or written.");
        Record(Call(Service, "GetOrCreate", fresh)!, "NEW");
        RunSavePrefix(fresh);
        Check(Slot(Export(fresh)) is not null, "The first new-run business state must persist through the real Ritsu exporter.");
    }

    private static void ValidStateContinuesSavingAfterResolution(string json)
    {
        RunState run = Import(json);
        Check((bool)Call(Service, "IsAvailable", run)!, "A valid imported payload must restore normally.");
        CheckSaveGuard(run, allowsSave: true);
        object shared = Call(Service, "GetOrCreate", run)!;
        Record(shared, "AFTER_RESOLVE");
        RunSavePrefix(run);
        string first = Slot(Export(run))!["data"]!["EncodedState"]!.GetValue<string>();
        Record(shared, "SECOND_SAVE");
        RunSavePrefix(run);
        string second = Slot(Export(run))!["data"]!["EncodedState"]!.GetValue<string>();
        Check(first != second, "WhenSet must preserve subsequent business changes after the service has already resolved the run.");
        RunState restored = Import(Export(run).ToJsonString());
        Check((bool)Call(Service, "IsAvailable", restored)!, "The second save must still decode and restore.");
        object restoredState = Call(Service, "GetOrCreate", restored)!;
        object doctrine = restoredState.GetType().GetProperty("CurrentDoctrine")!.GetValue(restoredState)!;
        Check((string)doctrine.GetType().GetProperty("DoctrineId")!.GetValue(doctrine)! == "SECOND_SAVE",
            "A real Ritsu import/export round trip must retain the most recent business mutation.");
    }

    private static void UnknownSourceAndBackupStayIsolated(string json)
    {
        RunState unknown = Import(json, observeSource: false);
        Check(!(bool)Call(Service, "IsAvailable", unknown)!, "An unproven loaded source cannot be treated as a missing slot.");
        Check(JsonNode.DeepEquals(Slot(JsonNode.Parse(json)!.AsObject()), Slot(Export(unknown))), "Unproven loaded sources must remain preserved.");
        CheckSaveGuard(unknown, allowsSave: false);
        var primary = JsonNode.Parse(ValidJson("PRIMARY"))!.AsObject();
        primary["_ritsulib"]!["run_saved_data"]!["OtherMod"] = new JsonObject
            { ["OTHER_SLOT"] = new JsonObject { ["opaque"] = "untouched" } };
        string primaryJson = primary.ToJsonString();
        RunState backup = Import(primaryJson, path: "audit/current_run.save.backup", nativeJson: ValidJson("BACKUP"));
        Check(!(bool)Call(Service, "IsAvailable", backup)!, "Native backup recovery cannot authorize Ritsu's later primary-file reread.");
        CheckSaveGuard(backup, allowsSave: false);
        Check(JsonNode.DeepEquals(primary, Export(backup)),
            "Refusing a backup/primary mismatch must not modify Ritsu's imported primary document or another mod's slot.");
        CheckSaveGuard(Import(primaryJson, nativeJson: ValidJson("DIFFERENT_NATIVE_SNAPSHOT")), allowsSave: false);
    }

    private static void NativeRunResultRetainsExactSource(string json)
    {
        Type captureType = Mod.GetType("STS2Philosophers.PhilosophyRunStateNativeLoadCapture")!;
        object capture = Call(captureType, "Begin")!;
        try
        {
            Call(captureType, "ObserveInput", "audit/current_run.save", json);
            string content = (string)captureType.GetProperty("Content", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(capture)!;
            string path = (string)captureType.GetProperty("SourcePath", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(capture)!;
            SerializableRun intermediate = new();
            Call(Store, "ObserveLoadedSave", intermediate, content, path);
            SerializableRun final = new();
            Call(Store, "ObserveNativeRunLoad", new ReadSaveResult<SerializableRun>(final), content, path);
            Call(Registry, "AttachDocumentFromJson", final, json);
            RunState run = NewRun();
            Call(Registry, "Import", final, run);
            Call(Store, "ObserveRestoredRun", final, run);
            Check((bool)Call(Service, "IsAvailable", run)!,
                "A final native DTO with a different identity must retain the exact load snapshot.");
            CheckSaveGuard(run, allowsSave: true);
        }
        finally { Call(captureType, "End", capture); }

        SerializableRun unavailable = new();
        Call(Registry, "AttachDocumentFromJson", unavailable, json);
        Call(Store, "ObserveNativeRunLoad", new ReadSaveResult<SerializableRun>(unavailable), null, null);
        RunState isolated = NewRun();
        Call(Registry, "Import", unavailable, isolated);
        Call(Store, "ObserveRestoredRun", unavailable, isolated);
        Check(!(bool)Call(Service, "IsAvailable", isolated)!,
            "Missing migration input must not be guessed from a later save read.");
        CheckSaveGuard(isolated, allowsSave: false);

        SerializableRun recovered = new();
        Call(Registry, "AttachDocumentFromJson", recovered, json);
        Call(Store, "ObserveNativeRunLoad", new ReadSaveResult<SerializableRun>(
            recovered, ReadSaveStatus.JsonRepaired, null), json, "audit/current_run.save");
        RunState recoveredRun = NewRun();
        Call(Registry, "Import", recovered, recoveredRun);
        Call(Store, "ObserveRestoredRun", recovered, recoveredRun);
        Check(!(bool)Call(Service, "IsAvailable", recoveredRun)!,
            "A repaired native payload cannot authorize Ritsu's original import as an exact match.");
        CheckSaveGuard(recoveredRun, allowsSave: false);
    }

    private static void NonObjectSlotsRefuseSaving()
    {
        foreach (JsonNode? malformed in new JsonNode?[] { null, new JsonArray(1, 2), JsonValue.Create("corrupt") })
        {
            var json = JsonNode.Parse(ValidJson())!.AsObject();
            json["_ritsulib"]!["run_saved_data"]!["STS2Philosophers"]!["PHILOSOPHY_RUN_STATE"] = malformed;
            json["_ritsulib"]!["run_saved_data"]!["OtherMod"] = new JsonObject
                { ["OTHER_SLOT"] = new JsonObject { ["opaque"] = "untouched" } };
            RunState run = Import(json.ToJsonString());
            CheckSaveGuard(run, allowsSave: false);
            Check(JsonNode.DeepEquals(json["_ritsulib"]!["run_saved_data"]!["OtherMod"],
                Export(run)["_ritsulib"]!["run_saved_data"]!["OtherMod"]),
                "Rejecting a slot the framework discards must not rewrite other mods' objects.");
        }
    }

    private static void CheckSaveGuard(RunState run, bool allowsSave)
    {
        RunManager manager = RunManager.Instance;
        RunState? original = manager.DebugOnlyGetState();
        try
        {
            Set(manager, "State", run);
            object?[] arguments = [Task.CompletedTask];
            bool proceed = (bool)Call(Mod.GetType("STS2Philosophers.PhilosophyRunStateUnsafeSaveGuardPatch")!, "Prefix", arguments)!;
            Check(proceed == allowsSave, "The actual SaveManager prefix must allow safe runs and skip unsafe native writes.");
            if (!proceed)
                Check(arguments[0] is Task { IsFaulted: true } failed && failed.Exception?.InnerException is IOException,
                    "Rejected saving must return an explicit failed Task, never a false successful save.");
        }
        finally { Set(manager, "State", original); }
    }

    private static void SaveReadHookTargetsNativeStores()
    {
        Type patch = Mod.GetType("STS2Philosophers.PhilosophyRunStateSaveReadPatch")!;
        var methods = ((IEnumerable<MethodBase>)Call(patch, "TargetMethods")!).ToArray();
        Check(methods.Select(method => method.DeclaringType?.Name).ToHashSet().IsSupersetOf(
            ["CloudSaveStore", "GodotFileIo", "SteamRemoteSaveStore"])
            && methods.All(method => method.Name == nameof(ISaveStore.ReadFile)),
            "The snapshot must observe native store ReadFile results, including cloud and local sources.");
        var harmony = new HarmonyLib.Harmony("STS2Philosophers.RitsuSnapshotChecks");
        RunState? original = RunManager.Instance.DebugOnlyGetState();
        Check(typeof(SaveManager).GetMethod(nameof(SaveManager.SaveRun), [typeof(AbstractRoom), typeof(bool)])?.ReturnType == typeof(Task),
            "The save guard must match the current native Task SaveRun(AbstractRoom, bool) signature.");
        try
        {
            harmony.CreateClassProcessor(patch).Patch();
            harmony.CreateClassProcessor(Mod.GetType("STS2Philosophers.PhilosophyRunStateUnsafeSaveGuardPatch")!).Patch();
            Set(RunManager.Instance, "State", Import(ValidJson(), observeSource: false));
            var saveManager = (SaveManager)RuntimeHelpers.GetUninitializedObject(typeof(SaveManager));
            Task rejected = saveManager.SaveRun(null!);
            Check(rejected.IsFaulted && rejected.Exception?.InnerException is IOException,
                "The patched native SaveRun must return the explicit refusal before touching any save store.");
        }
        finally { Set(RunManager.Instance, "State", original); harmony.UnpatchAll(harmony.Id); }
    }

    private static void NativeLoaderPatchesObserveActualResult(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        root["schema_version"] = 20;
        var store = new InMemoryRunSaveStore(root.ToJsonString());
        var migration = new MigrationManager(store);
        var manager = new RunSaveManager(2, store, migration, false);
        var harmony = new HarmonyLib.Harmony("STS2Philosophers.NativeLoadChecks");
        try
        {
            Type patch = Mod.GetType("STS2Philosophers.PhilosophyRunStateSaveReadPatch")!;
            harmony.CreateClassProcessor(patch).Patch();
            harmony.Patch(typeof(InMemoryRunSaveStore).GetMethod(nameof(ISaveStore.ReadFile))!,
                postfix: new HarmonyLib.HarmonyMethod(patch.GetMethod("Postfix", Static)!));
            harmony.CreateClassProcessor(Mod.GetType("STS2Philosophers.PhilosophyRunStateNativeRunLoadPatch")!).Patch();
            ReadSaveResult<SerializableRun> result = manager.LoadRunSave();
            Check(result.Success && result.SaveData is not null,
                $"The in-memory native run loader must return a DTO: {result.Status} {result.ErrorMessage}");
            SerializableRun save = result.SaveData!;
            Call(Registry, "AttachDocumentFromJson", save, root.ToJsonString());
            RunState run = NewRun();
            Call(Registry, "Import", save, run);
            Call(Store, "ObserveRestoredRun", save, run);
            bool available = (bool)Call(Service, "IsAvailable", run)!;
            object?[] unsafeReason = [run, null];
            Call(Store, "TryGetUnsafeSaveReason", unsafeReason);
            Check(available,
                $"The patched native loader must carry its exact JSON to the final DTO before run restoration. "
                + $"status={result.Status}, reason={unsafeReason[1]}");
            CheckSaveGuard(run, allowsSave: true);

            var backupStore = new InMemoryRunSaveStore(null, root.ToJsonString());
            var backupManager = new RunSaveManager(2, backupStore, new MigrationManager(backupStore), false);
            ReadSaveResult<SerializableRun> backupResult = backupManager.LoadRunSave();
            Check(backupResult.Success && backupResult.SaveData is not null,
                "The native backup fallback fixture must actually restore a DTO.");
            SerializableRun backupSave = backupResult.SaveData!;
            Call(Registry, "AttachDocumentFromJson", backupSave, root.ToJsonString());
            RunState backupRun = NewRun();
            Call(Registry, "Import", backupSave, backupRun);
            Call(Store, "ObserveRestoredRun", backupSave, backupRun);
            Check(!(bool)Call(Service, "IsAvailable", backupRun)!,
                "An exact backup read still cannot authorize Ritsu's later primary-file reread.");
            CheckSaveGuard(backupRun, allowsSave: false);
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static void IsolatedLegacyMarkersRemainPreserved()
    {
        SerializableRun save = new();
        ModelId original = new("STS2PhilosophersRunState", "V9_UNSUPPORTED");
        save.EventsSeen.Add(original);
        Call(Store, "ObserveLoadedSave", save, "{}", "audit/current_run.save");
        Type fromSave = Mod.GetType("STS2Philosophers.PhilosophyRunStateFromSavePatch")!;
        object?[] capture = [save, null];
        fromSave.GetMethod("Prefix", Static)!.Invoke(null, capture);
        RunState run = NewRun();
        Call(Registry, "Import", save, run);
        Call(fromSave, "Postfix", save, run, capture[1]);
        Check(!(bool)Call(Service, "IsAvailable", run)!, "An unsupported legacy marker must disable invitations instead of becoming a writable new route.");
        var manager = (RunManager)RuntimeHelpers.GetUninitializedObject(typeof(RunManager));
        Set(manager, "State", run);
        SerializableRun rewritten = new();
        object?[] output = [manager, rewritten];
        Mod.GetType("STS2Philosophers.PhilosophyRunStateToSavePatch")!.GetMethod("Postfix", Static)!.Invoke(null, output);
        Check(rewritten.EventsSeen.SequenceEqual([original]), "An otherwise native save must retain the exact isolated legacy marker.");
        Check(Slot(Export(run)) is null, "An isolated legacy marker must not create or overwrite a Ritsu slot.");
        CheckSaveGuard(run, allowsSave: true);
    }

    private static RunState Import(string json, bool observeSource = true, string path = "audit/current_run.save", string? nativeJson = null)
    {
        SerializableRun save = new();
        RunState run = NewRun();
        Call(Registry, "AttachDocumentFromJson", save, json);
        if (observeSource) Call(Store, "ObserveLoadedSave", save, nativeJson ?? json, path);
        Call(Registry, "Import", save, run);
        Call(Store, "ObserveRestoredRun", save, run);
        return run;
    }

    private static RunState NewRun()
    {
        var run = (RunState)RuntimeHelpers.GetUninitializedObject(typeof(RunState));
        Set(run, "_players", new List<Player>());
        return run;
    }

    private static JsonObject Export(RunState run)
    {
        object? document = Call(Registry, "Export", run);
        return document is null ? new JsonObject()
            : (JsonObject)document.GetType().GetMethod("ToRootObject", BindingFlags.Instance | BindingFlags.Public)!.Invoke(document, null)!;
    }

    private static void RunSavePrefix(RunState run)
    {
        var manager = (RunManager)RuntimeHelpers.GetUninitializedObject(typeof(RunManager));
        Set(manager, "State", run);
        Call(Mod.GetType("STS2Philosophers.PhilosophyRunStateToSavePatch")!, "Prefix", manager);
    }

    private static string ValidJson(string doctrine = "ORIGINAL")
    {
        object state = Activator.CreateInstance(Mod.GetType("STS2Philosophers.PhilosophyRunState")!, nonPublic: true)!;
        Record(state, doctrine);
        var payload = (PhilosophyRunStateRitsuPayload)Call(typeof(PhilosophyRunStateRitsuPayload), "Bind", state)!;
        return new JsonObject
        {
            ["_ritsulib"] = new JsonObject { ["version"] = 1, ["run_saved_data"] = new JsonObject
            {
                ["STS2Philosophers"] = new JsonObject { ["PHILOSOPHY_RUN_STATE"] = new JsonObject
                {
                    ["schema"] = 1, ["kind"] = "run", ["data"] = new JsonObject
                    { ["SchemaVersion"] = 1, ["EncodedState"] = payload.EncodedState },
                } },
            } },
        }.ToJsonString();
    }

    private static JsonObject? Slot(JsonObject root) =>
        root["_ritsulib"]?["run_saved_data"]?["STS2Philosophers"]?["PHILOSOPHY_RUN_STATE"] as JsonObject;

    private static void Record(object state, string doctrine) =>
        state.GetType().GetMethod("RecordCurrentDoctrine")!.Invoke(state, ["SOCRATES", doctrine, null]);

    private static object? Call(Type type, string method, params object?[] arguments) =>
        type.GetMethod(method, Static)!.Invoke(null, arguments);
}

internal sealed class InMemoryRunSaveStore(string? primary, string? backup = null) : ISaveStore
{
    public string ReadFile(string path) => (path.EndsWith(".backup", StringComparison.Ordinal)
        ? backup : primary) ?? throw new FileNotFoundException(path);
    public Task<string> ReadFileAsync(string path) => Task.FromResult(ReadFile(path));
    public bool FileExists(string path) => (path.EndsWith(".backup", StringComparison.Ordinal)
        ? backup : primary) is not null;
    public bool DirectoryExists(string path) => true;
    public string GetFullPath(string path) => path;
    public DateTimeOffset GetLastModifiedTime(string path) => DateTimeOffset.UtcNow;
    public int GetFileSize(string path) => ReadFile(path).Length;
    public string[] GetFilesInDirectory(string path) => [];
    public string[] GetDirectoriesInDirectory(string path) => [];
    public void WriteFile(string path, string content) => throw new InvalidOperationException("Read-only fixture");
    public void WriteFile(string path, byte[] content) => throw new InvalidOperationException("Read-only fixture");
    public Task WriteFileAsync(string path, string content) => throw new InvalidOperationException("Read-only fixture");
    public Task WriteFileAsync(string path, byte[] content) => throw new InvalidOperationException("Read-only fixture");
    public void DeleteFile(string path) => throw new InvalidOperationException("Read-only fixture");
    public void RenameFile(string source, string destination) => throw new InvalidOperationException("Read-only fixture");
    public void CreateDirectory(string path) => throw new InvalidOperationException("Read-only fixture");
    public void DeleteDirectory(string path) => throw new InvalidOperationException("Read-only fixture");
    public void DeleteTemporaryFiles(string path) => throw new InvalidOperationException("Read-only fixture");
    public void SetLastModifiedTime(string path, DateTimeOffset value) => throw new InvalidOperationException("Read-only fixture");
}
