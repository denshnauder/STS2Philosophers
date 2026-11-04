using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class KuhnParadigmPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        bool fixedEntry = FixedEntryScope.Active;
        if ((!fixedEntry && !NativeQuestionNodeEvents.IsEntry(run))
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
        bool fixedEntry = FixedEntryScope.Active;
        if ((!fixedEntry && !NativeQuestionNodeEvents.IsEntry(run)) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        KuhnParadigmState? saved = shared.KuhnParadigm;
        KuhnParadigm? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = 3;
        bool savedUsable = saved is not null && saved.IsUsable();
        string edgeId = savedUsable ? saved!.EdgeId : SelectEdgeId(journey);
        if (edgeId.Length == 0) return null;
        string requiredEdgeId = KuhnParadigmState.RequiredEdgeFor(edgeId);
        bool edgeEligible = journey is not null
            && (saved is { FixedMode: false }
                ? graph.GetEligibleEdges(journey, "Question").Any(edge => edge.EdgeId == edgeId)
                : graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId));
        KuhnParadigmTriggerContext context = new(
            RunManager.Instance.IsInProgress, fixedEntry || NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1, run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(requiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && KuhnParadigmFlow.IsRouteConsistent(saved, journey),
            canonical is not null, routeAct);
        if (!KuhnParadigmTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? KuhnParadigmState.CreateAt(
            $"KUHN_PARADIGM:{run.Rng.Seed}:{routeAct}", routeAct, edgeId);
        if (candidate.FixedMode)
        {
            candidate.RoutePlaced = true;
            if (candidate.RouteSourceNodeId.Length == 0)
                candidate.RouteSourceNodeId = graph.GetEdge(candidate.EdgeId).FromNodeId;
        }
        if (journey is null || !KuhnParadigmFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.KuhnParadigm = candidate;
        Log.Info("[STS2Philosophers] Presenting Kuhn's paradigm room.");
        return canonical;
    }

    private static string SelectEdgeId(WesternJourneyState? journey)
    {
        if (journey is null) return string.Empty;
        if (journey.CurrentNodeId == KuhnParadigmState.ComteSourceNodeId)
            return KuhnParadigmState.ComteCompletionEdgeId;
        if (journey.CurrentNodeId == KuhnParadigmState.NewtonSourceNodeId)
            return KuhnParadigmState.NewtonCompletionEdgeId;
        return journey.RouteId switch
        {
            KuhnParadigmState.SocialRouteId => KuhnParadigmState.ComteCompletionEdgeId,
            KuhnParadigmState.ScientificRouteId => KuhnParadigmState.NewtonCompletionEdgeId,
            _ => string.Empty,
        };
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
