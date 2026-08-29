using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;

namespace STS2MinimalMod;

[HarmonyPatch]
internal static class ActTwoPhilosophersGazePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.DeclaredMethod(
                typeof(RunManager),
                "EnterRoomInternal",
                [typeof(AbstractRoom), typeof(bool)])
            ?? throw new MissingMethodException(
                typeof(RunManager).FullName,
                "EnterRoomInternal(AbstractRoom, bool)");
    }

    private static void Postfix(
        AbstractRoom room,
        bool isRestoringRoomStackBase,
        ref Task __result)
    {
        if (room is not MapRoom)
        {
            return;
        }

        __result = InsertContinuationAfterMapRoomEntry(
            __result,
            isRestoringRoomStackBase);
    }

    private static async Task InsertContinuationAfterMapRoomEntry(
        Task mapRoomEntryTask,
        bool isRestoringRoomStackBase)
    {
        await mapRoomEntryTask;

        RunManager runManager = RunManager.Instance;
        RunState? runState = runManager.IsInProgress
            ? runManager.DebugOnlyGetState()
            : null;
        if (isRestoringRoomStackBase
            || runState is null
            || runState.CurrentActIndex != 1
            || runState.Players.Count != 1)
        {
            return;
        }

        Player player = runState.Players[0];
        PhilosophersGazeRelicOwnership ownership = PhilosophersGaze.GetOwnership(player);
        bool continuationRecorded = PhilosophersGaze.HasContinuationBeenRecorded(player);
        PhilosophersGaze? canonicalEvent = TryGetCanonicalEvent();
        PhilosophersGazeContinuationInsertionContext context = new(
            runManager.IsInProgress,
            runState.CurrentRoom is MapRoom,
            runState.CurrentActIndex,
            canonicalEvent is not null,
            runState.Players.Count == 1,
            ownership,
            continuationRecorded);
        if (!PhilosophersGazeContinuationPolicy.ShouldInsert(context))
        {
            return;
        }

        Log.Info("[STS2MinimalMod] Closing the act two map screen before presenting the PhilosophersGaze continuation.");
        PhilosophersGazeContinuationEntryPlan entryPlan =
            PhilosophersGazeContinuationPolicy.CreateEntryPlan(
                mapRoomEntryCompleted: true);
        if (entryPlan.CloseMapScreen)
        {
            NMapScreen.Instance?.Close(animateOut: false);
        }

        await runManager.EnterRoomWithoutExitingCurrentRoom(
            new EventRoom(canonicalEvent!),
            fadeToBlack: entryPlan.FadeToBlack);
    }

    private static PhilosophersGaze? TryGetCanonicalEvent()
    {
        try
        {
            return ModelDb.Event<PhilosophersGaze>();
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2MinimalMod] PhilosophersGaze is unavailable; skipping the act two continuation: {exception}");
            return null;
        }
    }
}
