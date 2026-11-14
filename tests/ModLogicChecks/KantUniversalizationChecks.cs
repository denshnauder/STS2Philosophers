using STS2Philosophers;

internal static class KantUniversalizationChecks
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
        Check(graph.TryApply(journey, "WESTERN_EDGE_021"), "021 must be completed first.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            KantUniversalization = KantUniversalizationState.Create("KANT_UNIVERSALIZATION:TEST:3"),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        KantUniversalizationAction action)
    {
        Check(KantUniversalizationFlow.TryPrepare(shared.KantUniversalization!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(KantUniversalizationFlow.TryCommitPending(shared.KantUniversalization!, shared.WesternJourney!, graph),
            $"Commit {action}.");
        Check(!KantUniversalizationFlow.TryCommitPending(shared.KantUniversalization!, shared.WesternJourney!, graph),
            "A confirmed Kant action cannot replay.");
        shared = Reload(shared);
        Check(KantUniversalizationFlow.IsRouteConsistent(shared.KantUniversalization!, shared.WesternJourney!),
            "Kant survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        Check(KantUniversalizationFlow.IsRouteConsistent(shared.KantUniversalization!, shared.WesternJourney!),
            "Kant opens at Rousseau's freedom node after 021.");
        shared = Commit(shared, graph, KantUniversalizationAction.StateMaxim);
        shared = Commit(shared, graph, KantUniversalizationAction.UniversalRule);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(KantUniversalizationState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == KantUniversalizationState.AdoptedNodeId
            && shared.WesternJourney.LastFixedAct == 3
            && shared.KantUniversalization!.Maxim == KantMaxim.Universal
            && shared.KantUniversalization.Loss == KantUniversalizationLoss.Exemption,
            "The equal rule completes 022 and records the lost exemption.");
        shared = Commit(shared, graph, KantUniversalizationAction.Leave);
        Check(shared.KantUniversalization!.Closed, "Kant closes after Leave.");

        shared = Fixture(graph);
        shared = Commit(shared, graph, KantUniversalizationAction.KeepRousseau);
        Check(shared.WesternJourney!.DeclinedFixedActs.Contains(3)
            && !shared.WesternJourney.CompletedEdgeIds.Contains(KantUniversalizationState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == KantUniversalizationState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 3,
            "Declining Kant spends only the act-three fixed slot.");
        shared = Commit(shared, graph, KantUniversalizationAction.Leave);

        // The act-two Kant room refuses in place. TryDecline(Fixed, 2) leaves
        // LastFixedAct at 2; requiring act + 1 froze the visible KeepRousseau
        // button, so this path is pinned separately from the act-three refusal.
        WesternJourneyState actTwoJourney = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(actTwoJourney, "WESTERN_EDGE_171"),
            "The checks line's first-act turn must reach Montesquieu.");
        actTwoJourney.LastFixedAct = 2;
        var actTwoKant = KantUniversalizationState.CreateAt(
            "KANT_UNIVERSALIZATION:TEST:2", 2, "WESTERN_EDGE_172");
        actTwoKant.RoutePlaced = true;
        actTwoKant.RouteSourceNodeId = "MONTESQUIEU__FREEDOM_AND_INSTITUTIONS__CORE";
        Check(KantUniversalizationFlow.IsRouteConsistent(actTwoKant, actTwoJourney),
            "Kant opens on the checks line's act-two 172 handover.");
        Check(KantUniversalizationFlow.TryPrepare(actTwoKant, KantUniversalizationAction.KeepRousseau)
            && KantUniversalizationFlow.TryCommitPending(actTwoKant, actTwoJourney, graph),
            "Kant's act-two KeepRousseau must commit, not freeze on a forced act-three position.");
        Check(actTwoKant.FixedDeclined && actTwoKant.Stage == KantUniversalizationStage.ResultKept
            && actTwoJourney.LastFixedAct == 2
            && actTwoJourney.DeclinedFixedActs.Contains(2)
            && !actTwoJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_172"),
            "The act-two refusal spends slot 2 in place and leaves 172 uncommitted.");

        shared = Fixture(graph);
        shared.WesternJourney!.CompletedEdgeIds.Remove(KantUniversalizationState.RequiredEdgeId);
        Check(!KantUniversalizationFlow.IsRouteConsistent(shared.KantUniversalization!, shared.WesternJourney!),
            "Kant cannot open without 021.");

        KantUniversalizationTriggerContext context = new(true, true, true, 2, true, 1,
            KantUniversalizationState.SourceNodeId, 2, true, true, false, false, false, false, true);
        Check(KantUniversalizationTriggerPolicy.ShouldInsert(context),
            "Kant is offered after a real act-three combat.");
        Check(!KantUniversalizationTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 1 }),
            "Kant is not an act-two event.");
        Check(!KantUniversalizationTriggerPolicy.ShouldInsert(context with { RequiredEdgeCompleted = false }),
            "Kant requires 021.");
        Check(!KantUniversalizationTriggerPolicy.ShouldInsert(context with { CompletionEdgeEligible = false }),
            "Kant requires an eligible 022.");
    }
}
