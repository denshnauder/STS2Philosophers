using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class FoucaultPowerPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        FoucaultPowerState? saved = shared.FoucaultPower;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !FoucaultPowerFlow.IsRouteConsistent(saved, journey)) return null;
        FoucaultPower? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        FoucaultPowerState? saved = shared.FoucaultPower;
        FoucaultPower? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "MICHEL_FOUCAULT" ? 2
                : route.ActThreeFigure == "MICHEL_FOUCAULT" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct && edge.ToNodeId == FoucaultPowerState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? FoucaultPowerState.CompletionEdgeId
            : FoucaultPowerState.CompletionEdgeId;
        bool edgeEligible = journey is not null
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId);
        FoucaultPowerTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible && journey is not null
                && (routeAct >= 2 || WesternFirstActBranch.IsChosen(run, journey, graph, edgeId)),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && FoucaultPowerFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!FoucaultPowerTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? FoucaultPowerState.Create($"FOUCAULT_POWER:{run.Rng.Seed}:2");
        if (routeAct >= 2)
        {
            candidate.RoutePlaced = true;
            if (candidate.RouteSourceNodeId.Length == 0)
                candidate.RouteSourceNodeId = graph.GetEligibleEdges(journey!, "Fixed")
                    .Where(edge => edge.EdgeId == edgeId).Select(edge => edge.FromNodeId)
                    .FirstOrDefault() ?? string.Empty;
        }
        if (journey is null || !FoucaultPowerFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.FoucaultPower = candidate;
        Log.Info("[STS2Philosophers] Presenting Mill's harm question.");
        return canonical;
    }

    private static FoucaultPower? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<FoucaultPower>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] FoucaultPower is unavailable: {exception}");
            return null;
        }
    }
}
