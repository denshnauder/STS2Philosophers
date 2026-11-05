using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class LockeCargoIdentityPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!FixedEntryScope.Active || !NativeQuestionNodeEvents.IsEntry(run)
            || run.Players.Count != 1 || run.CurrentActIndex != 1
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || journey.RetainedEnding || journey.DeclinedFixedActs.Contains(2)) return null;
        if (!run.Players[0].Relics.OfType<DescartesThinkingLens>()
            .Any(relic => relic.CanOfferNativeQuestion)) return null;
        var saved = shared.LockeCargoIdentity;
        var canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)
            || saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;
        if (saved is not null)
            return saved.ActIndex == 2 && LockeCargoIdentityFlow.IsRouteConsistent(saved, journey) ? canonical : null;
        if (journey.CurrentNodeId != LockeCargoIdentityState.SourceNodeId
            || !WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed")
                .Any(edge => edge.EdgeId == LockeCargoIdentityState.SecondActEdgeId)) return null;
        var candidate = LockeCargoIdentityState.CreateAt($"LOCKE_CARGO_IDENTITY:{run.Rng.Seed}:2", 2);
        if (!LockeCargoIdentityFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.LockeCargoIdentity = candidate;
        return canonical;
    }

    private static LockeCargoIdentity? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<LockeCargoIdentity>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] LockeCargoIdentity is unavailable: {exception}");
            return null;
        }
    }
}
