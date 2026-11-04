using STS2Philosophers;

internal static class FregeSenseChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = FregeSenseState.Create("FREGE_SENSE:TEST:1");
        Check(FregeSenseFlow.Options(state.Stage).Count == 3,
            "Kierkegaard must offer description, the universal, or keeping Descartes.");
        Check(FregeSenseFlow.TryPrepare(state, FregeSenseAction.SenseAndReference),
            "The description must be selectable.");
        var journey = graph.Start(FregeSenseState.SourceNodeId);
        journey.LastFixedAct = 2;
        Check(FregeSenseFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(FregeSenseFlow.TryCommitPending(state, journey, graph),
            "The description must take the first-act fixed link.");
        Check(state.Standard == FregeSenseStandard.Sense && state.EdgeCommitted
            && state.Stage == FregeSenseStage.ResultSense,
            "The description must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(FregeSenseState.CompletionEdgeId)
            && journey.CurrentNodeId == FregeSenseState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Kierkegaard and consume act one.");

        var kept = FregeSenseState.Create("FREGE_SENSE:TEST:2");
        FregeSenseFlow.TryPrepare(kept, FregeSenseAction.KeepAristotle);
        var declined = graph.Start(FregeSenseState.SourceNodeId);
        declined.LastFixedAct = 2;
        Check(FregeSenseFlow.TryCommitPending(kept, declined, graph),
            "Keeping Socrates must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == FregeSenseState.SourceNodeId,
            "Declining must leave the player on Descartes' node.");

        var fixedState = FregeSenseState.CreateAt(
            "FREGE_SENSE:TEST:F", 2, "WESTERN_EDGE_104");
        var fixedJourney = graph.Start(FregeSenseState.SourceNodeId);
        fixedJourney.RouteId = "FORMAL_LANGUAGE";
        fixedJourney.LastFixedAct = 2;
        Check(FregeSenseFlow.IsRouteConsistent(fixedState, fixedJourney),
            "The default room must sit on act two's fixed slot after Aristotle.");
        Check(FregeSenseFlow.TryPrepare(fixedState, FregeSenseAction.SenseAndReference)
            && FregeSenseFlow.TryCommitPending(fixedState, fixedJourney, graph),
            "The default room must adopt Frege through edge 104.");
        Check(fixedJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_104")
            && fixedJourney.CurrentNodeId == FregeSenseState.AdoptedNodeId
            && fixedJourney.LastFixedAct == 3,
            "The default fixed room must consume act two, not a question slot.");

        var tampered = FregeSenseState.Create("FREGE_SENSE:TEST:3");
        tampered.Standard = FregeSenseStandard.Script;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }
}
