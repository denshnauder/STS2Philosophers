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
internal static class SextusOpposedAppearancesPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(typeof(RunManager), "EnterRoomInternal", [typeof(AbstractRoom), typeof(bool)])
        ?? throw new MissingMethodException(typeof(RunManager).FullName, "EnterRoomInternal(AbstractRoom, bool)");

    private static void Postfix(AbstractRoom room, bool isRestoringRoomStackBase, ref Task __result)
    {
        if (room is MapRoom) __result = InsertAfterMapEntry(__result, isRestoringRoomStackBase);
    }

    private static async Task InsertAfterMapEntry(Task mapEntryTask, bool isRestoringRoomStackBase)
    {
        await mapEntryTask;
        if (isRestoringRoomStackBase) return;

        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null) return;

        PhilosophyRunState philosophy = PhilosophyRunStateService.GetOrCreate(run);
        WesternJourneyState? journey = philosophy.WesternJourney;
        SextusOpposedAppearances? canonical = TryGetCanonicalEvent();
        SextusOpposedAppearancesState? saved = philosophy.SextusOpposedAppearances;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        var currentMapPoint = run.CurrentMapCoord is not null ? run.CurrentMapPoint : null;
        bool hasNaturalFork = currentMapPoint?.Children.Count >= 2;
        SextusOpposedAppearancesTriggerContext context = new(
            manager.IsInProgress,
            run.CurrentRoom is MapRoom,
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            player?.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is not null,
            hasNaturalFork,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!SextusOpposedAppearancesTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
        {
            if (journey is null || !WesternRouteGraph.LoadEmbedded().TryReserveQuestion(journey, 1)) return;
            philosophy.SextusOpposedAppearances = SextusOpposedAppearancesState.Create(
                $"SEXTUS_OPPOSED_APPEARANCES:{run.Rng.Seed}:1");
            if (manager.ShouldSave) await SaveManager.Instance.SaveRun(null);
        }

        Log.Info("[STS2Philosophers] Presenting Sextus's opposed appearances at a natural map fork.");
        NMapScreen.Instance?.Close(animateOut: false);
        await manager.EnterRoomWithoutExitingCurrentRoom(new EventRoom(canonical!), fadeToBlack: false);
    }

    private static SextusOpposedAppearances? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<SextusOpposedAppearances>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] SextusOpposedAppearances is unavailable: {exception}");
            return null;
        }
    }
}
