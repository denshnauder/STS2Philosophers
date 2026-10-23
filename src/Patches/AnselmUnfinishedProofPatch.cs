using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AnselmUnfinishedProofPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
        WesternJourneyState? journey = shared.WesternJourney;
        PyrrhoReportedCrossingState? pyrrho = shared.PyrrhoReportedCrossing;
        AnselmUnfinishedProofState? saved = shared.AnselmUnfinishedProof;
        // New questions occupy native Unknown event nodes; this path only resumes legacy records.
        if (saved is null) return;
        AnselmUnfinishedProof? canonical = TryGetCanonicalEvent();
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        bool pyrrhoConsistent = journey is not null && pyrrho is not null
            && shared.SextusOpposedAppearances is { } sextus
            && shared.DescartesMethodicReconstruction is { } descartes
            && PyrrhoReportedCrossingFlow.IsRouteConsistent(pyrrho, journey, sextus, descartes);
        AnselmUnfinishedProofTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false,
                RoomType: not RoomType.Boss },
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(1) == true,
            journey?.DeclinedFixedActs.Contains(2) == true,
            journey?.ResolvedQuestionActs.Contains(2) == true,
            journey?.LastFixedAct == 3,
            journey?.ResolvedQuestionActs.Contains(3) == true,
            player?.GetRelicById(ModelDb.GetId<SocratesQuestionCup>()) is not null,
            pyrrho?.Closed == true,
            pyrrhoConsistent,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null && pyrrho is not null
                && AnselmUnfinishedProofFlow.IsRouteConsistent(saved, journey, pyrrho),
            canonical is not null);
        if (!AnselmUnfinishedProofTriggerPolicy.ShouldInsert(context)) return;


        Log.Info("[STS2Philosophers] Presenting Anselm's third-act textual inquiry.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static AnselmUnfinishedProof? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AnselmUnfinishedProof>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AnselmUnfinishedProof is unavailable: {exception}");
            return null;
        }
    }
}
