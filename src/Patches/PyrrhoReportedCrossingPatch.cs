using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class PyrrhoReportedCrossingPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    // Resume an allocated record at a real node; never create or reserve another instance.
    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        RunManager manager = RunManager.Instance;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is not SocratesQuestionCup { CanOfferNativeQuestion: true } carrier
            || shared.CurrentDoctrine?.DoctrineId != carrier.Id.Entry) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        SextusOpposedAppearancesState? sextus = shared.SextusOpposedAppearances;
        DescartesMethodicReconstructionState? descartes = shared.DescartesMethodicReconstruction;
        PyrrhoReportedCrossingState? saved = shared.PyrrhoReportedCrossing;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        PyrrhoReportedCrossing? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        PyrrhoReportedCrossingTriggerContext context = new(
            manager.IsInProgress,
            NativeQuestionNodeEvents.IsEntry(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            // The allocated encounter already owns its opportunity. Restoring it
            // at a physical question node never demands another combat receipt.
            NativeQuestionNodeEvents.IsEntry(run),
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            journey?.ResolvedQuestionActs.Contains(2) == true,
            journey?.DeclinedFixedActs.Contains(2) == true,
            player?.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is not null,
            sextus?.Closed == true,
            descartes is { Closed: true, Stage: DescartesReconstructionStage.ResultDeclined },
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            canonical is not null);
        if (!PyrrhoReportedCrossingTriggerPolicy.ShouldInsert(context)) return null;
        if (journey is null || sextus is null || descartes is null
            || !sextus.IsUsable() || !descartes.IsUsable()) return null;

        if (shared.PyrrhoReportedCrossing is not { } active
            || !PyrrhoReportedCrossingFlow.IsRouteConsistent(active, journey, sextus, descartes))
            return null;
        return canonical;
    }

    private static PyrrhoReportedCrossing? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<PyrrhoReportedCrossing>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] PyrrhoReportedCrossing is unavailable: {exception}");
            return null;
        }
    }
}
