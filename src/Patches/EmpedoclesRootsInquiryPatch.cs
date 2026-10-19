using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class EmpedoclesRootsInquiryPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        WesternJourneyState? journey = shared.WesternJourney;
        EmpedoclesRootsInquiryState? saved = shared.EmpedoclesRootsInquiry;
        EmpedoclesRootsInquiry? canonical = TryGetCanonicalEvent();
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        bool priorClosed = shared.DemocritusArrangement is { Closed: true } prior
            && journey is not null && DemocritusArrangementFlow.IsJourneyConsistent(prior, journey, graph)
            && prior.ConfirmedActions.Contains(DemocritusArrangementAction.AdoptAtoms);
        EmpedoclesRootsInquiryTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
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
        if (!EmpedoclesRootsInquiryTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
        {
            if (journey is null || !graph.TryReserveQuestion(journey, 2)) return;
            shared.EmpedoclesRootsInquiry = EmpedoclesRootsInquiryState.Create(
                $"EMPEDOCLES_ROOTS_INQUIRY:{run.Rng.Seed}:2");
        }
        if (shared.EmpedoclesRootsInquiry is not { } active || journey is null
            || !EmpedoclesRootsInquiryFlow.IsJourneyConsistent(active, journey, graph))
            throw new InvalidDataException("The Empedocles boundary conflicts with its route.");
        Log.Info("[STS2Philosophers] Presenting Empedocles roots inquiry at the map boundary.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
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
