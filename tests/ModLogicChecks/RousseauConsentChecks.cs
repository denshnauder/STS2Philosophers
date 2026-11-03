using STS2Philosophers;

internal static class RousseauConsentChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_019"), "019 must be completed first.");
        Check(graph.TryApply(journey, "WESTERN_EDGE_020"), "020 must be completed first.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            RousseauConsent = RousseauConsentState.Create("ROUSSEAU_CONSENT:TEST:2"),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        RousseauConsentAction action)
    {
        Check(RousseauConsentFlow.TryPrepare(shared.RousseauConsent!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(RousseauConsentFlow.TryCommitPending(shared.RousseauConsent!, shared.WesternJourney!, graph),
            $"Commit {action}.");
        Check(!RousseauConsentFlow.TryCommitPending(shared.RousseauConsent!, shared.WesternJourney!, graph),
            "A confirmed Rousseau action cannot replay.");
        shared = Reload(shared);
        Check(RousseauConsentFlow.IsRouteConsistent(shared.RousseauConsent!, shared.WesternJourney!),
            "Rousseau survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState late = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(late, "WESTERN_EDGE_019"), "019 must be completed first.");
        // The act boundary hands over the second act with its position already
        // recorded, so 020 leaves the position at three.
        late.LastFixedAct = 2;
        Check(graph.TryApply(late, "WESTERN_EDGE_020"), "020 must be completed first.");
        Check(late.LastFixedAct == 3, "020 moves the fixed position to three.");
        // The second act's fixed slot is spent by the time its question is asked,
        // so the question has to be found by its act rather than by the position.
        Check(graph.TryReserveQuestion(late, 2),
            "The second act's question stays answerable after the fixed slot moved on.");
        Check(late.ResolvedQuestionActs.Contains(2), "The reservation records the second act.");
        Check(graph.TryCompleteReservedQuestion(late, RousseauConsentState.CompletionEdgeId),
            "The reserved question completes at the same position.");
        PhilosophyRunState shared = Fixture(graph);
        Check(graph.TryReserveQuestion(shared.WesternJourney, 2),
            "The second act's question stays answerable after the fixed slot moved on.");
        Check(shared.WesternJourney.ResolvedQuestionActs.Contains(2),
            "The reservation records the second act.");
        Check(graph.TryCompleteReservedQuestion(shared.WesternJourney, RousseauConsentState.CompletionEdgeId),
            "The reserved question completes at the same position.");
        shared = Fixture(graph);
        Check(RousseauConsentFlow.IsRouteConsistent(shared.RousseauConsent!, shared.WesternJourney!),
            "Rousseau opens at Augustine after 020.");
        shared = Commit(shared, graph, RousseauConsentAction.InspectConvention);
        Check(!shared.RousseauConsent!.ConventionKnown && shared.RousseauConsent.ExcludedCompanion,
            "The insufficient-data branch records the companion's counterexample.");
        shared = Commit(shared, graph, RousseauConsentAction.UniversalException);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(RousseauConsentState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == RousseauConsentState.AdoptedNodeId
            && shared.WesternJourney.LastFixedAct == 2
            && shared.WesternJourney.ResolvedQuestionActs.Contains(2),
            "A universal exception completes 021 and enters Rousseau.");
        shared = Commit(shared, graph, RousseauConsentAction.Leave);
        Check(shared.RousseauConsent!.Closed, "Rousseau closes after Leave.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, RousseauConsentAction.KeepAugustine);
        Check(shared.WesternJourney!.ResolvedQuestionActs.Contains(2)
            && !shared.WesternJourney.CompletedEdgeIds.Contains(RousseauConsentState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == RousseauConsentState.SourceNodeId,
            "Declining Rousseau spends only the act-two question slot.");
        shared = Commit(shared, graph, RousseauConsentAction.Leave);

        shared = Fixture(graph);
        shared.WesternJourney!.CompletedEdgeIds.Remove(RousseauConsentState.RequiredEdgeId);
        Check(!RousseauConsentFlow.IsRouteConsistent(shared.RousseauConsent!, shared.WesternJourney!),
            "Rousseau cannot open without 020.");

        RousseauConsentTriggerContext context = new(true, true, true, 1, true, 1,
            RousseauConsentState.SourceNodeId, 2, true, true, false, false, false, false, true);
        Check(RousseauConsentTriggerPolicy.ShouldInsert(context),
            "Rousseau is offered after a real act-two combat.");
        Check(!RousseauConsentTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 0 }),
            "Rousseau is not an act-one event.");
        Check(!RousseauConsentTriggerPolicy.ShouldInsert(context with { RequiredEdgeCompleted = false }),
            "Rousseau requires 020.");
        Check(!RousseauConsentTriggerPolicy.ShouldInsert(context with { CompletionEdgeEligible = false }),
            "Rousseau requires an eligible 021.");
        // Sol's repro: the new host opens act two at position 2, applying Augustine's
        // fixed 020 moves the journey to 3, and the act-two question node then comes
        // with position 3. That is "this act's fixed room is done", not act three.
        WesternRouteGraph real = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState actTwo = real.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(real.TryApply(actTwo, "WESTERN_EDGE_019"), "Act one must be able to walk to Augustine.");
        actTwo.LastFixedAct = 2;
        Check(real.TryApply(actTwo, "WESTERN_EDGE_020") && actTwo.LastFixedAct == 3,
            "Augustine's fixed room must move the journey to position 3 while still in act two.");
        Check(actTwo.CurrentNodeId == RousseauConsentState.SourceNodeId,
            "Augustine's room stands on the figure Rousseau's question starts from.");
        Check(RousseauConsentTriggerPolicy.ShouldInsert(context with { LastFixedAct = 3 }),
            "Rousseau must be offered at the act-two question after 020, not only at position 2.");
        var afterFixed = RousseauConsentState.Create("ROUSSEAU_CONSENT:TEST:2");
        Check(RousseauConsentFlow.IsRouteConsistent(afterFixed, actTwo),
            "Rousseau's record must accept the position the fixed room left behind.");
    }
}
