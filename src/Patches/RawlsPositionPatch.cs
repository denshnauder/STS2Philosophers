using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class RawlsPositionPatch
{
    // Two lines close on Rawls: the act-three fixed room after Kant's
    // institutions (172 -> 173) and after Mill's freedom (169 -> 170). The line
    // the run already walked names the edge, so the room never guesses.
    private static readonly string[] RequiredEdges =
        [RawlsPositionState.KantRequiredEdgeId, RawlsPositionState.MillRequiredEdgeId,
            RawlsPositionState.EarlyMillRequiredEdgeId];

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        RawlsPositionState? saved = shared.RawlsPosition;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !RawlsPositionFlow.IsRouteConsistent(saved, journey)) return null;
        RawlsPosition? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        RawlsPositionState? saved = shared.RawlsPosition;
        RawlsPosition? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        string? required = saved?.RequiredEdgeId
            ?? RequiredEdges.FirstOrDefault(edge => journey?.CompletedEdgeIds.Contains(edge) == true);
        if (required is null || journey is null) return null;
        string completion = RawlsPositionFlow.CompletionEdgeIdOf(required);
        bool arrived = journey.CurrentNodeId == RawlsPositionState.AdoptedNodeId
            && journey.CompletedEdgeIds.Contains(completion);
        if (arrived)
        {
            var arrivedState = saved ?? RawlsPositionState.Create($"RAWLS_POSITION:{run.Rng.Seed}:3", required);
            if (!RawlsPositionFlow.IsRouteConsistent(arrivedState, journey)) return null;
            shared.RawlsPosition = arrivedState;
            Log.Info("[STS2Philosophers] Presenting Rawls's original position after Mill's handover.");
            return canonical;
        }
        if (journey.CurrentNodeId != RawlsPositionFlow.SourceNodeIdOf(required)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == completion);
        bool afterMill = required is RawlsPositionState.MillRequiredEdgeId
            or RawlsPositionState.EarlyMillRequiredEdgeId;
        ModelId predecessor = afterMill
            ? ModelDb.GetId<MillHarm>() : ModelDb.GetId<KantUniversalization>();
        string predecessorRoot = afterMill ? "MILL_HARM" : "KANT_UNIVERSALIZATION";
        RawlsPositionTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombatAfter(run, predecessor, predecessorRoot),
            NativeQuestionNodeEvents.CombatCount(behavior), journey.CurrentNodeId, journey.LastFixedAct,
            journey.CompletedEdgeIds.Contains(required), edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && RawlsPositionFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!RawlsPositionTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? RawlsPositionState.Create($"RAWLS_POSITION:{run.Rng.Seed}:3", required);
        if (!RawlsPositionFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.RawlsPosition = candidate;
        Log.Info("[STS2Philosophers] Presenting Rawls's original position.");
        return canonical;
    }

    private static RawlsPosition? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<RawlsPosition>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] RawlsPosition is unavailable: {exception}");
            return null;
        }
    }
}
