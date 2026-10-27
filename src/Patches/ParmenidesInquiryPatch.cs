using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ParmenidesInquiryPatch
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
        ParmenidesInquiry? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        ParmenidesInquiryState? saved = philosophy.ParmenidesInquiry;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        if (journey is null || journey.RetainedEnding || !journey.ResolvedQuestionActs.Contains(1)) return null;
        bool adopted = saved.ConfirmedActions.Contains(ParmenidesInquiryAction.AdoptBoundary);
        bool adoptedRoute = journey.CurrentNodeId == ParmenidesInquiryState.AdoptedNodeId
            && journey.CompletedEdgeIds.Contains(ParmenidesInquiryState.EdgeId);
        if (adopted ? !adoptedRoute : journey.CurrentNodeId != ParmenidesInquiryState.RequiredNodeId
            && !(saved.PendingAction == ParmenidesInquiryAction.AdoptBoundary && adoptedRoute)) return null;
        ActBehaviorState? behavior = philosophy.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        ParmenidesInquiryTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            player?.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!ParmenidesInquiryTriggerPolicy.ShouldInsert(context)) return null;


        return canonical;
    }

    private static ParmenidesInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<ParmenidesInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] ParmenidesInquiry is unavailable: {exception}");
            return null;
        }
    }
}
