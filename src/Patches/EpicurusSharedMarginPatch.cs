using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class EpicurusSharedMarginPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null) return;

        if (!RouteEventMapBoundary.IsSafe(run)) return;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return;
        WesternJourneyState? journey = philosophy.WesternJourney;
        EpicurusSharedMargin? canonical = TryGetCanonicalEvent();
        EpicurusSharedMarginState? saved = philosophy.EpicurusSharedMargin;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        EpicurusSharedMarginTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.CompletedEdgeIds.Contains(EpicurusSharedMarginState.RequiredEdgeId) == true,
            LaterActRelicEligibilityRuntime.IsEligible(
                player,
                LaterActCandidateIds.Epicurus,
                run.CurrentActIndex),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!EpicurusSharedMarginTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
        {
            philosophy.EpicurusSharedMargin = EpicurusSharedMarginState.Create(
                $"EPICURUS_SHARED_MARGIN:{run.Rng.Seed}:3");
        }

        Log.Info("[STS2Philosophers] Presenting Epicurus's act-three shared margin.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static EpicurusSharedMargin? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<EpicurusSharedMargin>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] EpicurusSharedMargin is unavailable: {exception}");
            return null;
        }
    }
}
