using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AnaxagorasMixtureInquiryPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    // Resume an allocated record at a real node; never create or reserve another instance.
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is not HeraclitusLyre { CanOfferNativeQuestion: true } carrier
            || shared.CurrentDoctrine?.DoctrineId != carrier.Id.Entry) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        AnaxagorasMixtureInquiryState? saved = shared.AnaxagorasMixtureInquiry;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        AnaxagorasMixtureInquiry? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        bool priorClosed = shared.DemocritusArrangement is { Closed: true } prior
            && journey is not null && DemocritusArrangementFlow.IsJourneyConsistent(prior, journey, graph)
            && !prior.ConfirmedActions.Contains(DemocritusArrangementAction.AdoptAtoms);
        AnaxagorasMixtureInquiryTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(AnaxagorasMixtureInquiryState.PrerequisiteEdgeId) == true,
            journey?.DeclinedFixedActs.Contains(2) == true,
            priorClosed,
            journey is not null && !journey.ResolvedQuestionActs.Contains(2),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!AnaxagorasMixtureInquiryTriggerPolicy.ShouldInsert(context)) return null;

        if (shared.AnaxagorasMixtureInquiry is not { } active || journey is null
            || !AnaxagorasMixtureInquiryFlow.IsJourneyConsistent(active, journey))
            return null;
        return canonical;
    }

    private static AnaxagorasMixtureInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AnaxagorasMixtureInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AnaxagorasMixtureInquiry is unavailable: {exception}");
            return null;
        }
    }
}
