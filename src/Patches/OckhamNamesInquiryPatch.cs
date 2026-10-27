using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class OckhamNamesInquiryPatch
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
        GorgiasSpeechInquiryState? gorgias = shared.GorgiasSpeechInquiry;
        OckhamNamesInquiryState? saved = shared.OckhamNamesInquiry;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        OckhamNamesInquiry? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool gorgiasConsistent = journey is not null && gorgias is not null
            && GorgiasSpeechInquiryFlow.IsJourneyConsistent(gorgias, journey);
        OckhamNamesInquiryTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.IsEntry(run),
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            journey?.ResolvedQuestionActs.Contains(2) == true,
            player?.GetRelicById(ModelDb.GetId<AristotleCategoryTablet>()) is AristotleCategoryTablet { CanOfferOckham: true },
            gorgias?.Closed == true,
            gorgiasConsistent,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && gorgias is not null
                && OckhamNamesInquiryFlow.IsRouteConsistent(saved, journey, gorgias),
            canonical is not null);
        if (!OckhamNamesInquiryTriggerPolicy.ShouldInsert(context)) return null;


        return canonical;
    }

    private static OckhamNamesInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<OckhamNamesInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] OckhamNamesInquiry is unavailable: {exception}");
            return null;
        }
    }
}
