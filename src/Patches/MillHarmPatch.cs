using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class MillHarmPatch
{
    internal static Task InsertAtBoundaryAsync() => Task.CompletedTask;

    internal static EventModel? TrySelectExistingNative(RunState run)
    {
        bool fixedEntry = FixedEntryScope.Active;
        if ((!fixedEntry && !NativeQuestionNodeEvents.IsEntry(run))
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        MillHarmState? saved = shared.MillHarm;
        if (saved is null || saved.Closed || !saved.IsUsable()) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        if (journey is null || !MillHarmFlow.IsRouteConsistent(saved, journey)) return null;
        MillHarm? canonical = TryGetCanonicalEvent();
        return canonical is null || run.VisitedEventIds.Contains(canonical.Id) ? null : canonical;
    }

    internal static EventModel? TrySelectNative(RunState run)
    {
        bool fixedEntry = FixedEntryScope.Active;
        if ((!fixedEntry && !NativeQuestionNodeEvents.IsEntry(run)) || run.Players.Count != 1) return null;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return null;
        WesternJourneyState? journey = shared.WesternJourney;
        MillHarmState? saved = shared.MillHarm;
        MillHarm? canonical = TryGetCanonicalEvent();
        if (canonical is null || run.VisitedEventIds.Contains(canonical.Id)) return null;
        if (fixedEntry)
        {
            if (journey is null) return null;
            // The early first-act turn already stands on Smith or Bentham. It
            // reaches Mill in act two on 055/066, not through a second Smith or
            // Bentham room; Mill then hands to Rawls/Arendt in act three.
            bool earlyTurn = run.CurrentActIndex == 1 && journey.TurnedAct == 1
                && journey.LastFixedAct == 2
                && journey.CurrentNodeId is MillHarmState.SmithSourceNodeId or MillHarmState.BenthamSourceNodeId
                && ((journey.RouteId == "SYMPATHY_AND_PUBLIC_ACTION"
                        && journey.CompletedEdgeIds.Contains("WESTERN_EDGE_174"))
                    || (journey.RouteId == "CONSEQUENCE_AND_FREEDOM"
                        && journey.CompletedEdgeIds.Contains("WESTERN_EDGE_168")));
            if (earlyTurn)
            {
                string earlyEdge = journey.RouteId == "SYMPATHY_AND_PUBLIC_ACTION"
                    ? MillHarmState.EarlySmithSourceEdgeId : MillHarmState.EarlyBenthamSourceEdgeId;
                var earlyCandidate = saved is { FixedMode: true, ActIndex: 2 } ? saved : MillHarmState.CreateAt(
                    $"MILL_HARM:{run.Rng.Seed}:2", 2, earlyEdge, fixedMode: true,
                    "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE", journey.CurrentNodeId);
                if (!earlyCandidate.EdgeCommitted && !earlyCandidate.FixedDeclined && !earlyCandidate.Closed)
                {
                    earlyCandidate.ActIndex = 2;
                    earlyCandidate.EdgeId = earlyEdge;
                    earlyCandidate.TargetNodeId = "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE";
                    earlyCandidate.RoomSourceNodeId = journey.CurrentNodeId;
                    earlyCandidate.FixedMode = true;
                }
                if (!MillHarmFlow.IsRouteConsistent(earlyCandidate, journey)) return null;
                shared.MillHarm = earlyCandidate;
                Log.Info("[STS2Philosophers] Presenting Mill's harm room on the early fixed line.");
                return canonical;
            }
            if (run.CurrentActIndex != 2 || journey.LastFixedAct != 3) return null;
            bool early = journey.RouteId == "CONSEQUENCE_AND_FREEDOM"
                && journey.CurrentNodeId == "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE"
                && journey.CompletedEdgeIds.Contains("WESTERN_EDGE_169");
            // The sympathy line reaches Mill through Smith's 175; his third-act
            // room is the real Mill scene that hands over to Arendt on 176.
            early |= journey.RouteId == "SYMPATHY_AND_PUBLIC_ACTION"
                && journey.CurrentNodeId == "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE"
                && journey.CompletedEdgeIds.Contains("WESTERN_EDGE_175");
            bool source = journey.CurrentNodeId is MillHarmState.BenthamSourceNodeId
                or MillHarmState.SmithSourceNodeId;
            if (!early && !source) return null;
            WesternRouteGraph fixedGraph = WesternRouteGraph.LoadEmbedded();
            string fixedEdge;
            string targetNodeId;
            string roomSourceNodeId;
            if (early)
            {
                WesternGraphEdge[] outgoing = fixedGraph.GetEligibleEdges(journey, "Fixed")
                    .Where(edge => edge.Act == 3
                        && edge.FromNodeId == "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE")
                    .ToArray();
                WesternGraphEdge? forward = outgoing.FirstOrDefault(edge =>
                        edge.RouteIds?.Contains(journey.RouteId) == true)
                    ?? outgoing.FirstOrDefault();
                if (forward is null) return null;
                fixedEdge = forward.EdgeId;
                targetNodeId = forward.ToNodeId;
                roomSourceNodeId = "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE";
            }
            else
            {
                fixedEdge = fixedGraph.GetEligibleEdges(journey, "Fixed")
                    .Where(edge => edge.Act == 3
                        && edge.ToNodeId == "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE")
                    .Select(edge => edge.EdgeId).FirstOrDefault() ?? string.Empty;
                targetNodeId = "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE";
                roomSourceNodeId = journey.CurrentNodeId;
            }
            if (fixedEdge.Length == 0) return null;
            var fixedCandidate = saved is { FixedMode: true } ? saved : MillHarmState.CreateAt(
                $"MILL_HARM:{run.Rng.Seed}:3", 3, fixedEdge, fixedMode: true, targetNodeId, roomSourceNodeId);
            if (!fixedCandidate.EdgeCommitted && !fixedCandidate.FixedDeclined && !fixedCandidate.Closed)
            {
                fixedCandidate.ActIndex = 3;
                fixedCandidate.EdgeId = fixedEdge;
                fixedCandidate.TargetNodeId = targetNodeId;
                fixedCandidate.RoomSourceNodeId = roomSourceNodeId;
                fixedCandidate.FixedMode = true;
            }
            if (!MillHarmFlow.IsRouteConsistent(fixedCandidate, journey)) return null;
            shared.MillHarm = fixedCandidate;
            Log.Info("[STS2Philosophers] Presenting Mill's harm room on the fixed line.");
            return canonical;
        }
        ActBehaviorState? behavior = shared.ActBehaviorStates.GetValueOrDefault(run.CurrentActIndex);
        bool edgeEligible = journey is not null && WesternRouteGraph.LoadEmbedded()
            .GetEligibleEdges(journey, "Question")
            .Any(edge => edge.EdgeId == MillHarmState.CompletionEdgeId);
        MillHarmTriggerContext context = new(
            RunManager.Instance.IsInProgress, NativeQuestionNodeEvents.IsEntry(run), run.Players.Count == 1,
            run.CurrentActIndex, NativeQuestionNodeEvents.HasCompletedCombatAfter(run,
                ModelDb.GetId<WollstonecraftAdmission>(), "WOLLSTONECRAFT_ADMISSION"),
            NativeQuestionNodeEvents.CombatCount(behavior), journey?.CurrentNodeId, journey?.LastFixedAct ?? 0,
            journey?.CompletedEdgeIds.Contains(MillHarmState.RequiredEdgeId) == true, edgeEligible,
            saved is not null, saved?.IsUsable() == true, saved?.Closed == true,
            saved is not null && journey is not null && MillHarmFlow.IsRouteConsistent(saved, journey),
            canonical is not null);
        if (!MillHarmTriggerPolicy.ShouldInsert(context)) return null;
        var candidate = saved ?? MillHarmState.Create($"MILL_HARM:{run.Rng.Seed}:2");
        if (journey is null || !MillHarmFlow.IsRouteConsistent(candidate, journey)) return null;
        shared.MillHarm = candidate;
        Log.Info("[STS2Philosophers] Presenting Mill's harm question.");
        return canonical;
    }

    private static MillHarm? TryGetCanonicalEvent()
    {
        try { return ModelDb.Event<MillHarm>(); }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] MillHarm is unavailable: {exception}");
            return null;
        }
    }
}
