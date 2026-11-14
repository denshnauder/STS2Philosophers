using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class KierkegaardFaithPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        KierkegaardFaithState? saved = shared.KierkegaardFaith;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !KierkegaardFaithFlow.IsRouteConsistent(saved, journey)) return null;
        KierkegaardFaith? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        bool fixedEntry = FixedEntryScope.Active;
        if ((!fixedEntry && !NativeQuestionNodeEvents.IsEntry(run)) || run.Players.Count != 1) return null;
        if (fixedEntry && run.CurrentActIndex != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        KierkegaardFaithState? saved = shared.KierkegaardFaith;
        KierkegaardFaith? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        int routeAct = journey is null ? 0 : WesternRouteTable.Instance.Routes
            .Where(route => route.RouteId == journey.RouteId)
            .Select(route => route.ActTwoFigure == "SOREN_KIERKEGAARD" ? 2
                : route.ActThreeFigure == "SOREN_KIERKEGAARD" ? 3 : 0)
            .DefaultIfEmpty(0).First();
        bool earlyKierkegaard = journey is not null && !fixedEntry
            && run.CurrentActIndex == 0
            && journey.CurrentNodeId == KierkegaardFaithState.AdoptedNodeId
            && journey.CompletedEdgeIds.Contains("WESTERN_EDGE_158")
            && journey.LastFixedAct == 1;
        if (earlyKierkegaard) routeAct = 1;
        // The default fixed room belongs only to the individual-commitment line;
        // another line's Socrates second act must keep its own room.
        if (fixedEntry && routeAct != 2) return null;
        string edgeId = earlyKierkegaard
            ? "WESTERN_EDGE_158"
            : routeAct >= 2
                ? graph.GetEligibleEdges(journey!, "Fixed")
                    .Where(edge => edge.Act == routeAct && edge.ToNodeId == KierkegaardFaithState.AdoptedNodeId)
                    .Select(edge => edge.EdgeId).FirstOrDefault()
                    ?? KierkegaardFaithState.CompletionEdgeId
                : KierkegaardFaithState.CompletionEdgeId;
        bool edgeEligible = earlyKierkegaard || journey is not null
            && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == edgeId);
        KierkegaardFaithTriggerContext context = new(
            RunManager.Instance.IsInProgress, fixedEntry || NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            edgeEligible && journey is not null
                && (earlyKierkegaard || routeAct >= 2
                    || WesternFirstActBranch.IsOnBranch(run, journey, graph, KierkegaardFaithState.SourceNodeId)),
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && KierkegaardFaithFlow.IsRouteConsistent(saved, journey),
            canonical is not null,
            routeAct, fixedEntry);
        if (!KierkegaardFaithTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? (earlyKierkegaard
            ? KierkegaardFaithState.CreateAt($"KIERKEGAARD_FAITH:{run.Rng.Seed}:1", 1, edgeId)
            : fixedEntry
                ? KierkegaardFaithState.CreateAt($"KIERKEGAARD_FAITH:{run.Rng.Seed}:2", 2, edgeId)
                : routeAct >= 2
                    ? KierkegaardFaithState.CreateAt($"KIERKEGAARD_FAITH:{run.Rng.Seed}:{routeAct}", routeAct, edgeId)
                    : KierkegaardFaithState.Create($"KIERKEGAARD_FAITH:{run.Rng.Seed}:1"));
        if (journey is null || !KierkegaardFaithFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.KierkegaardFaith = candidate;
        Log.Info("[STS2Philosophers] Presenting Kierkegaard's faith room.");
        return canonical;
    }

    private static KierkegaardFaith? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<KierkegaardFaith>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] KierkegaardFaith is unavailable: {exception}");
            return null;
        }
    }
}
