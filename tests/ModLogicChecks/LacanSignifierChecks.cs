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
        Check(graph.TryApply(journey, LacanSignifierState.RequiredEdgeId),
            "The room needs Freud's completed first-act edge.");
        Check(journey.CurrentNodeId == LacanSignifierState.SourceNodeId && journey.LastFixedAct == 2,
            "Freud's first-act edge must place the run on his node with act two open.");
        return journey;
    }

    private static void Bind(LacanSignifierState state, LacanSignifierAction action)
    {
        Check(LacanSignifierFlow.TryPrepare(state, action), $"Binding {action} must be selectable.");
        Check(LacanSignifierFlow.TryCommitPending(state, Journey(WesternRouteGraph.LoadEmbedded()),
                WesternRouteGraph.LoadEmbedded()),
            $"Binding {action} must be committed.");
        Check(state.Stage == LacanSignifierStage.Interrupted, "Binding must reach the interruption.");
    }
}
