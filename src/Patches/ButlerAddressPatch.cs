using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ButlerAddressPatch
{
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        ButlerAddressState? saved = shared.ButlerAddress;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !ButlerAddressFlow.IsRouteConsistent(saved, journey)) return null;
        ButlerAddress? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        ButlerAddressState? saved = shared.ButlerAddress;
        ButlerAddress? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool fromLacan = journey is not null
            && (journey.RouteId == "UNCONSCIOUS_AND_NORM"
                || journey.CompletedEdgeIds.Contains(ButlerAddressState.LacanRequiredEdgeId));
        string requiredEdge = fromLacan ? ButlerAddressState.LacanRequiredEdgeId : ButlerAddressState.RequiredEdgeId;
        string completionEdge = fromLacan ? ButlerAddressState.LacanCompletionEdgeId : ButlerAddressState.CompletionEdgeId;
        string sourceNode = fromLacan ? ButlerAddressState.LacanSourceNodeId : ButlerAddressState.SourceNodeId;
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == completionEdge);
        ButlerAddressTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, fromLacan
                ? NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                    ModelDb.GetId<LacanSignifier>(), "LACAN_SIGNIFIER")
                : NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                    ModelDb.GetId<BeauvoirOther>(), "BEAUVOIR_OTHER"),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            sourceNode, journey?.CompletedEdgeIds.Contains(requiredEdge) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && ButlerAddressFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!ButlerAddressTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? ButlerAddressState.Create($"BUTLER_ADDRESS:{run.Rng.Seed}:3");
        if (saved is null) candidate.FromLacan = fromLacan;
        if (journey is null || !ButlerAddressFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.ButlerAddress = candidate;
        Log.Info("[STS2Philosophers] Presenting Butler's address question.");
        return canonical;
    }

    private static ButlerAddress? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<ButlerAddress>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] ButlerAddress is unavailable: {exception}");
            return null;
        }
    }
}
