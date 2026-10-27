using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class EmpedoclesRootsInquiryPatch
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
        EmpedoclesRootsInquiryState? saved = shared.EmpedoclesRootsInquiry;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        EmpedoclesRootsInquiry? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        bool priorClosed = shared.DemocritusArrangement is { Closed: true } prior
            && journey is not null && DemocritusArrangementFlow.IsJourneyConsistent(prior, journey, graph)
            && prior.ConfirmedActions.Contains(DemocritusArrangementAction.AdoptAtoms);
        EmpedoclesRootsInquiryTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(DemocritusArrangementState.EdgeId) == true,
            priorClosed,
            journey is not null && !journey.ResolvedQuestionActs.Contains(2),
            journey is not null && graph.GetEligibleEdges(journey, "Question")
                .Any(edge => edge.EdgeId == EmpedoclesRootsInquiryState.EdgeId),
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!EmpedoclesRootsInquiryTriggerPolicy.ShouldInsert(context)) return null;

        if (shared.EmpedoclesRootsInquiry is not { } active || journey is null
            || !EmpedoclesRootsInquiryFlow.IsJourneyConsistent(active, journey, graph))
            return null;
        return canonical;
    }

    private static EmpedoclesRootsInquiry? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<EmpedoclesRootsInquiry>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] EmpedoclesRootsInquiry is unavailable: {exception}");
            return null;
        }
    }
}
