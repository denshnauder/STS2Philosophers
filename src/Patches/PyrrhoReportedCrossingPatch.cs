using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class PyrrhoReportedCrossingPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        WesternJourneyState? journey = shared.WesternJourney;
        SextusOpposedAppearancesState? sextus = shared.SextusOpposedAppearances;
        DescartesMethodicReconstructionState? descartes = shared.DescartesMethodicReconstruction;
        PyrrhoReportedCrossingState? saved = shared.PyrrhoReportedCrossing;
        // New questions occupy native Unknown event nodes; this path only resumes legacy records.
        if (saved is null) return;
        PyrrhoReportedCrossing? canonical = TryGetCanonicalEvent();
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        PyrrhoReportedCrossingTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false, RoomType: not RoomType.Boss },
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
        if (!PyrrhoReportedCrossingTriggerPolicy.ShouldInsert(context)) return;
        if (journey is null || sextus is null || descartes is null
            || !sextus.IsUsable() || !descartes.IsUsable()) return;

        if (shared.PyrrhoReportedCrossing is not { } active
            || !PyrrhoReportedCrossingFlow.IsRouteConsistent(active, journey, sextus, descartes))
            throw new InvalidDataException("The Pyrrho question conflicts with its saved knowledge route.");
        Log.Info("[STS2Philosophers] Presenting Pyrrho's conflicting testimony after combat.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
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
