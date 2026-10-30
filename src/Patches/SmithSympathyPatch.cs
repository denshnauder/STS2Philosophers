using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SmithSympathyPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        SmithSympathyState? saved = shared.SmithSympathy;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !SmithSympathyFlow.IsRouteConsistent(saved, journey)) return null;
        SmithSympathy? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        SmithSympathyState? saved = shared.SmithSympathy;
        SmithSympathy? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == SmithSympathyState.CompletionEdgeId);
        SmithSympathyTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            behavior?.CompletedCombatCount ?? 0, journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible && journey is not null
                && WesternFirstActBranch.IsChosen(run, journey, WesternRouteGraph.LoadEmbedded(),
                    SmithSympathyState.CompletionEdgeId),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && SmithSympathyFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!SmithSympathyTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? SmithSympathyState.Create($"SMITH_SYMPATHY:{run.Rng.Seed}:1");
        if (journey is null || !SmithSympathyFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.SmithSympathy = candidate;
        Log.Info("[STS2Philosophers] Presenting Mill's harm question.");
        return canonical;
    }

    private static SmithSympathy? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<SmithSympathy>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] SmithSympathy is unavailable: {exception}");
            return null;
        }
    }
}
