using STS2Philosophers;

internal static class ButlerAddressChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph)
    {
        // The turned line reaches Beauvoir through 193 instead of the default 040:
        // the act-two fixed room is Beauvoir, so the journey starts at the node
        // 193 leaves and takes that edge.
        WesternJourneyState journey = graph.Start(WollstonecraftAdmissionState.SourceNodeId);
        Check(graph.TryApply(journey, WollstonecraftAdmissionState.CompletionEdgeId), "040 must be completed first.");
        journey.CurrentNodeId = "MARY_WOLLSTONECRAFT__FREEDOM_AND_INSTITUTIONS__CORE";
        journey.LastFixedAct = 1;
        // 193 belongs to the education line, so the journey has to name it.
        journey.RouteId = "EDUCATION_AND_SITUATION";
        journey.RouteRules = true;
        Check(graph.TryApply(journey, ButlerAddressState.RequiredEdgeId), "193 must carry Beauvoir's act two.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            ButlerAddress = ButlerAddressState.Create("BUTLER_ADDRESS:TEST:3"),
        };
    }

    private static PhilosophyRunState LacanFixture(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        journey.RouteId = "UNCONSCIOUS_AND_NORM";
        journey.RouteRules = true;
        journey.CompletedEdgeIds.Add("WESTERN_EDGE_189");
        journey.CompletedEdgeIds.Add(ButlerAddressState.LacanRequiredEdgeId);
        journey.SeenThinkerIds.Add("SIGMUND_FREUD");
        journey.SeenThinkerIds.Add("JACQUES_LACAN");
        journey.CurrentNodeId = ButlerAddressState.LacanSourceNodeId;
        journey.LastFixedAct = 2;
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            ButlerAddress = new ButlerAddressState
            {
                InstanceId = "BUTLER_ADDRESS:TEST:3",
                FromLacan = true,
            },
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        ButlerAddressAction action)
    {
        Check(ButlerAddressFlow.TryPrepare(shared.ButlerAddress!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(ButlerAddressFlow.TryCommitPending(shared.ButlerAddress!, shared.WesternJourney!, graph),
            $"Commit {action}.");
        Check(!ButlerAddressFlow.TryCommitPending(shared.ButlerAddress!, shared.WesternJourney!, graph),
            "A confirmed Butler action cannot replay.");
        shared = Reload(shared);
        Check(ButlerAddressFlow.IsRouteConsistent(shared.ButlerAddress!, shared.WesternJourney!),
            "Butler survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        Check(ButlerAddressFlow.IsRouteConsistent(shared.ButlerAddress!, shared.WesternJourney!),
            "Butler opens at Beauvoir after 193.");
        Check(shared.ButlerAddress!.Cost == 0m, "Opening the ledger costs nothing.");

        // Amending the table is the only branch that pays, and it pays once.
        PhilosophyRunState amended = Commit(shared, graph, ButlerAddressAction.ObserveLedger);
        amended = Commit(amended, graph, ButlerAddressAction.AmendRegistry);
        Check(amended.ButlerAddress!.Judgment == ButlerAddressJudgment.Amended
            && amended.ButlerAddress.Cost == ButlerAddressState.ReprintGoldCost
            && amended.WesternJourney!.CompletedEdgeIds.Contains(ButlerAddressState.CompletionEdgeId),
            "Reprinting the table costs 15 gold and completes 194.");
        decimal gold = 100m;
        int charges = 0;
        var first = EventGoldCostProtocol.ApplyAsync(amended.ButlerAddress, () => (decimal?)gold,
            () => { gold -= amended.ButlerAddress.Cost; charges++; return Task.CompletedTask; },
            () => Task.FromResult(true)).GetAwaiter().GetResult();
        var second = EventGoldCostProtocol.ApplyAsync(amended.ButlerAddress, () => (decimal?)gold,
            () => { gold -= amended.ButlerAddress.Cost; charges++; return Task.CompletedTask; },
            () => Task.FromResult(true)).GetAwaiter().GetResult();
        Check(first == EventGoldCostOutcome.Settled && second == EventGoldCostOutcome.Settled
            && gold == 85m && charges == 1,
            "The reprint is charged exactly once and a replay never takes gold twice.");
        amended = Commit(amended, graph, ButlerAddressAction.Leave);
        Check(amended.ButlerAddress!.Closed, "Butler closes after Leave.");

        // Letting the person propose a procedure costs no gold but still enters
        // the node; the immediate delivery is the price.
        PhilosophyRunState alternative = Commit(Fixture(graph), graph, ButlerAddressAction.ObserveLedger);
        alternative = Commit(alternative, graph, ButlerAddressAction.AlternativeProcess);
        Check(alternative.ButlerAddress!.Judgment == ButlerAddressJudgment.Alternative
            && alternative.ButlerAddress.Cost == 0m
            && alternative.WesternJourney!.CompletedEdgeIds.Contains(ButlerAddressState.CompletionEdgeId),
            "An alternative procedure enters the node without charging gold.");

        // Keeping the old table spends the slot and completes nothing.
        PhilosophyRunState kept = Commit(Fixture(graph), graph, ButlerAddressAction.KeepLedger);
        Check(kept.ButlerAddress!.FixedDeclined && kept.ButlerAddress.Closed == false
            && kept.WesternJourney!.DeclinedFixedActs.Contains(3)
            && !kept.WesternJourney.CompletedEdgeIds.Contains(ButlerAddressState.CompletionEdgeId),
            "Keeping the ledger declines the act-three fixed slot without completing 194.");
        kept = Commit(kept, graph, ButlerAddressAction.Leave);
        Check(kept.ButlerAddress!.Closed, "The kept ledger still closes the room.");

        // The same room must close the unconscious line after Lacan, not look
        // for Beauvoir's 193. The route is named and 190 is already complete.
        PhilosophyRunState lacan = LacanFixture(graph);
        Check(ButlerAddressFlow.IsRouteConsistent(lacan.ButlerAddress!, lacan.WesternJourney!),
            "Butler opens at Lacan after 190.");
        lacan = Commit(lacan, graph, ButlerAddressAction.ObserveLedger);
        lacan = Commit(lacan, graph, ButlerAddressAction.AmendRegistry);
        Check(lacan.ButlerAddress!.Judgment == ButlerAddressJudgment.Amended
            && lacan.WesternJourney!.CompletedEdgeIds.Contains(ButlerAddressState.LacanCompletionEdgeId)
            && !lacan.WesternJourney.CompletedEdgeIds.Contains(ButlerAddressState.CompletionEdgeId)
            && lacan.WesternJourney.CurrentNodeId == ButlerAddressState.AdoptedNodeId
            && lacan.WesternJourney.LastFixedAct == 3,
            "Lacan's line must complete 191, not Beauvoir's 194.");
        lacan = Commit(lacan, graph, ButlerAddressAction.Leave);
        Check(lacan.ButlerAddress!.Closed, "The Lacan line closes Butler after Leave.");

        PhilosophyRunState lacanKept = Commit(LacanFixture(graph), graph, ButlerAddressAction.KeepLedger);
        Check(lacanKept.ButlerAddress!.FixedDeclined
            && lacanKept.WesternJourney!.DeclinedFixedActs.Contains(3)
            && !lacanKept.WesternJourney.CompletedEdgeIds.Contains(ButlerAddressState.LacanCompletionEdgeId),
            "Keeping the ledger declines Lacan's act-three slot without completing 191.");

        // The record is replayable: a tampered history is rejected, a reload is not.
        PhilosophyRunState tampered = Fixture(graph);
        tampered.ButlerAddress!.ConfirmedActions.Add(ButlerAddressAction.AmendRegistry);
        Check(!tampered.ButlerAddress.IsUsable(),
            "A judgment without its recorded actions must be rejected.");
        Check(ButlerAddressState.Create("BUTLER_ADDRESS:TEST:3").ActIndex == 3,
            "Butler's record names the act the room opens in.");
    }
}
