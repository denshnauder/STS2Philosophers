using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Real native option draining and real native/Ritsu saves remain in this test.
// Only the private room/UI resume boundary is replaced with an exact-stack pop.
internal static class LegacyStoicQuestionExitNativeChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type T(string name) => typeof(DiogenesStaySwitch).Assembly.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) => T(type).GetMethods(Flags)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length).Invoke(null, args);
    private static object? On(object item, string method, params object?[] args) => item.GetType().GetMethods(Flags)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length).Invoke(item, args);
    private static object Get(object item, string name) => item.GetType().GetProperty(name, Flags)!.GetValue(item)!;
    private static void Put(object item, string name, object? value) => item.GetType().GetProperty(name, Flags)!.SetValue(item, value);
    private static object? Stoic(string method, params object?[] args) => typeof(StoicQuestionEntryNativeChecks)
        .GetMethod(method, Flags)!.Invoke(null, args);
    private static string Encode(object shared) => (string)Call("PhilosophyRunStateCodec", "Encode", shared)!;
    private static object Catalog => T("ZenoRouteValidationCatalogs").GetProperty("SaveRestore", Flags)!.GetValue(null)!;

    private static RunState? _expectedRun;
    private static MapRoom? _expectedBase;
    private static EventModel? _expectedOwner;
    private static Task? _expectedOption;
    private static NativeEventMemorySaveFixture? _expectedSaves;
    private static object? _expectedShared;
    private static int _resumes;

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
            ModelDb.Inject(typeof(DiogenesStaySwitch));
            await DiogenesLegacyExit(switchRoute: false);
            await DiogenesLegacyExit(switchRoute: true);
            Console.WriteLine("Legacy Stoic exit checks passed: actual Diogenes v1 two-room Stay/Switch closures persist and read back before returning to their own pending native option task; real Godot deferred callbacks await native EventSynchronizer draining, pop only the exact old base once, confirm its closed Ritsu/native checkpoint, then request Proceed once. Only ResumePreviousRoom/map opening are UI stubs; no Boss/act-exit/UI replay or player-save access.");
        }
        finally
        {
            _expectedRun = null; _expectedBase = null; _expectedOwner = null; _expectedOption = null;
            _expectedSaves = null; _expectedShared = null;
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave); Set(manager, "NetService", priorNetwork);
            Set(manager, "EventSynchronizer", priorSynchronizer); Set(manager, "ActionExecutor", priorExecutor);
            Set(CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
        }
    }

    private static async Task DiogenesLegacyExit(bool switchRoute)
    {
        var tuple = (ITuple)Stoic("Fixture")!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        object shared = tuple[3]!;
        MapRoom originalBase = new(); Set(originalBase, "Id", 17);
        object context = Activator.CreateInstance(T("DiogenesStaySwitchEntryContext"), [2, 1, true, false, 17])!;
        object?[] create = [shared, run.Rng.Seed, context, null];
        Check((bool)T("DiogenesStaySwitchEntryPolicy").GetMethod("TryCreate")!.Invoke(null, create)!,
            "The original production factory creates a real version-one act-map entry.");
        object entry = create[3]!;
        Check((int)Get(entry, "Version") == 1 && (int)Get(entry, "SourceRoomId") == originalBase.Id
            && entry.GetType().GetProperty("NativeNode")!.GetValue(entry) is null,
            "The fixture retains the old source identity instead of relabelling it as a native question.");
        Put(shared, "DiogenesStaySwitchEntry", entry);
        On(shared, "SetCurrentZenoRouteState", Call("DiogenesStaySwitchEntryPolicy", "CreateInitialState", entry), Catalog);
        EventRoom room = new(ModelDb.Event<DiogenesStaySwitch>());
        Set(run, "_currentRooms", new List<AbstractRoom> { originalBase, room });
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        object session = Call("DiogenesStaySwitchEntrySession", "Get", RunManager.Instance)!;
        Check(await (Task<bool>)On(session, "PrepareEventRoomAsync", room)!,
            "The actual legacy EventRoom confirms its exact native and Ritsu checkpoint before options exist.");
        EventModel local = (EventModel)Stoic("Configure", run, fixture, room)!;
        object runtime = Stoic("Runtime", run)!;
        object state = Get(runtime, "CurrentState"), route = Get(state, "Route");
        object prepared = switchRoute
            ? Call("ZenoRouteStateService", "PrepareSwitch", state, Get(route, "Revision"), Get(entry, "Material"), Catalog)!
            : Call("ZenoRouteStateService", "PrepareStay", state, Get(route, "Revision"), Catalog)!;
        await (Task)Stoic("Execute", runtime, prepared)!;
        Check(Get(Get(Get(runtime, "CurrentState"), "Route"), "Stage").ToString()
                == (switchRoute ? "WaitingInterval" : "DiogenesClosed"),
            "The native writer commits the original Stay or Switch transaction before closing.");

        var startOption = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOption = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeReturned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task OwnOption()
        {
            try
            {
                await startOption.Task;
                bool accepted = await (Task<bool>)On(session, "CloseAsync")!;
                closeReturned.TrySetResult(accepted);
                await releaseOption.Task;
            }
            catch (Exception error) { closeReturned.TrySetException(error); throw; }
        }
        Task option = OwnOption();
        Set(RunManager.Instance.EventSynchronizer, "_pendingOptionTasks", new List<Task> { option });
        _expectedRun = run; _expectedBase = originalBase; _expectedOwner = local; _expectedOption = option;
        _expectedSaves = saves; _expectedShared = shared; _resumes = 0;
        using var resumePatch = new ResumePatch();
        try
        {
            int initialProceed = saves.ProceedAttempts, initialWrites = saves.SaveAttempts;
            startOption.SetResult();
            Check(await closeReturned.Task.WaitAsync(TimeSpan.FromSeconds(10)),
                "Closing returns true without awaiting the same still-pending native option task.");
            string closed = Encode(shared);
            int closedWrites = saves.SaveAttempts;
            Check(!option.IsCompleted && closedWrites > initialWrites
                && (bool)Get(Get(shared, "DiogenesStaySwitchEntry"), "Closed") && room.IsPreFinished
                && run.CurrentRoomCount == 2 && ReferenceEquals(run.BaseRoom, originalBase)
                && ReferenceEquals(run.CurrentRoom, room) && ReferenceEquals(room.LocalMutableEvent, local)
                && (bool)Get(session, "HasQueuedLegacyExit") && _resumes == 0 && saves.ProceedAttempts == initialProceed,
                "The exact old event is durably closed while its own option remains on the native synchronizer.");
            Stoic("CheckStored", saves, typeof(DiogenesStaySwitch), true, shared);

            await Frame(); await Frame();
            Check(!option.IsCompleted && _resumes == 0 && saves.ProceedAttempts == initialProceed
                && saves.SaveAttempts == closedWrites && run.CurrentRoomCount == 2
                && ReferenceEquals(run.CurrentRoom, room) && Encode(shared) == closed,
                "The real deferred callback drains pending native options before any pop, additional save or Proceed.");
            Check(await (Task<bool>)On(session, "CloseAsync")! && saves.SaveAttempts == closedWrites
                && _resumes == 0 && saves.ProceedAttempts == initialProceed,
                "A repeated close while its deferred exit is waiting cannot duplicate the save or queue another exit.");

            releaseOption.SetResult();
            await option.WaitAsync(TimeSpan.FromSeconds(10));
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (saves.ProceedAttempts == initialProceed && DateTime.UtcNow < deadline) await Frame();
            Check(_resumes == 1 && saves.ProceedAttempts == initialProceed + 1
                && saves.SaveAttempts == closedWrites + 1 && run.CurrentRoomCount == 1
                && ReferenceEquals(run.CurrentRoom, originalBase) && ReferenceEquals(run.BaseRoom, originalBase)
                && !(bool)Get(session, "HasQueuedLegacyExit") && Encode(shared) == closed,
                "After its own option completes, one exit restores the original base and confirms one closed checkpoint before Proceed.");
            var loaded = saves.Manager.LoadRunSave();
            Check(loaded.Success && loaded.SaveData!.PreFinishedRoom is null,
                "The real native loader confirms that the resumed base checkpoint no longer carries the old event overlay.");
            CheckRitsuMatches(saves, shared);
            await Frame(); await Frame();
            Check(_resumes == 1 && saves.ProceedAttempts == initialProceed + 1
                && saves.SaveAttempts == closedWrites + 1
                && !await (Task<bool>)On(session, "CloseAsync")!,
                "Later callbacks cannot pop the old stack or reopen a completed legacy exit.");
        }
        finally
        {
            startOption.TrySetResult(); releaseOption.TrySetResult();
            try { await option.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            // Drain an already queued test callback while the memory writer and
            // exact-stack stub still exist, including on an assertion failure.
            await Frame(); await Frame();
        }
    }

    private static async Task Frame()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static void CheckRitsuMatches(NativeEventMemorySaveFixture saves, object shared)
    {
        object read = Call("PhilosophyRunStateRitsuSaveReader", "Read", saves.Json)!;
        Check(Get(read, "Classification").ToString() == "Current" && (string)Get(read, "EncodedState") == Encode(shared),
            "The real written Ritsu state preserves the original closed entry, frozen material and route transaction.");
    }

    private static bool ResumeExactBase(RunManager __instance, ref Task __result)
    {
        Check(ReferenceEquals(__instance, RunManager.Instance) && _expectedRun is { } run
            && ReferenceEquals(__instance.DebugOnlyGetState(), run) && run.CurrentRoomCount == 2
            && ReferenceEquals(run.BaseRoom, _expectedBase) && run.CurrentRoom is EventRoom room
            && ReferenceEquals(room.LocalMutableEvent, _expectedOwner) && room.IsPreFinished
            && _expectedOption?.IsCompletedSuccessfully == true && _resumes == 0,
            "The UI-only resume boundary sees the exact old base and event owner after the real pending option has drained.");
        Stoic("CheckStored", _expectedSaves!, typeof(DiogenesStaySwitch), true, _expectedShared!);
        _resumes++;
        Set(_expectedRun!, "_currentRooms", new List<AbstractRoom> { _expectedBase! });
        __result = Task.CompletedTask;
        return false;
    }

    private sealed class ResumePatch : IDisposable
    {
        private readonly Harmony _harmony = new("STS2Philosophers.ENC199.LegacyStoicExactResume");
        internal ResumePatch()
        {
            MethodInfo target = AccessTools.DeclaredMethod(typeof(RunManager), "ResumePreviousRoom", Type.EmptyTypes)
                ?? throw new MissingMethodException("RunManager.ResumePreviousRoom()");
            _harmony.Patch(target, prefix: new HarmonyMethod(typeof(LegacyStoicQuestionExitNativeChecks)
                .GetMethod(nameof(ResumeExactBase), Flags)!));
        }
        public void Dispose() => _harmony.UnpatchAll(_harmony.Id);
    }
}
