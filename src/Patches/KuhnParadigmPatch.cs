using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class KuhnParadigmPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        KuhnParadigmState? saved = shared.KuhnParadigm;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !KuhnParadigmFlow.IsRouteConsistent(saved, journey)) return null;
        KuhnParadigm? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        KuhnParadigmState? saved = shared.KuhnParadigm;
        KuhnParadigm? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null
            && WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Question")
                .Any(edge => edge.EdgeId == KuhnParadigmState.CompletionEdgeId);
        KuhnParadigmTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(KuhnParadigmState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && KuhnParadigmFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!KuhnParadigmTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? KuhnParadigmState.Create($"KUHN_PARADIGM:{run.Rng.Seed}:3");
        if (journey is null || !KuhnParadigmFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.KuhnParadigm = candidate;
        Log.Info("[STS2Philosophers] Presenting Kuhn's paradigm question.");
        return canonical;
    }

    private static KuhnParadigm? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<KuhnParadigm>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] KuhnParadigm is unavailable: {exception}");
            return null;
        }
    }
}
