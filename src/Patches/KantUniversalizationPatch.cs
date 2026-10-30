using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class KantUniversalizationPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        KantUniversalizationState? saved = shared.KantUniversalization;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !KantUniversalizationFlow.IsRouteConsistent(saved, journey)) return null;
        KantUniversalization? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        KantUniversalizationState? saved = shared.KantUniversalization;
        KantUniversalization? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        // The route's own handover into Kant names the act this room stands in;
        // the legacy path keeps the room on its own edge.
        // The handover only places the room when it fills the slot this boundary
        // is opening; an earlier or later act's edge is just visible, not this
        // room's.
        WesternGraphEdge? handover = journey is null ? null : graph.GetEligibleEdges(journey, "Fixed")
            .FirstOrDefault(edge => edge.ToNodeId == KantUniversalizationState.AdoptedNodeId
                && edge.Act is 2 or 3 && edge.Act == journey.LastFixedAct);
        int routeAct = handover?.Act ?? 0;
        string edgeId = handover?.EdgeId ?? KantUniversalizationState.CompletionEdgeId;
        bool edgeEligible = journey is not null
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId);
        KantUniversalizationTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            // A route-placed room follows the act's own fight; the legacy path
            // keeps the fight it has always asked for.
            routeAct >= 2
                ? NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true)
                : NativeQuestionNodeEvents.HasCompletedCombatAfterAcrossActs(run,
                    ModelDb.GetId<RousseauConsent>(), "ROUSSEAU_CONSENT"),
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(KantUniversalizationState.RequiredEdgeId) == true,
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && KantUniversalizationFlow.IsRouteConsistent(saved, journey),
            canonical is not null,
            routeAct);
        if (!KantUniversalizationTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? (routeAct >= 2
            ? KantUniversalizationState.CreateAt($"KANT_UNIVERSALIZATION:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
            : KantUniversalizationState.Create($"KANT_UNIVERSALIZATION:{run.Rng.Seed}:3"));
        if (routeAct >= 2)
        {
            candidate.RoutePlaced = true;
            if (candidate.RouteSourceNodeId.Length == 0)
                candidate.RouteSourceNodeId = handover!.FromNodeId;
        }
        if (journey is null || !KantUniversalizationFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.KantUniversalization = candidate;

        Log.Info("[STS2Philosophers] Presenting Kant's universalization question.");
        return canonical;
    }

    private static KantUniversalization? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<KantUniversalization>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] KantUniversalization is unavailable: {exception}");
            return null;
        }
    }
}
