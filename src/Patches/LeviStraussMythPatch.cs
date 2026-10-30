using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class LeviStraussMythPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        LeviStraussMythState? saved = shared.LeviStraussMyth;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !LeviStraussMythFlow.IsRouteConsistent(saved, journey)) return null;
        LeviStraussMyth? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        LeviStraussMythState? saved = shared.LeviStraussMyth;
        LeviStraussMyth? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "CLAUDE_LEVI_STRAUSS" ? 2
                : route.ActThreeFigure == "CLAUDE_LEVI_STRAUSS" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct && edge.ToNodeId == LeviStraussMythState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? LeviStraussMythState.CompletionEdgeId
            : LeviStraussMythState.CompletionEdgeId;
        bool edgeEligible = journey is not null
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId);
        LeviStraussMythTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible && journey is not null
                && (routeAct >= 2 || WesternFirstActBranch.IsChosen(run, journey, graph, edgeId)),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && LeviStraussMythFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!LeviStraussMythTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? LeviStraussMythState.Create($"LEVI_STRAUSS_MYTH:{run.Rng.Seed}:2");
        if (routeAct >= 2)
        {
            candidate.RoutePlaced = true;
            if (candidate.RouteSourceNodeId.Length == 0)
                candidate.RouteSourceNodeId = graph.GetEligibleEdges(journey!, "Fixed")
                    .Where(edge => edge.EdgeId == edgeId).Select(edge => edge.FromNodeId)
                    .FirstOrDefault() ?? string.Empty;
        }
        if (journey is null || !LeviStraussMythFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.LeviStraussMyth = candidate;
        Log.Info("[STS2Philosophers] Presenting Mill's harm question.");
        return canonical;
    }

    private static LeviStraussMyth? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<LeviStraussMyth>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] LeviStraussMyth is unavailable: {exception}");
            return null;
        }
    }
}
