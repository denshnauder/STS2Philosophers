using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class AugustinePromisePatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;

        WesternJourneyState? journey = shared.WesternJourney;
        AugustinePromiseState? saved = shared.AugustinePromise;
        AugustinePromise? canonical = TryGetCanonicalEvent();
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Fixed")
            .Any(edge => edge.EdgeId == AugustinePromiseState.CompletionEdgeId);
        AugustinePromiseTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false,
                RoomType: not RoomType.Boss },
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(AugustinePromiseState.RequiredEdgeId) == true,
            edgeEligible,
            player?.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && AugustinePromiseFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!AugustinePromiseTriggerPolicy.ShouldInsert(context)) return;

        if (saved is null)
            shared.AugustinePromise = AugustinePromiseState.Create(
                $"AUGUSTINE_PROMISE:{run.Rng.Seed}:2");

        Log.Info("[STS2Philosophers] Presenting Augustine's act-two promise inquiry.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static AugustinePromise? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<AugustinePromise>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] AugustinePromise is unavailable: {exception}");
            return null;
        }
    }
}
