using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class LockeCargoIdentityPatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        LockeCargoIdentityState? saved = shared.LockeCargoIdentity;
        LockeCargoIdentity? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == LockeCargoIdentityState.EdgeId);
        LockeCargoIdentityTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombat(run),
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && LockeCargoIdentityFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!LockeCargoIdentityTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? LockeCargoIdentityState.Create($"LOCKE_CARGO_IDENTITY:{run.Rng.Seed}:1");
        if (journey is null || !LockeCargoIdentityFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.LockeCargoIdentity = candidate;

        Log.Info("[STS2Philosophers] Presenting Locke's cargo-identity question.");
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
