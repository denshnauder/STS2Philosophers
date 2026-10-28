using STS2Philosophers;

internal static class LeibnizMonadComparisonChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph)
    {
        var journey = new WesternJourneyState
        {
            CurrentNodeId = LeibnizMonadComparisonState.SourceNodeId,
            LastFixedAct = 2,
        };
        journey.CompletedEdgeIds.Add(LeibnizMonadComparisonState.RequiredEdgeId);
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            LeibnizMonadComparison = LeibnizMonadComparisonState.Create("LEIBNIZ_MONAD_COMPARISON:TEST:2"),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        LeibnizMonadComparisonAction action)
    {
        Check(LeibnizMonadComparisonFlow.TryPrepare(shared.LeibnizMonadComparison!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(LeibnizMonadComparisonFlow.TryCommitPending(shared.LeibnizMonadComparison!,
            shared.WesternJourney!, graph), $"Commit {action}.");
        Check(!LeibnizMonadComparisonFlow.TryCommitPending(shared.LeibnizMonadComparison!,
            shared.WesternJourney!, graph), "A confirmed comparison action cannot replay.");
        shared = Reload(shared);
        Check(LeibnizMonadComparisonFlow.IsRouteConsistent(shared.LeibnizMonadComparison!,
            shared.WesternJourney!), "Leibniz comparison survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        Check(LeibnizMonadComparisonFlow.IsRouteConsistent(shared.LeibnizMonadComparison!, shared.WesternJourney!),
            "Leibniz comparison opens at Spinoza's being node after 023.");

        shared = Commit(shared, graph, LeibnizMonadComparisonAction.CompareRecords);
        Check(shared.LeibnizMonadComparison!.RecordsCompared,
            "The two records are read before judgment.");
        shared = Commit(shared, graph, LeibnizMonadComparisonAction.KeepSubstance);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(LeibnizMonadComparisonState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == LeibnizMonadComparisonState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 2
            && shared.WesternJourney.ResolvedQuestionActs.Contains(2)
            && shared.LeibnizMonadComparison!.EdgeCommitted
            && !shared.LeibnizMonadComparison.MonadsRecorded,
            "Keeping substance completes 024 without switching nodes.");
        shared = Commit(shared, graph, LeibnizMonadComparisonAction.Leave);
        Check(shared.LeibnizMonadComparison!.Closed, "The comparison closes after Leave.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, LeibnizMonadComparisonAction.CompareRecords);
        shared = Commit(shared, graph, LeibnizMonadComparisonAction.MarkMonads);
        Check(shared.LeibnizMonadComparison!.MonadsRecorded
            && shared.WesternJourney!.SeenThinkerIds.Contains(LeibnizMonadComparisonState.SeenThinkerId)
            && shared.WesternJourney.CompletedEdgeIds.Contains(LeibnizMonadComparisonState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == LeibnizMonadComparisonState.SourceNodeId,
            "Recording the monads marks Leibniz seen but keeps Spinoza's node.");
        shared = Commit(shared, graph, LeibnizMonadComparisonAction.Leave);

        shared = Fixture(graph);
        shared = Commit(shared, graph, LeibnizMonadComparisonAction.KeepSpinoza);
        Check(shared.LeibnizMonadComparison!.QuestionDeclined
            && !shared.WesternJourney!.CompletedEdgeIds.Contains(LeibnizMonadComparisonState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == LeibnizMonadComparisonState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 2
            && shared.WesternJourney.ResolvedQuestionActs.Contains(2),
            "Declining the comparison spends only the act-two question.");
        shared = Commit(shared, graph, LeibnizMonadComparisonAction.Leave);

        LeibnizMonadComparisonTriggerContext context = new(true, true, true, 1, true, 1,
            LeibnizMonadComparisonState.SourceNodeId, 2, true, true, false, false, false, false, true);
        Check(LeibnizMonadComparisonTriggerPolicy.ShouldInsert(context),
            "Leibniz comparison is offered after a real act-two combat.");
        Check(!LeibnizMonadComparisonTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 0 }),
            "Leibniz comparison is not an act-one event.");
        Check(!LeibnizMonadComparisonTriggerPolicy.ShouldInsert(context with { LastFixedAct = 1 }),
            "Leibniz comparison requires the act-two fixed state.");
        Check(!LeibnizMonadComparisonTriggerPolicy.ShouldInsert(context with { CompletionEdgeEligible = false }),
            "Leibniz comparison requires an eligible 024.");
    }
}
