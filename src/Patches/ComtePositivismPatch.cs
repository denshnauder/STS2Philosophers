using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ComtePositivismPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        ComtePositivismState? saved = shared.ComtePositivism;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !ComtePositivismFlow.IsRouteConsistent(saved, journey)) return null;
        ComtePositivism? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        ComtePositivismState? saved = shared.ComtePositivism;
        ComtePositivism? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "AUGUSTE_COMTE" ? 2
                : route.ActThreeFigure == "AUGUSTE_COMTE" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct && edge.ToNodeId == ComtePositivismState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? ComtePositivismState.CompletionEdgeId
            : ComtePositivismState.CompletionEdgeId;
        bool edgeEligible = journey is not null && (routeAct >= 2
            ? graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId)
            : graph.GetEligibleEdges(journey, "Question").Any(edge => edge.EdgeId == ComtePositivismState.CompletionEdgeId));
        ComtePositivismTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            routeAct >= 2 || journey?.CompletedEdgeIds.Contains(ComtePositivismState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && ComtePositivismFlow.IsRouteConsistent(saved, journey),
            canonical is not null, routeAct);
        if (!ComtePositivismTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? (routeAct >= 2
            ? ComtePositivismState.CreateAt($"COMTE_POSITIVISM:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
            : ComtePositivismState.Create($"COMTE_POSITIVISM:{run.Rng.Seed}:3"));
        if (routeAct >= 2)
        {
            candidate.RoutePlaced = true;
            if (candidate.RouteSourceNodeId.Length == 0)
                candidate.RouteSourceNodeId = graph.GetEligibleEdges(journey!, "Fixed")
                    .Where(edge => edge.EdgeId == edgeId).Select(edge => edge.FromNodeId)
                    .FirstOrDefault() ?? string.Empty;
        }
        if (journey is null || !ComtePositivismFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.ComtePositivism = candidate;
        Log.Info("[STS2Philosophers] Presenting Marx's alienation question.");
        return canonical;
    }

    private static ComtePositivism? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<ComtePositivism>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] ComtePositivism is unavailable: {exception}");
            return null;
        }
    }
}
