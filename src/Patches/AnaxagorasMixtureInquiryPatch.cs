using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AnaxagorasMixtureInquiryPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        WesternJourneyState? journey = shared.WesternJourney;
        AnaxagorasMixtureInquiryState? saved = shared.AnaxagorasMixtureInquiry;
        // New questions occupy native Unknown event nodes; this path only resumes legacy records.
        if (saved is null) return;
        AnaxagorasMixtureInquiry? canonical = TryGetCanonicalEvent();
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        bool priorClosed = shared.DemocritusArrangement is { Closed: true } prior
            && journey is not null && DemocritusArrangementFlow.IsJourneyConsistent(prior, journey, graph)
            && !prior.ConfirmedActions.Contains(DemocritusArrangementAction.AdoptAtoms);
        AnaxagorasMixtureInquiryTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
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
        if (!AnaxagorasMixtureInquiryTriggerPolicy.ShouldInsert(context)) return;

        if (shared.AnaxagorasMixtureInquiry is not { } active || journey is null
            || !AnaxagorasMixtureInquiryFlow.IsJourneyConsistent(active, journey))
            throw new InvalidDataException("The Anaxagoras boundary conflicts with its route.");
        Log.Info("[STS2Philosophers] Presenting Anaxagoras mixture inquiry at the map boundary.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
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
