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
internal static class PlatoWholeArrangementPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(typeof(RunManager), "EnterRoomInternal", [typeof(AbstractRoom), typeof(bool)])
        ?? throw new MissingMethodException(typeof(RunManager).FullName, "EnterRoomInternal(AbstractRoom, bool)");

    private static void Postfix(AbstractRoom room, bool isRestoringRoomStackBase, ref Task __result)
    {
        if (room is MapRoom)
        {
            __result = InsertAfterMapEntry(__result, isRestoringRoomStackBase);
        }
    }

    private static async Task InsertAfterMapEntry(Task mapEntryTask, bool isRestoringRoomStackBase)
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
        WesternJourneyState? journey = philosophy.WesternJourney;
        PlatoWholeArrangement? canonical = TryGetCanonicalEvent();
        PlatoWholeArrangementState? saved = philosophy.PlatoWholeArrangement;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        PlatoWholeArrangementTriggerContext context = new(
            manager.IsInProgress,
            run.CurrentRoom is MapRoom,
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains("WESTERN_EDGE_008") == true,
            journey?.DeclinedFixedActs.Contains(2) == true,
            journey?.CompletedEdgeIds.Contains(PlatoWholeArrangementState.CompletionEdgeId) == true,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!PlatoWholeArrangementTriggerPolicy.ShouldInsert(context))
        {
            return;
        }

        if (saved is null)
        {
            philosophy.PlatoWholeArrangement = PlatoWholeArrangementState.Create(
                $"PLATO_WHOLE_ARRANGEMENT:{run.Rng.Seed}:2");
            if (manager.ShouldSave)
            {
                await SaveManager.Instance.SaveRun(null);
            }
        }

        Log.Info("[STS2Philosophers] Presenting Plato's act-two whole arrangement.");
        NMapScreen.Instance?.Close(animateOut: false);
        await manager.EnterRoomWithoutExitingCurrentRoom(new EventRoom(canonical!), fadeToBlack: false);
    }

    private static PlatoWholeArrangement? TryGetCanonicalEvent()
    {
        try
        {
            return ModelDb.Event<PlatoWholeArrangement>();
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] PlatoWholeArrangement is unavailable: {exception}");
            return null;
        }
    }
}
