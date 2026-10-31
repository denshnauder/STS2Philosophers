using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class HegelSubstanceSubjectPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        HegelSubstanceSubjectState? saved = shared.HegelSubstanceSubject;
        HegelSubstanceSubject? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        // A run the first act's question turned onto Kant meets Hegel a whole act
        // before the line would otherwise place him, and by that turn rather than by
        // the comparison the line normally puts in front of him.
        bool turned = journey is not null
            && journey.CurrentNodeId == HegelSubstanceSubjectState.TurnedSourceNodeId
            && journey.ResolvedQuestionActs.Contains(1);
        string adoptEdge = turned
            ? HegelSubstanceSubjectState.TurnedAdoptEdgeId
            : HegelSubstanceSubjectState.CompletionEdgeId;
        string requiredEdge = turned
            ? HegelSubstanceSubjectState.TurnedRequiredEdgeId
            : HegelSubstanceSubjectState.RequiredEdgeId;
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == adoptEdge);
        HegelSubstanceSubjectTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            turned || NativeQuestionNodeEvents.HasCompletedCombatAfterAcrossActs(run,
                ModelDb.GetId<LeibnizMonadComparison>(), "LEIBNIZ_MONAD_COMPARISON"),
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(requiredEdge) == true,
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && HegelSubstanceSubjectFlow.IsRouteConsistent(saved, journey),
            canonical is not null,
            turned);
        if (!HegelSubstanceSubjectTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved;
        if (candidate is null)
        {
            candidate = HegelSubstanceSubjectState.Create($"HEGEL_SUBSTANCE_SUBJECT:{run.Rng.Seed}:3");
            if (turned)
            {
                candidate.ActIndex = 2;
                candidate.RequiredEdge = requiredEdge;
                candidate.AdoptEdge = adoptEdge;
                candidate.AdoptedNode = HegelSubstanceSubjectState.TurnedAdoptedNodeId;
                candidate.SourceNode = HegelSubstanceSubjectState.TurnedSourceNodeId;
            }
        }
        if (journey is null || !HegelSubstanceSubjectFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.HegelSubstanceSubject = candidate;

        Log.Info("[STS2Philosophers] Presenting Hegel's substance-as-subject question.");
        return canonical;
    }

    private static HegelSubstanceSubject? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<HegelSubstanceSubject>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] HegelSubstanceSubject is unavailable: {exception}");
            return null;
        }
    }
}
