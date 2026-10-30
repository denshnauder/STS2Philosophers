using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class DeleuzeDifferencePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        DeleuzeDifferenceState? saved = shared.DeleuzeDifference;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !DeleuzeDifferenceFlow.IsRouteConsistent(saved, journey)) return null;
        DeleuzeDifference? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        DeleuzeDifferenceState? saved = shared.DeleuzeDifference;
        DeleuzeDifference? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == DeleuzeDifferenceState.CompletionEdgeId);
        DeleuzeDifferenceTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            DeleuzeDifferenceState.RequiredEdgeId.Length == 0
                || journey?.CompletedEdgeIds.Contains(DeleuzeDifferenceState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && DeleuzeDifferenceFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!DeleuzeDifferenceTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? DeleuzeDifferenceState.Create($"DELEUZE_DIFFERENCE:{run.Rng.Seed}:3");
        if (journey is null || !DeleuzeDifferenceFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.DeleuzeDifference = candidate;
        Log.Info("[STS2Philosophers] Presenting Marx's alienation question.");
        return canonical;
    }

    private static DeleuzeDifference? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<DeleuzeDifference>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] DeleuzeDifference is unavailable: {exception}");
            return null;
        }
    }
}
