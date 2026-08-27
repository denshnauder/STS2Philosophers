using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
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
        PhilosophersGazeRelicOwnership ownership = GetOwnership(player);
        bool continuationRecorded = HasContinuationBeenRecorded(player);
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

        Log.Info("[STS2MinimalMod] Inserting the act two PhilosophersGaze continuation before map travel is enabled.");
        await runManager.EnterRoomWithoutExitingCurrentRoom(
            new EventRoom(canonicalEvent!),
            fadeToBlack: false);
    }

    private static PhilosophersGazeRelicOwnership GetOwnership(Player player)
    {
        return new PhilosophersGazeRelicOwnership(
            player.GetRelicById(ModelDb.GetId<KongziMuduo>()) is not null,
            player.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) is not null,
            player.GetRelicById(ModelDb.GetId<MengziXiongZhang>()) is not null,
            player.GetRelicById(ModelDb.GetId<XunziShengMo>()) is not null);
    }

    private static bool HasContinuationBeenRecorded(Player player)
    {
        return (player.GetRelicById(ModelDb.GetId<KongziMuduo>()) as KongziMuduo)
                ?.HasResolvedPhilosophersGazeContinuation == true
            || (player.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei)
                ?.HasResolvedPhilosophersGazeContinuation == true;
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
