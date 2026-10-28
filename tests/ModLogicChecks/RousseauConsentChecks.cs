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
        PhilosophyRunState shared = Fixture(graph);
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
    }
}
