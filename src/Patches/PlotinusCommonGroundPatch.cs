using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class PlotinusCommonGroundPatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;

        WesternJourneyState? journey = shared.WesternJourney;
        PlotinusCommonGroundState? saved = shared.PlotinusCommonGround;
        PlotinusCommonGround? canonical = TryGetCanonicalEvent();
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == PlotinusCommonGroundState.CompletionEdgeId);
        PlotinusCommonGroundTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false,
                RoomType: not RoomType.Boss },
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(PlotinusCommonGroundState.RequiredEdgeId) == true,
            edgeEligible,
            LaterActRelicEligibilityRuntime.IsEligible(player, LaterActCandidateIds.Plotinus,
                run.CurrentActIndex),
            shared.EpicurusSharedMargin?.Closed == true,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && PlotinusCommonGroundFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!PlotinusCommonGroundTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
            shared.PlotinusCommonGround = PlotinusCommonGroundState.Create(
                $"PLOTINUS_COMMON_GROUND:{run.Rng.Seed}:3");

        Log.Info("[STS2Philosophers] Presenting Plotinus's act-three common ground.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static PlotinusCommonGround? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<PlotinusCommonGround>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] PlotinusCommonGround is unavailable: {exception}");
            return null;
        }
    }
}
