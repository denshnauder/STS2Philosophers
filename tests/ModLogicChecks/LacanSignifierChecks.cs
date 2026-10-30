using STS2Philosophers;

internal static class LacanSignifierChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = LacanSignifierState.Create("LACAN_SIGNIFIER:TEST:1");
        Check(LacanSignifierFlow.Options(state.Stage).Count == 3,
            "Lacan must first offer three speakers to bind the word to.");
        Check(LacanSignifierFlow.TryPrepare(state, LacanSignifierAction.BindCourier),
            "Binding the word to the courier must be selectable.");
        var journey = Journey(graph);
        Check(LacanSignifierFlow.TryCommitPending(state, journey, graph),
            "Binding a speaker must advance to the interruption without touching the route.");
        Check(state.Stage == LacanSignifierStage.Interrupted && state.Speaker == LacanSpeaker.Courier
            && !state.EdgeCommitted && journey.CurrentNodeId == LacanSignifierState.SourceNodeId,
            "Binding must record the speaker and leave the second-act slot open.");
        Check(LacanSignifierFlow.Options(state.Stage).Count == 3,
            "The interruption must offer clarify, claim now, or refuse the object.");

        var clarify = LacanSignifierState.Create("LACAN_SIGNIFIER:TEST:2");
        Bind(clarify, LacanSignifierAction.BindKeeper);
        var clarifyJourney = Journey(graph);
        LacanSignifierFlow.TryPrepare(clarify, LacanSignifierAction.Clarify);
        Check(LacanSignifierFlow.TryCommitPending(clarify, clarifyJourney, graph),
            "Clarifying must take the second-act fixed link.");
        Check(clarify.Standard == LacanSignifierStandard.Clarified && clarify.EdgeCommitted
            && clarify.Stage == LacanSignifierStage.ResultClarified
            && clarifyJourney.CompletedEdgeIds.Contains(LacanSignifierState.CompletionEdgeId)
            && clarifyJourney.CurrentNodeId == LacanSignifierState.AdoptedNodeId
            && clarifyJourney.LastFixedAct == 3,
            "Clarifying must move the journey to Lacan and consume act two.");

        // A route can put this room in the third act, where there is no act four
        // to move into: refusing has to close the slot the room stands in.
        var third = LacanSignifierState.CreateAt("LACAN_SIGNIFIER:TEST:3B", 3,
            LacanSignifierState.CompletionEdgeId);
        var thirdJourney = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        thirdJourney.RouteId = "UNCONSCIOUS_AND_NORM";
        thirdJourney.LastFixedAct = 1;
        Check(graph.TryApply(thirdJourney, LacanSignifierState.RequiredEdgeId),
            "The third-act case needs Freud's second-act edge.");
        Put(thirdJourney, 3);
        Check(LacanSignifierFlow.TryPrepare(third, LacanSignifierAction.BindCourier)
            && LacanSignifierFlow.TryCommitPending(third, thirdJourney, graph),
            "Binding a speaker must reach the interruption in the third act.");
        Check(LacanSignifierFlow.TryPrepare(third, LacanSignifierAction.RefuseObject)
            && LacanSignifierFlow.TryCommitPending(third, thirdJourney, graph),
            "Refusing the object must close the third act's slot.");
        Check(thirdJourney.DeclinedFixedActs.Contains(3) && thirdJourney.LastFixedAct == 3
            && thirdJourney.CurrentNodeId == LacanSignifierState.SourceNodeId,
            "Refusing in the third act must decline that act, not a fourth one.");

        var claim = LacanSignifierState.Create("LACAN_SIGNIFIER:TEST:3");
        Bind(claim, LacanSignifierAction.BindArchivist);
        var claimJourney = Journey(graph);
        LacanSignifierFlow.TryPrepare(claim, LacanSignifierAction.ClaimNow);
        Check(LacanSignifierFlow.TryCommitPending(claim, claimJourney, graph),
            "Claiming at once must also take the second-act fixed link.");
        Check(claim.Standard == LacanSignifierStandard.Claimed
            && claim.UnansweredRequests.SequenceEqual([LacanSpeaker.Archivist]),
            "Claiming at once must record the request that went unanswered.");

        var refuse = LacanSignifierState.Create("LACAN_SIGNIFIER:TEST:4");
        Bind(refuse, LacanSignifierAction.BindKeeper);
        var refuseJourney = Journey(graph);
        LacanSignifierFlow.TryPrepare(refuse, LacanSignifierAction.RefuseObject);
        Check(LacanSignifierFlow.TryCommitPending(refuse, refuseJourney, graph),
            "Refusing to name the object must decline the act's fixed slot.");
        Check(refuse.Standard == LacanSignifierStandard.Unresolved && refuse.FixedDeclined
            && refuseJourney.DeclinedFixedActs.Contains(3)
            && refuseJourney.CurrentNodeId == LacanSignifierState.SourceNodeId,
            "Refusing must leave the player on Freud's node with the slot declined.");

        var tampered = LacanSignifierState.Create("LACAN_SIGNIFIER:TEST:5");
        tampered.Standard = LacanSignifierStandard.Claimed;
        Check(!tampered.IsUsable(), "A standard without its recorded actions must be rejected.");

        var unansweredTampered = LacanSignifierState.Create("LACAN_SIGNIFIER:TEST:6");
        Bind(unansweredTampered, LacanSignifierAction.BindKeeper);
        LacanSignifierFlow.TryPrepare(unansweredTampered, LacanSignifierAction.ClaimNow);
        LacanSignifierFlow.Apply(unansweredTampered, LacanSignifierAction.ClaimNow);
        unansweredTampered.UnansweredRequests.Clear();
        Check(!unansweredTampered.IsUsable(),
            "Dropping the recorded unanswered request must invalidate the state.");
    }

    private static WesternJourneyState Journey(WesternRouteGraph graph)
    {
        var journey = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        // Freud's handover belongs to the unconscious line and fills act two, so
        // the run has to be on that line before it can walk it.
        journey.RouteId = "UNCONSCIOUS_AND_NORM";
        journey.LastFixedAct = 1;
        Check(graph.TryApply(journey, LacanSignifierState.RequiredEdgeId),
            "The room needs Freud's completed second-act edge.");
        Check(journey.CurrentNodeId == LacanSignifierState.SourceNodeId && journey.LastFixedAct == 2,
            "Freud's second-act edge must place the run on his node with act two open.");
        return journey;
    }

    private static void Put(WesternJourneyState journey, int act) => journey.LastFixedAct = act;

    private static void Bind(LacanSignifierState state, LacanSignifierAction action)
    {
        Check(LacanSignifierFlow.TryPrepare(state, action), $"Binding {action} must be selectable.");
        Check(LacanSignifierFlow.TryCommitPending(state, Journey(WesternRouteGraph.LoadEmbedded()),
                WesternRouteGraph.LoadEmbedded()),
            $"Binding {action} must be committed.");
        Check(state.Stage == LacanSignifierStage.Interrupted, "Binding must reach the interruption.");
    }
}
