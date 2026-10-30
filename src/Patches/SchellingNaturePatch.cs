using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class SchellingNaturePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        SchellingNatureState? saved = shared.SchellingNature;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !SchellingNatureFlow.IsRouteConsistent(saved, journey)) return null;
        SchellingNature? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        SchellingNatureState? saved = shared.SchellingNature;
        SchellingNature? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "FRIEDRICH_WILHELM_JOSEPH_SCHELLING" ? 2
                : route.ActThreeFigure == "FRIEDRICH_WILHELM_JOSEPH_SCHELLING" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct && edge.ToNodeId == SchellingNatureState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? SchellingNatureState.CompletionEdgeId
            : SchellingNatureState.CompletionEdgeId;
        bool edgeEligible = journey is not null
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId);
        SchellingNatureTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible && journey is not null
                && (routeAct >= 2 || WesternFirstActBranch.IsChosen(run, journey, graph,
                    SchellingNatureState.CompletionEdgeId)),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && SchellingNatureFlow.IsRouteConsistent(saved, journey),
            canonical is not null, routeAct);
        if (!SchellingNatureTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? (routeAct >= 2
            ? SchellingNatureState.CreateAt($"SCHELLING_NATURE:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
            : SchellingNatureState.Create($"SCHELLING_NATURE:{run.Rng.Seed}:2"));
        if (routeAct >= 2)
        {
            candidate.RoutePlaced = true;
            if (candidate.RouteSourceNodeId.Length == 0)
                candidate.RouteSourceNodeId = graph.GetEligibleEdges(journey!, "Fixed")
                    .Where(edge => edge.EdgeId == edgeId).Select(edge => edge.FromNodeId)
                    .FirstOrDefault() ?? string.Empty;
        }
        if (journey is null || !SchellingNatureFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.SchellingNature = candidate;
        Log.Info("[STS2Philosophers] Presenting Mill's harm question.");
        return canonical;
    }

    private static SchellingNature? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<SchellingNature>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] SchellingNature is unavailable: {exception}");
            return null;
        }
    }
}
