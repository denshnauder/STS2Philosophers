using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class FichteSelfPositingPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        FichteSelfPositingState? saved = shared.FichteSelfPositing;
        if (saved is null || saved.Closed || !saved.IsUsable()
            || saved.ActIndex - 1 != run.CurrentActIndex) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !FichteSelfPositingFlow.IsRouteConsistent(saved, journey)) return null;
        FichteSelfPositing? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        FichteSelfPositingState? saved = shared.FichteSelfPositing;
        FichteSelfPositing? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        int act = run.CurrentActIndex == 1 ? 2 : 3;
        if (saved is not null && saved.ActIndex != act) return null;
        bool actTwo = act == 2;
        KantApperceptionState? kant = shared.KantApperception;
        bool actTwoKant = actTwo && kant is { ActIndex: 2, EdgeCommitted: true }
            && journey is not null
            && journey.CompletedEdgeIds.Contains(kant.EdgeId)
            && journey.CurrentNodeId == KantApperceptionState.AdoptedNodeId;
        bool actThreeKant = !actTwo && journey is not null
            && journey.CompletedEdgeIds.Contains(FichteSelfPositingState.ActThreeRequiredEdgeId)
            && journey.CurrentNodeId == FichteSelfPositingState.SourceNodeId;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = actTwo
            ? journey is not null && !journey.ResolvedQuestionActs.Contains(2)
                && journey.LastFixedAct >= 2
            : actThreeKant
                ? !journey!.ResolvedQuestionActs.Contains(3)
                : journey is not null && WesternRouteGraph.LoadEmbedded()
                    .GetEligibleEdges(journey, "Question")
                    .Any(edge => edge.EdgeId == FichteSelfPositingState.CompletionEdgeId);
        FichteSelfPositingTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            actTwo
                ? NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true)
                : NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                    ModelDb.GetId<KantUniversalization>(), "KANT_UNIVERSALIZATION"),
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            actTwo ? actTwoKant
                : actThreeKant || journey?.CompletedEdgeIds.Contains(FichteSelfPositingState.RequiredEdgeId) == true,
            edgeEligible,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && FichteSelfPositingFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!FichteSelfPositingTriggerPolicy.ShouldInsert(context)) return null;

        FichteSelfPositingState candidate = saved ?? (actTwo
            ? FichteSelfPositingState.CreateAt(
                $"FICHTE_SELF_POSITING:{run.Rng.Seed}:2", 2, kant!.EdgeId)
            : actThreeKant
                ? FichteSelfPositingState.CreateAt(
                    $"FICHTE_SELF_POSITING:{run.Rng.Seed}:3", 3, FichteSelfPositingState.ActThreeRequiredEdgeId)
                : FichteSelfPositingState.Create($"FICHTE_SELF_POSITING:{run.Rng.Seed}:3"));
        if (journey is null || !FichteSelfPositingFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.FichteSelfPositing = candidate;

        Log.Info("[STS2Philosophers] Presenting Fichte's self-positing question.");
        return canonical;
    }

    private static FichteSelfPositing? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<FichteSelfPositing>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] FichteSelfPositing is unavailable: {exception}");
            return null;
        }
    }
}
