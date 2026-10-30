using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class FreudUnconsciousPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        FreudUnconsciousState? saved = shared.FreudUnconscious;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !FreudUnconsciousFlow.IsRouteConsistent(saved, journey)) return null;
        FreudUnconscious? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        FreudUnconsciousState? saved = shared.FreudUnconscious;
        FreudUnconscious? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == FreudUnconsciousState.CompletionEdgeId);
        FreudUnconsciousTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            behavior?.CompletedCombatCount ?? 0, journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible && journey is not null
                && WesternFirstActBranch.IsChosen(run, journey, WesternRouteGraph.LoadEmbedded(),
                    FreudUnconsciousState.CompletionEdgeId),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && FreudUnconsciousFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!FreudUnconsciousTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? FreudUnconsciousState.Create($"FREUD_UNCONSCIOUS:{run.Rng.Seed}:1");
        if (journey is null || !FreudUnconsciousFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.FreudUnconscious = candidate;
        Log.Info("[STS2Philosophers] Presenting Freud's unconscious question.");
        return canonical;
    }

    private static FreudUnconscious? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<FreudUnconscious>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] FreudUnconscious is unavailable: {exception}");
            return null;
        }
    }
}
