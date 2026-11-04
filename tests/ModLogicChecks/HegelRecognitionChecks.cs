using STS2Philosophers;

internal static class HegelRecognitionChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        RouteId = "AUTONOMY_AND_HISTORY",
        CurrentNodeId = HegelRecognitionState.SourceNodeId,
        LastFixedAct = 3,
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = HegelRecognitionState.CreateAt("HEGEL_RECOGNITION:TEST:3", 3,
            HegelRecognitionState.DefaultEdgeId);
        Check(HegelRecognitionFlow.Options(state.Stage).Count == 3,
            "Hegel must offer renegotiation, keeping the arrangement, or abandoning the order.");
        Check(HegelRecognitionFlow.TryPrepare(state, HegelRecognitionAction.Renegotiate),
            "Renegotiating the terms must be selectable.");
        var journey = Journey();
        Check(HegelRecognitionFlow.IsRouteConsistent(state, journey),
            "The default history room must sit on the third-act fixed slot.");
        Check(HegelRecognitionFlow.TryCommitPending(state, journey, graph),
            "Renegotiating must take the third-act fixed link.");
        Check(state.Standard == HegelRecognitionStandard.Recognition && state.EdgeCommitted
            && state.Stage == HegelRecognitionStage.ResultRecognition,
            "Recognition must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(HegelRecognitionState.DefaultEdgeId)
            && journey.CurrentNodeId == HegelRecognitionState.AdoptedNodeId
            && journey.LastFixedAct == 3,
            "Adopting must move to Hegel's history node and keep the capped third slot.");

        var kept = HegelRecognitionState.CreateAt("HEGEL_RECOGNITION:TEST:4", 3,
            HegelRecognitionState.DefaultEdgeId);
        Check(HegelRecognitionFlow.TryPrepare(kept, HegelRecognitionAction.KeepArrangement),
            "Keeping the arrangement must be selectable.");
        var keptJourney = Journey();
        Check(HegelRecognitionFlow.TryCommitPending(kept, keptJourney, graph),
            "Keeping the arrangement must still take the fixed link.");
        Check(kept.Standard == HegelRecognitionStandard.Instrumental && kept.EdgeCommitted
            && keptJourney.CurrentNodeId == HegelRecognitionState.AdoptedNodeId,
            "The instrumental standard must be recorded without claiming recognition.");

        var abandoned = HegelRecognitionState.CreateAt("HEGEL_RECOGNITION:TEST:5", 3,
            HegelRecognitionState.DefaultEdgeId);
        Check(HegelRecognitionFlow.TryPrepare(abandoned, HegelRecognitionAction.AbandonOrder),
            "Abandoning the order must be selectable.");
        var declined = Journey();
        Check(HegelRecognitionFlow.TryCommitPending(abandoned, declined, graph),
            "Abandoning must decline the act's fixed slot.");
        Check(abandoned.Standard == HegelRecognitionStandard.Exit && abandoned.FixedDeclined
            && declined.DeclinedFixedActs.Contains(3)
            && declined.CurrentNodeId == HegelRecognitionState.SourceNodeId
            && declined.LastFixedAct == 3,
            "Declining must leave Kant in place and record the refusal.");

        // The early Kant turn places the same history room in the second act.
        var early = HegelRecognitionState.CreateAt("HEGEL_RECOGNITION:TEST:2", 2,
            HegelRecognitionState.EarlyEdgeId);
        var earlyJourney = new WesternJourneyState
        {
            RouteId = "AUTONOMY_AND_HISTORY",
            CurrentNodeId = HegelRecognitionState.SourceNodeId,
            LastFixedAct = 2,
        };
        Check(HegelRecognitionFlow.IsRouteConsistent(early, earlyJourney),
            "The early history room must sit on the second-act fixed slot.");
        Check(HegelRecognitionFlow.TryPrepare(early, HegelRecognitionAction.Renegotiate)
            && HegelRecognitionFlow.TryCommitPending(early, earlyJourney, graph),
            "The early history choice must take edge 154.");
        Check(earlyJourney.CompletedEdgeIds.Contains(HegelRecognitionState.EarlyEdgeId)
            && earlyJourney.CurrentNodeId == HegelRecognitionState.AdoptedNodeId
            && earlyJourney.LastFixedAct == 3,
            "The early adoption must move to Hegel's history node.");

        Check(HegelRecognitionTriggerPolicy.ShouldInsert(new(true, true, true, 2, 3,
            HegelRecognitionState.SourceNodeId, 3, true, 1, true, false, false, false, false, true)),
            "The default line offers Hegel at the third-act boundary.");
        Check(!HegelRecognitionTriggerPolicy.ShouldInsert(new(true, true, true, 1, 3,
            HegelRecognitionState.SourceNodeId, 3, true, 1, true, false, false, false, false, true)),
            "The third-act record is not offered in the second act.");
        Check(!HegelRecognitionTriggerPolicy.ShouldInsert(new(true, true, true, 2, 3,
            HegelRecognitionState.SourceNodeId, 3, true, 1, false, false, false, false, false, true)),
            "The third-act record needs edge 096 to be eligible.");
        Check(HegelRecognitionTriggerPolicy.ShouldInsert(new(true, true, true, 1, 2,
            HegelRecognitionState.SourceNodeId, 2, true, 1, true, false, false, false, false, true)),
            "The early Kant turn offers the second-act history room.");
        Check(!HegelRecognitionTriggerPolicy.ShouldInsert(new(true, true, true, 2, 3,
            HegelRecognitionState.SourceNodeId, 2, true, 1, true, false, false, false, false, true)),
            "The third-act room needs the act-three fixed slot.");

        Check(!HegelRecognitionState.CreateAt("HEGEL_RECOGNITION:TEST:6", 3,
                HegelRecognitionState.EarlyEdgeId).IsUsable(),
            "A third-act record cannot use the early edge.");
        Check(!HegelRecognitionState.CreateAt("HEGEL_RECOGNITION:TEST:7", 1,
                HegelRecognitionState.DefaultEdgeId).IsUsable(),
            "An act outside two or three is rejected.");

        var tampered = HegelRecognitionState.CreateAt("HEGEL_RECOGNITION:TEST:8", 3,
            HegelRecognitionState.DefaultEdgeId);
        tampered.Standard = HegelRecognitionStandard.Instrumental;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
