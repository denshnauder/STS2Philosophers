using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class HobbesAuthorizedSovereigntyPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        HobbesAuthorizedSovereigntyState? saved = shared.HobbesAuthorizedSovereignty;
        HobbesAuthorizedSovereignty? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == HobbesAuthorizedSovereigntyState.CompletionEdgeId);
        HobbesAuthorizedSovereigntyTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                ModelDb.GetId<AristotleCivicInquiry>(), "ARISTOTLE_CIVIC_INQUIRY"),
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(HobbesAuthorizedSovereigntyState.RequiredEdgeId) == true,
            edgeEligible,
            shared.AristotleCivicInquiry?.Closed == true,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && HobbesAuthorizedSovereigntyFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!HobbesAuthorizedSovereigntyTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? HobbesAuthorizedSovereigntyState.Create(
            $"HOBBES_AUTHORIZED_SOVEREIGNTY:{run.Rng.Seed}:3");
        if (journey is null || !HobbesAuthorizedSovereigntyFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.HobbesAuthorizedSovereignty = candidate;

        Log.Info("[STS2Philosophers] Presenting Hobbes's act-three authorization.");
        return canonical;
    }

    private static HobbesAuthorizedSovereignty? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<HobbesAuthorizedSovereignty>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] HobbesAuthorizedSovereignty is unavailable: {exception}");
            return null;
        }
    }
}
