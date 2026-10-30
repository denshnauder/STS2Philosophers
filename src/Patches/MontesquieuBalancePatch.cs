using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class MontesquieuBalancePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        MontesquieuBalanceState? saved = shared.MontesquieuBalance;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !MontesquieuBalanceFlow.IsRouteConsistent(saved, journey)) return null;
        MontesquieuBalance? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        MontesquieuBalanceState? saved = shared.MontesquieuBalance;
        MontesquieuBalance? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        // A route that names Montesquieu in a later act brings its own fixed edge
        // and its own position; otherwise this stays the first act's question.
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "MONTESQUIEU" ? 2
                : route.ActThreeFigure == "MONTESQUIEU" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct
                    && edge.ToNodeId == MontesquieuBalanceState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? MontesquieuBalanceState.CompletionEdgeId
            : MontesquieuBalanceState.CompletionEdgeId;
        bool edgeEligible = journey is not null
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId);
        MontesquieuBalanceTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible && journey is not null
                && (routeAct >= 2 || WesternFirstActBranch.IsChosen(run, journey, graph, edgeId)),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && MontesquieuBalanceFlow.IsRouteConsistent(saved, journey),
            canonical is not null,
            routeAct);
        if (!MontesquieuBalanceTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? (routeAct >= 2
            ? MontesquieuBalanceState.CreateAt($"MONTESQUIEU_BALANCE:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
            : MontesquieuBalanceState.Create($"MONTESQUIEU_BALANCE:{run.Rng.Seed}:1"));
        if (journey is null || !MontesquieuBalanceFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.MontesquieuBalance = candidate;
        Log.Info("[STS2Philosophers] Presenting Montesquieu's balance question.");
        return canonical;
    }

    private static MontesquieuBalance? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<MontesquieuBalance>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] MontesquieuBalance is unavailable: {exception}");
            return null;
        }
    }
}
