using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ZenoCivicChallengePatch
{
    internal static async Task InsertAtBoundaryAsync()
    {
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || !RouteEventMapBoundary.IsSafe(run)) return;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;

        WesternJourneyState? journey = shared.WesternJourney;
        ZenoCivicChallengeState? saved = shared.ZenoCivicChallenge;
        // New questions occupy native Unknown event nodes; this path only resumes legacy records.
        if (saved is null) return;
        ZenoCivicChallenge? canonical = TryGetCanonicalEvent();
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        var player = run.Players.Count == 1 ? run.Players[0] : null;
        bool eligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == ZenoCivicChallengeState.EdgeId);
        ZenoCivicChallengeTriggerContext context = new(
            manager.IsInProgress,
            RouteEventMapBoundary.IsSafe(run),
            run.Players.Count == 1,
            run.CurrentActIndex,
            run.CurrentRoom is CombatRoom { IsPreFinished: true, IsVictoryRoom: false,
                RoomType: not RoomType.Boss },
            behavior?.CompletedCombatCount ?? 0,
            journey?.CurrentNodeId,
            journey?.LastFixedAct ?? 0,
            journey?.ResolvedQuestionActs.Contains(ZenoCivicChallengeState.QuestionAct) == true,
            eligible,
            player?.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) is not null,
            saved is not null,
            saved?.IsUsable() == true,
            saved?.Closed == true,
            saved is not null && journey is not null
                && ZenoCivicChallengeFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!ZenoCivicChallengeTriggerPolicy.ShouldInsert(context)) return;

        if (shared.ZenoCivicChallenge is not { } active || journey is null
            || !ZenoCivicChallengeFlow.IsRouteConsistent(active, journey))
            throw new InvalidDataException("The civic challenge conflicts with its reserved question.");

        Log.Info("[STS2Philosophers] Presenting Zeno's first-act civic challenge.");
        await RouteEventMapBoundary.EnterAsync(manager, run, canonical!);
    }

    private static ZenoCivicChallenge? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<ZenoCivicChallenge>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] ZenoCivicChallenge is unavailable: {exception}");
            return null;
        }
    }
}
