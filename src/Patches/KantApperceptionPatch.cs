using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class KantApperceptionPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        KantApperceptionState? saved = shared.KantApperception;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !KantApperceptionFlow.IsRouteConsistent(saved, journey)) return null;
        KantApperception? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        KantApperceptionState? saved = shared.KantApperception;
        KantApperception? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "IMMANUEL_KANT" ? 2
                : route.ActThreeFigure == "IMMANUEL_KANT" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        string edgeId = routeAct >= 2
            ? graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.Act == routeAct && edge.ToNodeId == KantApperceptionState.AdoptedNodeId)
                .Select(edge => edge.EdgeId).FirstOrDefault()
                ?? KantApperceptionState.CompletionEdgeId
            : KantApperceptionState.CompletionEdgeId;
        // The first act's question can put the run on this figure before the act
        // two handover is walked; standing on it is what that handover means.
        bool edgeEligible = journey is not null
            && (journey.CurrentNodeId == KantApperceptionState.AdoptedNodeId
                || graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId));
        KantApperceptionTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            routeAct >= 2
                ? NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true)
                : NativeQuestionNodeEvents.HasCompletedCombatAfterAcrossActs(run,
                    ModelDb.GetId<HumeInduction>(), "HUME_INDUCTION"),
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey is not null && KantApperceptionState.HasHumeArrival(journey),
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && KantApperceptionFlow.IsRouteConsistent(saved, journey),
            canonical is not null,
            routeAct);
        if (!KantApperceptionTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? (routeAct >= 2
            ? KantApperceptionState.CreateAt($"KANT_APPERCEPTION:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
            : KantApperceptionState.Create($"KANT_APPERCEPTION:{run.Rng.Seed}:3"));
        if (routeAct >= 2 && candidate.RouteSourceNodeId.Length == 0)
            candidate.RouteSourceNodeId = graph.GetEligibleEdges(journey!, "Fixed")
                .Where(edge => edge.EdgeId == edgeId).Select(edge => edge.FromNodeId).FirstOrDefault() ?? string.Empty;
        if (journey is null || !KantApperceptionFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.KantApperception = candidate;

        Log.Info("[STS2Philosophers] Presenting Kant's apperception question.");
        return canonical;
    }

    private static KantApperception? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<KantApperception>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] KantApperception is unavailable: {exception}");
            return null;
        }
    }
}
