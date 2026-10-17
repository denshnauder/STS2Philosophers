using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class DemocritusArrangementPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        WesternJourneyState? journey = shared.WesternJourney;
        DemocritusArrangementState? saved = shared.DemocritusArrangement;
        DemocritusArrangement? canonical = TryGetCanonicalEvent();
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        DemocritusArrangementTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Democritus, run.CurrentActIndex),
            journey?.CompletedEdgeIds.Contains(DemocritusArrangementState.PrerequisiteEdgeId) == true,
            journey?.RetainedEnding == true,
            RouteEventMapBoundary.IsSafe(run),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!DemocritusArrangementTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
            shared.DemocritusArrangement = DemocritusArrangementState.Create(
                $"DEMOCRITUS_ARRANGEMENT:{run.Rng.Seed}:2");
        if (!DemocritusArrangement.IsJourneyConsistent(shared))
            throw new InvalidDataException("The Democritus boundary conflicts with its route.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static DemocritusArrangement? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<DemocritusArrangement>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] DemocritusArrangement is unavailable: {exception}");
            return null;
        }
    }
}
