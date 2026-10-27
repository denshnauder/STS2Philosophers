using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ZenoCivicChallengePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    // Resume an allocated record at a real node; never create or reserve another instance.
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) is not PlatoCivicSeal { CanOfferNativeQuestion: true } carrier
            || shared.CurrentDoctrine?.DoctrineId != carrier.Id.Entry) return null;

        WesternJourneyState? journey = shared.WesternJourney;
        ZenoCivicChallengeState? saved = shared.ZenoCivicChallenge;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        ZenoCivicChallenge? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool eligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == ZenoCivicChallengeState.EdgeId);
        ZenoCivicChallengeTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.IsEntry(run),
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(ZenoCivicChallengeState.QuestionAct) == true,
            eligible,
            player?.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && ZenoCivicChallengeFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!ZenoCivicChallengeTriggerPolicy.ShouldInsert(context)) return null;

        if (shared.ZenoCivicChallenge is not { } active || journey is null
            || !ZenoCivicChallengeFlow.IsRouteConsistent(active, journey))
            return null;

        return canonical;
    }

    private static ZenoCivicChallenge? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<ZenoCivicChallenge>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] ZenoCivicChallenge is unavailable: {exception}");
            return null;
        }
    }
}
