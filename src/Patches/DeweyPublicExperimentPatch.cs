using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class DeweyPublicExperimentPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        DeweyPublicExperimentState? saved = shared.DeweyPublicExperiment;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !DeweyPublicExperimentFlow.IsRouteConsistent(saved, journey)) return null;
        DeweyPublicExperiment? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        DeweyPublicExperimentState? saved = shared.DeweyPublicExperiment;
        DeweyPublicExperiment? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "JOHN_DEWEY" ? 2
                : route.ActThreeFigure == "JOHN_DEWEY" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct && edge.ToNodeId == DeweyPublicExperimentState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? DeweyPublicExperimentState.CompletionEdgeId
            : DeweyPublicExperimentState.CompletionEdgeId;
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == DeweyPublicExperimentState.CompletionEdgeId);
        DeweyPublicExperimentTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(DeweyPublicExperimentState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && DeweyPublicExperimentFlow.IsRouteConsistent(saved, journey),
            canonical is not null, routeAct);
        if (!DeweyPublicExperimentTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? (routeAct >= 2
            ? DeweyPublicExperimentState.CreateAt($"DEWEY_PUBLIC_EXPERIMENT:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
            : DeweyPublicExperimentState.Create($"DEWEY_PUBLIC_EXPERIMENT:{run.Rng.Seed}:3"));
        if (routeAct >= 2)
        {
            candidate.RoutePlaced = true;
            if (candidate.RouteSourceNodeId.Length == 0)
                candidate.RouteSourceNodeId = graph.GetEligibleEdges(journey!, "Fixed")
                    .Where(edge => edge.EdgeId == edgeId).Select(edge => edge.FromNodeId)
                    .FirstOrDefault() ?? string.Empty;
        }
        if (journey is null || !DeweyPublicExperimentFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.DeweyPublicExperiment = candidate;
        Log.Info("[STS2Philosophers] Presenting Marx's alienation question.");
        return canonical;
    }

    private static DeweyPublicExperiment? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<DeweyPublicExperiment>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] DeweyPublicExperiment is unavailable: {exception}");
            return null;
        }
    }
}
