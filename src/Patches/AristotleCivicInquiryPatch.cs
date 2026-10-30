using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AristotleCivicInquiryPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectNative(RunState run)
    {
        RunManager manager = RunManager.Instance;

        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return null;
        WesternJourneyState? journey = philosophy.WesternJourney;
        AristotleCivicInquiry? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        AristotleCivicInquiryState? saved = philosophy.AristotleCivicInquiry;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        AristotleCivicInquiryTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            player?.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null,
            journey?.CompletedEdgeIds.Contains(AugustinePromiseState.RequiredEdgeId) == true);
        if (!AristotleCivicInquiryTriggerPolicy.ShouldInsert(context)) return null;

        var candidate = saved ?? AristotleCivicInquiryState.Create(
            $"ARISTOTLE_CIVIC_INQUIRY:{run.Rng.Seed}:2");
        if (journey is null || !candidate.IsUsable()) return null;
        philosophy.AristotleCivicInquiry = candidate;

        Log.Info("[STS2Philosophers] Presenting Aristotle civic inquiry at a native question node.");
        return canonical;
    }

    private static AristotleCivicInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AristotleCivicInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AristotleCivicInquiry is unavailable: {exception}");
            return null;
        }
    }
}

