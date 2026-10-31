using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class LacanSignifierPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        LacanSignifierState? saved = shared.LacanSignifier;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !LacanSignifierFlow.IsRouteConsistent(saved, journey)) return null;
        LacanSignifier? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        LacanSignifierState? saved = shared.LacanSignifier;
        LacanSignifier? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "JACQUES_LACAN" ? 2
                : route.ActThreeFigure == "JACQUES_LACAN" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct && edge.ToNodeId == LacanSignifierState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? LacanSignifierState.CompletionEdgeId
            : LacanSignifierState.CompletionEdgeId;
        bool edgeEligible = journey is not null
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId);
        LacanSignifierTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible, saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && LacanSignifierFlow.IsRouteConsistent(saved, journey),
            canonical is not null,
            routeAct,
            // The first act's question can put the run on Freud before the act two
            // handover is walked; the line then runs one act ahead.
            journey is not null
                && journey.CurrentNodeId == LacanSignifierState.SourceNodeId
                && journey.ResolvedQuestionActs.Contains(1));
        if (!LacanSignifierTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? (routeAct >= 2
            ? LacanSignifierState.CreateAt($"LACAN_SIGNIFIER:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
            : LacanSignifierState.Create($"LACAN_SIGNIFIER:{run.Rng.Seed}:2"));
        if (journey is null || !LacanSignifierFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.LacanSignifier = candidate;
        Log.Info("[STS2Philosophers] Presenting Lacan's signifier question.");
        return canonical;
    }

    private static LacanSignifier? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<LacanSignifier>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] LacanSignifier is unavailable: {exception}");
            return null;
        }
    }
}
