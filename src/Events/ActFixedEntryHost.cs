using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// Reviewed release handovers. Each line keeps the installed completed-slot
// counter and only allocates its own saved person at the Ancient boundary.
internal static class ActFixedEntryHost
{
    internal static bool TryPrepare(RunState run, out EventRoom? room)
    {
        room = null;
        if (run.CurrentActIndex is not (1 or 2)) return false;
        using (FixedEntryScope.Begin())
        {
            EventModel? model = run.CurrentActIndex == 1
                ? LockeCargoIdentityPatch.TrySelectNative(run)
                : HumeInductionPatch.TrySelectNative(run);
            model ??= TrySelectSocraticLine(run);
            if (model is null) return false;
            room = new EventRoom(model);
            return true;
        }
    }

    private static EventModel? TrySelectSocraticLine(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1
            || !run.Players[0].Relics.OfType<SocratesBalanceWeight>().Any(relic => relic.CanOfferNativeQuestion)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey) return null;
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        if (run.CurrentActIndex == 1)
        {
            if (journey.CurrentNodeId != SchopenhauerWillDenialState.FixedSourceNodeId
                || journey.LastFixedAct != 1) return null;
            SchopenhauerWillDenialState? saved = shared.SchopenhauerWillDenial;
            if (saved is { Closed: true }) return null;
            var candidate = saved ?? SchopenhauerWillDenialState.CreateAt(
                $"SCHOPENHAUER_WILL_DENIAL:{run.Rng.Seed}:2", string.Empty, 2, true);
            if (!candidate.FixedMode || candidate.ActIndex != 2
                || candidate.InstanceId != $"SCHOPENHAUER_WILL_DENIAL:{run.Rng.Seed}:2"
                || !SchopenhauerWillDenialFlow.IsRouteConsistent(candidate, journey, shared.KantApperception)
                || !graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == SchopenhauerWillDenialState.FixedCompletionEdgeId)) return null;
            EventModel canonical = ModelDb.Event<SchopenhauerWillDenial>();
            if (run.VisitedEventIds.Contains(canonical.Id)) return null;
            shared.SchopenhauerWillDenial = candidate;
            return canonical;
        }
        if (journey.CurrentNodeId != "ARTHUR_SCHOPENHAUER__VIRTUE_AND_HAPPINESS__CORE"
            || journey.LastFixedAct != 2 || shared.SchopenhauerWillDenial is not { Closed: true, FixedMode: true }
            || !journey.CompletedEdgeIds.Contains(SchopenhauerWillDenialState.FixedCompletionEdgeId)) return null;
        NietzscheRevaluationState? existing = shared.NietzscheRevaluation;
        if (existing is { Closed: true }) return null;
        var next = existing ?? NietzscheRevaluationState.CreateAt(
            $"NIETZSCHE_REVALUATION:{run.Rng.Seed}:3", 3, "WESTERN_EDGE_151");
        if (existing is null)
        {
            next.RoutePlaced = true;
            next.RouteSourceNodeId = journey.CurrentNodeId;
        }
        if (next.ActIndex != 3 || next.EdgeId != "WESTERN_EDGE_151"
            || next.InstanceId != $"NIETZSCHE_REVALUATION:{run.Rng.Seed}:3"
            || !NietzscheRevaluationFlow.IsRouteConsistent(next, journey)
            || !graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_151")) return null;
        EventModel selected = ModelDb.Event<NietzscheRevaluation>();
        if (run.VisitedEventIds.Contains(selected.Id)) return null;
        shared.NietzscheRevaluation = next;
        return selected;
    }
}
