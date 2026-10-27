using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class GorgiasSpeechInquiryPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    // Resume an allocated record at a real node; never create or reserve another instance.
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<AristotleCategoryTablet>()) is not AristotleCategoryTablet { CanOfferNativeQuestion: true } carrier
            || shared.CurrentDoctrine?.DoctrineId != carrier.Id.Entry) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        GorgiasSpeechInquiryState? saved = shared.GorgiasSpeechInquiry;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        GorgiasSpeechInquiry? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        GorgiasSpeechInquiryTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey is not null && !journey.ResolvedQuestionActs.Contains(1),
            player?.GetRelicById(ModelDb.GetId<AristotleCategoryTablet>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!GorgiasSpeechInquiryTriggerPolicy.ShouldInsert(context)) return null;

        if (shared.GorgiasSpeechInquiry is not { } active || journey is null
            || !GorgiasSpeechInquiryFlow.IsJourneyConsistent(active, journey))
            return null;
        return canonical;
    }

    private static GorgiasSpeechInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<GorgiasSpeechInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] GorgiasSpeechInquiry is unavailable: {exception}");
            return null;
        }
    }
}
