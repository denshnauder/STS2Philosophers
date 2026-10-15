using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

[HarmonyPatch]
internal static class HanFeiPublicStandardPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(
            typeof(RunManager),
            "EnterRoomInternal",
            [typeof(AbstractRoom), typeof(bool)])
        ?? throw new MissingMethodException(
            typeof(RunManager).FullName,
            "EnterRoomInternal(AbstractRoom, bool)");

    private static void Postfix(
        AbstractRoom room,
        bool isRestoringRoomStackBase,
        ref Task __result)
    {
        if (room is MapRoom)
        {
            __result = InsertAfterMapEntry(__result, isRestoringRoomStackBase);
        }
    }

    private static async Task InsertAfterMapEntry(
        Task mapEntryTask,
        bool isRestoringRoomStackBase)
    {
        await mapEntryTask;
        if (isRestoringRoomStackBase)
        {
            return;
        }

        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null)
        {
            return;
        }

        PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
        HanFeiPublicStandard? canonical = TryGetCanonicalEvent();
        HanFeiPublicStandardState? saved = philosophy.HanFeiPublicStandard;
        ActBehaviorState? behavior =
            philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        HanFeiPublicStandardTriggerContext context = new(
            manager.IsInProgress,
            run.CurrentRoom is MapRoom,
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            LaterActRelicEligibilityRuntime.IsEligible(
                player,
                LaterActCandidateIds.HanFei,
                run.CurrentActIndex),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!HanFeiPublicStandardTriggerPolicy.ShouldInsert(context))
        {
            return;
        }

        if (saved is null)
        {
            philosophy.HanFeiPublicStandard = HanFeiPublicStandardState.Create(
                $"HAN_FEI_PUBLIC_STANDARD:{run.Rng.Seed}:3");
            if (manager.ShouldSave)
            {
                await SaveManager.Instance.SaveRun(null);
            }
        }

        Log.Info("[STS2Philosophers] Presenting Han Fei's act-three public standard.");
        NMapScreen.Instance?.Close(animateOut: false);
        await manager.EnterRoomWithoutExitingCurrentRoom(
            new EventRoom(canonical!),
            fadeToBlack: false);
    }

    private static HanFeiPublicStandard? TryGetCanonicalEvent()
    {
        try
        {
            return ModelDb.Event<HanFeiPublicStandard>();
        }
        catch (Exception exception)
        {
            Log.Error(
                $"[STS2Philosophers] HanFeiPublicStandard is unavailable: {exception}");
            return null;
        }
    }
}
