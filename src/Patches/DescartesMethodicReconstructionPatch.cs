using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class DescartesMethodicReconstructionPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null) return;

        if (!RouteEventMapBoundary.IsSafe(run)) return;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return;
        WesternJourneyState? journey = philosophy.WesternJourney;
        DescartesMethodicReconstruction? canonical = TryGetCanonicalEvent();
        DescartesMethodicReconstructionState? saved = philosophy.DescartesMethodicReconstruction;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        var currentMapPoint = run.CurrentMapCoord is not null ? run.CurrentMapPoint : null;
        DescartesMethodicReconstructionTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.DeclinedFixedActs.Contains(2) == true,
            LaterActRelicEligibilityRuntime.IsEligible(
                player,
                LaterActCandidateIds.Descartes,
                run.CurrentActIndex),
            philosophy.SextusOpposedAppearances?.Closed == true,
            currentMapPoint?.Children.Count >= 2,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
        {
            if (journey is null || !WesternRouteGraph.LoadEmbedded().TryDecline(journey, "Fixed", 2)) return;
            philosophy.DescartesMethodicReconstruction = DescartesMethodicReconstructionState.Create(
                $"DESCARTES_METHODIC_RECONSTRUCTION:{run.Rng.Seed}:2");
        }

        Log.Info("[STS2Philosophers] Presenting Descartes's methodic reconstruction at a natural map fork.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static DescartesMethodicReconstruction? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<DescartesMethodicReconstruction>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] DescartesMethodicReconstruction is unavailable: {exception}");
            return null;
        }
    }
}
