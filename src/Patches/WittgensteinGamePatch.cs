using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class WittgensteinGamePatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        bool fixedEntry = FixedEntryScope.Active;
        if ((!fixedEntry && !NativeQuestionNodeEvents.IsEntry(run))
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WittgensteinGameState? saved = shared.WittgensteinGame;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !WittgensteinGameFlow.IsRouteConsistent(saved, journey)) return null;
        WittgensteinGame? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        bool fixedEntry = FixedEntryScope.Active;
        if ((!fixedEntry && !NativeQuestionNodeEvents.IsEntry(run)) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        WittgensteinGameState? saved = shared.WittgensteinGame;
        WittgensteinGame? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        if (fixedEntry)
        {
            if (journey is null || run.CurrentActIndex is not (1 or 2)) return null;
            int fixedAct = run.CurrentActIndex + 1;
            WesternRouteGraph fixedGraph = WesternRouteGraph.LoadEmbedded();
            string fixedEdge;
            string fixedSource;
            if (fixedAct == 2)
            {
                fixedEdge = WittgensteinGameState.EarlyCompletionEdgeId;
                fixedSource = WittgensteinGameState.EarlySourceNodeId;
                if (!fixedGraph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == fixedEdge)) return null;
            }
            else
            {
                WesternGraphEdge? lateEdge = fixedGraph.GetEligibleEdges(journey, "Fixed")
                    .FirstOrDefault(edge => edge.Act == 3
                        && edge.ToNodeId == WittgensteinGameState.AdoptedNodeId);
                fixedEdge = lateEdge?.EdgeId ?? string.Empty;
                fixedSource = lateEdge?.FromNodeId
                    ?? (journey.RouteId == "ORDINARY_SPEECH"
                        && journey.CompletedEdgeIds.Contains(WittgensteinGameState.EarlyCompletionEdgeId)
                        ? WittgensteinGameState.AdoptedNodeId : string.Empty);
                if (fixedSource.Length == 0) return null;
            }
            if (journey.CurrentNodeId != fixedSource || journey.LastFixedAct != fixedAct) return null;
            if (saved is { Closed: true } || saved is not null && !saved.IsUsable()) return null;
            var fixedCandidate = saved is { FixedMode: true } && saved.ActIndex == fixedAct && saved.EdgeId == fixedEdge
                ? saved
                : WittgensteinGameState.CreateAt(
                    $"WITTGENSTEIN_GAME:{run.Rng.Seed}:{fixedAct}", fixedAct, fixedEdge, fixedMode: true);
            fixedCandidate.FixedSourceNodeId = fixedSource;
            if (!WittgensteinGameFlow.IsRouteConsistent(fixedCandidate, journey)) return null;
            shared.WittgensteinGame = fixedCandidate;
            Log.Info(fixedAct == 2
                ? "[STS2Philosophers] Presenting the early Wittgenstein room on the fixed line."
                : "[STS2Philosophers] Presenting the late Wittgenstein room on the fixed line.");
            return canonical;
        }
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == WittgensteinGameState.CompletionEdgeId);
        WittgensteinGameTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombat(run, normalOnly: true),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(WittgensteinGameState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && WittgensteinGameFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!WittgensteinGameTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? WittgensteinGameState.Create($"WITTGENSTEIN_GAME:{run.Rng.Seed}:3");
        if (journey is null || !WittgensteinGameFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.WittgensteinGame = candidate;
        Log.Info("[STS2Philosophers] Presenting Wittgenstein's language-game question.");
        return canonical;
    }

    private static WittgensteinGame? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<WittgensteinGame>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] WittgensteinGame is unavailable: {exception}");
            return null;
        }
    }
}
