using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class RousseauConsentPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        RousseauConsentState? saved = shared.RousseauConsent;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !RousseauConsentFlow.IsRouteConsistent(saved, journey)) return null;
        RousseauConsent? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        RousseauConsentState? saved = shared.RousseauConsent;
        RousseauConsent? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == RousseauConsentState.CompletionEdgeId);
        RousseauConsentTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                ModelDb.GetId<AugustinePromise>(), "AUGUSTINE_PROMISE"),
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(RousseauConsentState.RequiredEdgeId) == true,
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && RousseauConsentFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!RousseauConsentTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? RousseauConsentState.Create($"ROUSSEAU_CONSENT:{run.Rng.Seed}:2");
        if (journey is null || !RousseauConsentFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.RousseauConsent = candidate;

        Log.Info("[STS2Philosophers] Presenting Rousseau's consent question.");
        return canonical;
    }

    private static RousseauConsent? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<RousseauConsent>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] RousseauConsent is unavailable: {exception}");
            return null;
        }
    }
}
