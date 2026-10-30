using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AnaximanderOriginInquiryPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    // Resume an allocated record at a real node; never create or reserve another instance.
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        RunManager manager = RunManager.Instance;

        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;

        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? philosophy) || philosophy is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not HeraclitusLyre { CanOfferNativeQuestion: true } carrier
            || philosophy.CurrentDoctrine?.DoctrineId != carrier.Id.Entry) return null;
        WesternJourneyState? journey = philosophy.WesternJourney;
        AnaximanderOriginInquiry? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        AnaximanderOriginInquiryState? saved = philosophy.AnaximanderOriginInquiry;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        if (journey is null || journey.RetainedEnding || !journey.DeclinedFixedActs.Contains(2)
            || !journey.ResolvedQuestionActs.Contains(1) || !journey.ResolvedQuestionActs.Contains(2)) return null;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        AnaximanderOriginInquiryTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            NativeQuestionNodeEvents.CombatCount(behavior),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            philosophy.ParmenidesInquiry is { Closed: true } prior && prior.IsUsable(),
            player?.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not null,
            journey is not null && !journey.DeclinedFixedActs.Contains(2),
            journey is not null && graph.GetEligibleEdges(journey, "Fixed").Count == 0,
            journey is not null && !journey.ResolvedQuestionActs.Contains(2),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!AnaximanderOriginInquiryTriggerPolicy.ShouldInsert(context)) return null;


        return canonical;
    }

    private static AnaximanderOriginInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AnaximanderOriginInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AnaximanderOriginInquiry is unavailable: {exception}");
            return null;
        }
    }
}
