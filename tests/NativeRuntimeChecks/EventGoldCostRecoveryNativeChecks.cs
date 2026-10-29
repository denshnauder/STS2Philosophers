using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using STS2Philosophers;
using static NativeRuntimeFixture;

// Real SaveManager/RunSaveManager writes through the private Ritsu memory store,
// a real native EventRoom and real PlayerCmd.LoseGold. A "restore" reads the run
// JSON back through the native loader and the Ritsu save reader, then decodes a
// fresh PhilosophyRunState instead of reusing the live object.
internal static class EventGoldCostRecoveryNativeChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private const decimal Cost = 12m;
    private const decimal NoSnapshot = -1m;
    private static Type T(string name) => typeof(PopperFalsification).Assembly.GetType("STS2Philosophers." + name)!;
    private static object? Call(string type, string method, params object?[] args) => T(type).GetMethods(Flags)
        .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length).Invoke(null, args);
    private static object Get(object item, string name) => item.GetType().GetProperty(name, Flags)!.GetValue(item)!;
    private static void Put(object item, string name, object? value) => item.GetType().GetProperty(name, Flags)!.SetValue(item, value);
    private static string Encode(object shared) => (string)Call("PhilosophyRunStateCodec", "Encode", shared)!;

    public static async Task Run()
    {
        RunManager manager = RunManager.Instance;
        RunState? prior = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        bool priorSave = manager.ShouldSave, priorTest = TestMode.IsOn;
        object? priorNetwork = manager.NetService;
        object priorSynchronizer = manager.EventSynchronizer, priorExecutor = manager.ActionExecutor;
        object? priorTurn = typeof(MegaCrit.Sts2.Core.Combat.CombatManager).GetField("_turnState", Flags)!
            .GetValue(MegaCrit.Sts2.Core.Combat.CombatManager.Instance);
        try
        {
            TestMode.IsOn = true;
            await AttemptOnlyChargeIsNotInTheSave();
            await ExternalSaveAfterChargeLeavesAttemptOnly();
            await LegacyAttemptIsNotChargedAgain();
            await SnapshotRecoveryChargesOnceWhenChargeWasNotSaved();
            await AttemptPersistFailureTakesNoGold();
            await PaidPersistFailureRecoversWithoutDoubleCharge();
            Console.WriteLine("Gold-cost recovery checks passed: real SaveManager/RunSaveManager memory-store writes, real PlayerCmd.LoseGold and a fresh Ritsu readback show that an attempt-only save never contains the later charge, while an unrelated save between the charge and the lost paid checkpoint does persist the reduced gold with CostPaid false; the protocol therefore settles such ambiguous legacy records without charging again, charges exactly once when a gold snapshot proves the charge was not saved, takes no gold when the attempt checkpoint fails, and recovers a failed paid checkpoint with exactly one persisted charge.");
        }
        finally
        {
            Set(manager, "State", prior); Set(manager, "ShouldSave", priorSave); Set(manager, "NetService", priorNetwork);
            Set(manager, "EventSynchronizer", priorSynchronizer); Set(manager, "ActionExecutor", priorExecutor);
            Set(MegaCrit.Sts2.Core.Combat.CombatManager.Instance, "_turnState", priorTurn); TestMode.IsOn = priorTest;
        }
    }

    private static (NativeRuntimeFixture Fixture, RunState Run, object Shared, EventRoom Room) Setup()
    {
        var tuple = (ITuple)typeof(NativeQuestionNodeChecks).GetMethod("Fixture", Flags)!
            .Invoke(null, [typeof(SocratesBalanceWeight), 2])!;
        var fixture = (NativeRuntimeFixture)tuple[0]!;
        var run = (RunState)tuple[1]!;
        object shared = typeof(NativeQuestionNodeChecks).GetMethod("Shared", Flags)!.Invoke(null,
            ["None", PopperNode, fixture.Relic.Id.Entry])!;
        Call("PhilosophyRunStateService", "Restore", run, shared);
        ModelDb.Inject(typeof(PopperFalsification));
        EventRoom room = new(ModelDb.Event<PopperFalsification>());
        Set(run, "_currentRooms", new List<AbstractRoom> { room });
        fixture.Player.Gold = 20;
        return (fixture, run, shared, room);
    }

    private const string PopperNode = "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE";

    private static object PopperState(bool attempted, decimal before, bool paid = false)
    {
        object state = Activator.CreateInstance(T("PopperFalsificationState"), nonPublic: true)!;
        Type outcome = T("PopperFalsificationOutcome"), action = T("PopperFalsificationAction");
        Put(state, "InstanceId", "ENC_F4_RECOVERY");
        Put(state, "Stage", Enum.Parse(T("PopperFalsificationStage"), "ResultCalibrated"));
        Put(state, "Outcome", Enum.Parse(outcome, "Calibrated"));
        Put(state, "FalsifiableTest", true);
        Put(state, "InstrumentRiskKnown", true);
        Put(state, "CalibrationPaid", true);
        Put(state, "CostAttempted", attempted);
        Put(state, "CostPaid", paid);
        Put(state, "CostGoldBefore", before);
        Put(state, "CostGoldAfter", NoSnapshot);
        var actions = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(action))!;
        actions.Add(Enum.Parse(action, "SelectBoundaryTest"));
        actions.Add(Enum.Parse(action, "CalibrateAndReport"));
        Put(state, "ConfirmedActions", actions);
        return state;
    }

    private static Task<bool> Persist(RunState run, object shared, EventRoom room) =>
        (Task<bool>)Call("RouteEventMapBoundary", "PersistEventAsync", run, shared, room)!;

    private static int SavedGold(NativeEventMemorySaveFixture saves)
    {
        var loaded = saves.Manager.LoadRunSave();
        Check(loaded.Status == MegaCrit.Sts2.Core.Saves.ReadSaveStatus.Success && loaded.SaveData is not null,
            "The native loader reads the saved run back.");
        return loaded.SaveData!.Players[0].Gold;
    }

    private static object Restore(NativeEventMemorySaveFixture saves, RunState run, NativeRuntimeFixture fixture)
    {
        object read = Call("PhilosophyRunStateRitsuSaveReader", "Read", saves.Json)!;
        Check(Get(read, "Classification").ToString() == "Current",
            "The saved Ritsu payload classifies as the current format.");
        object restored = Call("PhilosophyRunStateCodec", "Decode", Get(read, "EncodedState"))!;
        Call("PhilosophyRunStateService", "Restore", run, restored);
        fixture.Player.Gold = SavedGold(saves);
        return restored;
    }

    private static async Task<string> Apply(
        object state, Func<decimal?> gold, Func<Task> charge, Func<Task<bool>> persist)
    {
        MethodInfo apply = T("EventGoldCostProtocol").GetMethod("ApplyAsync", Flags)!;
        object taskObject = apply.Invoke(null, [state, gold, charge, persist])!;
        await (Task)taskObject;
        return taskObject.GetType().GetProperty("Result")!.GetValue(taskObject)!.ToString()!;
    }

    private static async Task AttemptOnlyChargeIsNotInTheSave()
    {
        var (fixture, run, shared, room) = Setup();
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        Put(shared, "PopperFalsification", PopperState(attempted: true, NoSnapshot));
        Check(await Persist(run, shared, room), "The legacy attempt-only checkpoint writes a real run save.");
        Check(SavedGold(saves) == 20, "The attempt checkpoint saves the pre-charge gold.");
        await PlayerCmd.LoseGold(Cost, fixture.Player, GoldLossType.Spent);
        Check(fixture.Player.Gold == 8, "The native command actually takes the cost in memory.");
        Check(SavedGold(saves) == 20,
            "A charge that is not followed by a save never appears in the saved run.");
    }

    private static async Task ExternalSaveAfterChargeLeavesAttemptOnly()
    {
        var (fixture, run, shared, room) = Setup();
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        Put(shared, "PopperFalsification", PopperState(attempted: true, NoSnapshot));
        Check(await Persist(run, shared, room), "The legacy attempt-only checkpoint writes a real run save.");
        await PlayerCmd.LoseGold(Cost, fixture.Player, GoldLossType.Spent);
        // Any unrelated save between the charge and the lost paid checkpoint
        // persists the reduced gold while the event still says "attempted only".
        await MegaCrit.Sts2.Core.Saves.SaveManager.Instance.SaveRun(room);
        Check(SavedGold(saves) == 8, "An unrelated save after the charge does persist the reduced gold.");
        object restored = Restore(saves, run, fixture);
        object popper = Get(restored, "PopperFalsification");
        Check((bool)Get(popper, "CostAttempted") && !(bool)Get(popper, "CostPaid")
            && (decimal)Get(popper, "CostGoldBefore") == NoSnapshot,
            "The restored legacy record is attempted-only with no snapshot, so it cannot prove whether it paid.");
    }

    private static async Task LegacyAttemptIsNotChargedAgain()
    {
        var (fixture, run, shared, room) = Setup();
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        Put(shared, "PopperFalsification", PopperState(attempted: true, NoSnapshot));
        Check(await Persist(run, shared, room), "The legacy attempt-only checkpoint writes a real run save.");
        await PlayerCmd.LoseGold(Cost, fixture.Player, GoldLossType.Spent);
        await MegaCrit.Sts2.Core.Saves.SaveManager.Instance.SaveRun(room);
        object restored = Restore(saves, run, fixture);
        object popper = Get(restored, "PopperFalsification");
        int charges = 0;
        string outcome = await Apply(popper,
            () => fixture.Player.Gold,
            () => { charges++; return PlayerCmd.LoseGold(Cost, fixture.Player, GoldLossType.Spent); },
            () => Persist(run, restored, room));
        Check(outcome == "Settled" && charges == 0,
            "An ambiguous legacy record settles instead of taking the cost a second time.");
        Check(fixture.Player.Gold == 8 && SavedGold(saves) == 8,
            "The settled legacy record leaves the already reduced gold untouched.");
        Check((bool)Get(popper, "CostPaid"), "The settled legacy record is marked paid in the save.");
    }

    private static async Task SnapshotRecoveryChargesOnceWhenChargeWasNotSaved()
    {
        var (fixture, run, shared, room) = Setup();
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        Put(shared, "PopperFalsification", PopperState(attempted: true, 20m));
        Check(await Persist(run, shared, room), "The snapshot checkpoint writes a real run save.");
        await PlayerCmd.LoseGold(Cost, fixture.Player, GoldLossType.Spent);
        Check(SavedGold(saves) == 20, "The unsaved charge is absent from the snapshot save.");
        object restored = Restore(saves, run, fixture);
        object popper = Get(restored, "PopperFalsification");
        int charges = 0;
        string outcome = await Apply(popper,
            () => fixture.Player.Gold,
            () => { charges++; return PlayerCmd.LoseGold(Cost, fixture.Player, GoldLossType.Spent); },
            () => Persist(run, restored, room));
        Check(outcome == "Settled" && charges == 1 && fixture.Player.Gold == 8,
            "A snapshot that still matches the saved gold charges exactly once.");
        Check(SavedGold(saves) == 8 && (bool)Get(popper, "CostPaid"),
            "The single recovered charge and its paid flag reach the save.");
    }

    private static async Task AttemptPersistFailureTakesNoGold()
    {
        var (fixture, run, shared, room) = Setup();
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        object popper = PopperState(attempted: false, NoSnapshot);
        Put(shared, "PopperFalsification", popper);
        saves.FailWrites = true;
        int charges = 0;
        string outcome = await Apply(popper,
            () => fixture.Player.Gold,
            () => { charges++; return PlayerCmd.LoseGold(Cost, fixture.Player, GoldLossType.Spent); },
            () => Persist(run, shared, room));
        saves.FailWrites = false;
        Check(outcome == "PersistenceUnknown" && charges == 0 && fixture.Player.Gold == 20,
            "A failed attempt checkpoint takes no gold and reports an unknown persistence.");
        var loaded = saves.Manager.LoadRunSave();
        Check(loaded.SaveData is null || loaded.SaveData.Players[0].Gold == 20,
            "A failed attempt checkpoint never persists a reduced gold.");
    }

    private static async Task PaidPersistFailureRecoversWithoutDoubleCharge()
    {
        var (fixture, run, shared, room) = Setup();
        using var saves = new NativeEventMemorySaveFixture(fixture, run);
        object popper = PopperState(attempted: true, 20m);
        Put(shared, "PopperFalsification", popper);
        Check(await Persist(run, shared, room), "The attempt checkpoint with its gold snapshot is written before the charge.");
        saves.FailWrites = true;
        int charges = 0;
        string outcome = await Apply(popper,
            () => fixture.Player.Gold,
            () => { charges++; return PlayerCmd.LoseGold(Cost, fixture.Player, GoldLossType.Spent); },
            () => Persist(run, shared, room));
        Check(outcome == "PersistenceUnknown" && charges == 1 && fixture.Player.Gold == 8,
            "The charge happens once even when the paid checkpoint fails.");
        saves.FailWrites = false;
        Check(SavedGold(saves) == 20, "The failed paid checkpoint leaves the pre-charge gold in the save.");
        object restored = Restore(saves, run, fixture);
        object recovered = Get(restored, "PopperFalsification");
        Check((bool)Get(recovered, "CostAttempted") && !(bool)Get(recovered, "CostPaid")
            && (decimal)Get(recovered, "CostGoldBefore") == 20m,
            "The restored snapshot still proves the unsaved charge.");
        int recoveredCharges = 0;
        string retry = await Apply(recovered,
            () => fixture.Player.Gold,
            () => { recoveredCharges++; return PlayerCmd.LoseGold(Cost, fixture.Player, GoldLossType.Spent); },
            () => Persist(run, restored, room));
        Check(retry == "Settled" && recoveredCharges == 1 && fixture.Player.Gold == 8,
            "Recovery after a failed paid checkpoint charges exactly once in total.");
        Check(SavedGold(saves) == 8 && (bool)Get(recovered, "CostPaid"),
            "The recovered charge is saved once with its paid flag.");
    }
}
