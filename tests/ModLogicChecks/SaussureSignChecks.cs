using STS2Philosophers;

internal static class SaussureSignChecks
{
    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = SaussureSignState.Create("SAUSSURE_SIGN:TEST:1");
        Check(SaussureSignFlow.Options(state.Stage).Count == 3,
            "Saussure must offer language/speech, the arbitrary sign, or keeping Aristotle.");
        Check(SaussureSignFlow.TryPrepare(state, SaussureSignAction.LanguageParole),
            "Language/speech must be selectable.");
        var journey = graph.Start(SaussureSignState.SourceNodeId);
        journey.LastFixedAct = 2;
        Check(SaussureSignFlow.IsRouteConsistent(state, journey),
            "The room must sit on the first-act fixed slot.");
        Check(SaussureSignFlow.TryCommitPending(state, journey, graph),
            "The choice must take the first-act fixed link.");
        Check(state.Standard == SaussureSignStandard.Langue && state.EdgeCommitted
            && state.Stage == SaussureSignStage.ResultLangue,
            "The choice must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(SaussureSignState.CompletionEdgeId)
            && journey.CurrentNodeId == SaussureSignState.AdoptedNodeId
            && journey.LastFixedAct == 3,
            "Adopting must move to Saussure and consume the act's fixed slot.");

        var kept = SaussureSignState.Create("SAUSSURE_SIGN:TEST:2");
        SaussureSignFlow.TryPrepare(kept, SaussureSignAction.KeepAristotle);
        var declined = graph.Start(SaussureSignState.SourceNodeId);
        declined.LastFixedAct = 2;
        Check(SaussureSignFlow.TryCommitPending(kept, declined, graph),
            "Keeping Aristotle must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == SaussureSignState.SourceNodeId,
            "Declining must leave the player on Aristotle's node.");

        var tampered = SaussureSignState.Create("SAUSSURE_SIGN:TEST:3");
        tampered.Standard = SaussureSignStandard.Sign;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
