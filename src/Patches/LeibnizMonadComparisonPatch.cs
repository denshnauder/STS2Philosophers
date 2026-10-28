using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class LeibnizMonadComparisonPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        LeibnizMonadComparisonState? saved = shared.LeibnizMonadComparison;
        LeibnizMonadComparison? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == LeibnizMonadComparisonState.CompletionEdgeId);
        LeibnizMonadComparisonTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                ModelDb.GetId<SpinozaCauseNetwork>(), "SPINOZA_CAUSE_NETWORK"),
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(LeibnizMonadComparisonState.RequiredEdgeId) == true,
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && LeibnizMonadComparisonFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!LeibnizMonadComparisonTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? LeibnizMonadComparisonState.Create(
            $"LEIBNIZ_MONAD_COMPARISON:{run.Rng.Seed}:2");
        if (journey is null || !LeibnizMonadComparisonFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.LeibnizMonadComparison = candidate;

        Log.Info("[STS2Philosophers] Presenting Leibniz's monad comparison.");
        return canonical;
    }

    private static LeibnizMonadComparison? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<LeibnizMonadComparison>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] LeibnizMonadComparison is unavailable: {exception}");
            return null;
        }
    }
}
