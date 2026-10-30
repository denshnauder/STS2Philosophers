using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class NietzscheRevaluationPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        NietzscheRevaluationState? saved = shared.NietzscheRevaluation;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !NietzscheRevaluationFlow.IsRouteConsistent(saved, journey)) return null;
        NietzscheRevaluation? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        NietzscheRevaluationState? saved = shared.NietzscheRevaluation;
        NietzscheRevaluation? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "FRIEDRICH_NIETZSCHE" ? 2
                : route.ActThreeFigure == "FRIEDRICH_NIETZSCHE" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct && edge.ToNodeId == NietzscheRevaluationState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? NietzscheRevaluationState.CompletionEdgeId
            : NietzscheRevaluationState.CompletionEdgeId;
        bool edgeEligible = journey is not null
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId);
        NietzscheRevaluationTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible && journey is not null
                && (routeAct >= 2 || WesternFirstActBranch.IsChosen(run, journey, graph, edgeId)),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && NietzscheRevaluationFlow.IsRouteConsistent(saved, journey),
            canonical is not null,
            routeAct);
        if (!NietzscheRevaluationTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? (routeAct >= 2
            ? NietzscheRevaluationState.CreateAt($"NIETZSCHE_REVALUATION:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
            : NietzscheRevaluationState.Create($"NIETZSCHE_REVALUATION:{run.Rng.Seed}:1"));
        if (routeAct >= 2)
        {
            candidate.RoutePlaced = true;
            if (candidate.RouteSourceNodeId.Length == 0)
                candidate.RouteSourceNodeId = graph.GetEligibleEdges(journey!, "Fixed")
                    .Where(edge => edge.EdgeId == edgeId).Select(edge => edge.FromNodeId)
                    .FirstOrDefault() ?? string.Empty;
        }
        if (journey is null || !NietzscheRevaluationFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.NietzscheRevaluation = candidate;
        Log.Info("[STS2Philosophers] Presenting Mill's harm question.");
        return canonical;
    }

    private static NietzscheRevaluation? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<NietzscheRevaluation>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] NietzscheRevaluation is unavailable: {exception}");
            return null;
        }
    }
}
