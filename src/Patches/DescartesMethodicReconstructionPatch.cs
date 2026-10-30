using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class DescartesMethodicReconstructionPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;

        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return null;
        WesternJourneyState? journey = philosophy.WesternJourney;
        DescartesMethodicReconstruction? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        DescartesMethodicReconstructionState? saved = philosophy.DescartesMethodicReconstruction;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        var currentMapPoint = run.CurrentMapCoord is not null ? run.CurrentMapPoint : null;
        DescartesMethodicReconstructionTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.CombatCount(behavior),
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
        if (!DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? DescartesMethodicReconstructionState.Create(
            $"DESCARTES_METHODIC_RECONSTRUCTION:{run.Rng.Seed}:2");
        if (!candidate.IsUsable()) return null;
        if (saved is null)
        {
            if (journey is null || !WesternRouteGraph.LoadEmbedded().TryDecline(journey, "Fixed", 2)) return null;
        }
        philosophy.DescartesMethodicReconstruction = candidate;

        Log.Info("[STS2Philosophers] Presenting Descartes's methodic reconstruction at a natural map fork.");
        return canonical;
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
