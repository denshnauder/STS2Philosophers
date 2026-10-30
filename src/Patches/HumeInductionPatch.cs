using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class HumeInductionPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        HumeInductionState? saved = shared.HumeInduction;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !HumeInductionFlow.IsRouteConsistent(saved, journey)) return null;
        HumeInduction? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        HumeInductionState? saved = shared.HumeInduction;
        HumeInduction? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == HumeInductionState.CompletionEdgeId
                || edge.EdgeId == HumeInductionState.SecondActEdgeId);
        HumeInductionTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.HasCompletedCombatAfterAcrossActs(run,
                ModelDb.GetId<LockeCargoIdentity>(), "LOCKE_CARGO_IDENTITY"),
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(HumeInductionState.RequiredEdgeId) == true,
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && HumeInductionFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!HumeInductionTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? HumeInductionState.CreateAt($"HUME_INDUCTION:{run.Rng.Seed}:{run.CurrentActIndex + 1}", run.CurrentActIndex + 1);
        if (journey is null || !HumeInductionFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.HumeInduction = candidate;

        Log.Info("[STS2Philosophers] Presenting Hume's induction question.");
        return canonical;
    }

    private static HumeInduction? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<HumeInduction>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] HumeInduction is unavailable: {exception}");
            return null;
        }
    }
}
