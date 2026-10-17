using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class PlatoWholeArrangementPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null)
        {
            return;
        }

        if (!RouteEventMapBoundary.IsSafe(run)) return;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return;
        WesternJourneyState? journey = philosophy.WesternJourney;
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        PlatoWholeArrangement? canonical = TryGetCanonicalEvent();
        PlatoWholeArrangementState? saved = philosophy.PlatoWholeArrangement;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        PlatoWholeArrangementTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains("WESTERN_EDGE_008") == true,
            journey?.DeclinedFixedActs.Contains(2) == true,
            journey?.CompletedEdgeIds.Contains(PlatoWholeArrangementState.CompletionEdgeId) == true,
            LaterActRelicEligibilityRuntime.IsEligible(
                player,
                LaterActCandidateIds.Plato,
                run.CurrentActIndex),
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
        }

        Log.Info("[STS2Philosophers] Presenting Plato's act-two whole arrangement.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
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
