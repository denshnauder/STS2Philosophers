using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class MachiavelliRealistChallengePatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null) return null;

        MachiavelliRealistChallenge? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;

        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == MachiavelliRealistChallengeRoute.EdgeId);
        MachiavelliRealistChallengeTriggerContext context = new(
            RunManager.Instance.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey.CurrentNodeId,
            journey.LastFixedAct,
            journey.CompletedEdgeIds.Contains(MachiavelliRealistChallengeRoute.RequiredEdgeId),
            journey.ResolvedQuestionActs.Contains(MachiavelliRealistChallengeRoute.QuestionAct),
            edgeEligible,
            shared.AristotleCivicInquiry?.Closed == true,
            canonical is not null);
        if (!MachiavelliRealistChallengeTriggerPolicy.ShouldInsert(context)) return null;

        Log.Info("[STS2Philosophers] Presenting Machiavelli's institutional question after Aristotle.");
        return canonical;
    }

    private static MachiavelliRealistChallenge? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<MachiavelliRealistChallenge>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] MachiavelliRealistChallenge is unavailable: {exception}");
            return null;
        }
    }
}
