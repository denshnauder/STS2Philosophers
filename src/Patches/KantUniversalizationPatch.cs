using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class KantUniversalizationPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        KantUniversalizationState? saved = shared.KantUniversalization;
        KantUniversalization? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == KantUniversalizationState.CompletionEdgeId);
        KantUniversalizationTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                ModelDb.GetId<RousseauConsent>(), "ROUSSEAU_CONSENT"),
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(KantUniversalizationState.RequiredEdgeId) == true,
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && KantUniversalizationFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!KantUniversalizationTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? KantUniversalizationState.Create($"KANT_UNIVERSALIZATION:{run.Rng.Seed}:3");
        if (journey is null || !KantUniversalizationFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.KantUniversalization = candidate;

        Log.Info("[STS2Philosophers] Presenting Kant's universalization question.");
        return canonical;
    }

    private static KantUniversalization? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<KantUniversalization>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] KantUniversalization is unavailable: {exception}");
            return null;
        }
    }
}
