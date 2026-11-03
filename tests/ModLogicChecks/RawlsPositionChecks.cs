using STS2Philosophers;

internal static class RawlsPositionChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph, string required = RawlsPositionState.KantRequiredEdgeId)
    {
        string source = RawlsPositionFlow.SourceNodeIdOf(required);
        WesternJourneyState journey = graph.Start(WollstonecraftAdmissionState.SourceNodeId);
        journey.CurrentNodeId = source;
        journey.LastFixedAct = 2;
        journey.RouteId = required == RawlsPositionState.MillRequiredEdgeId
            ? "CONSEQUENCE_AND_FREEDOM" : "CHECKS_AND_JUSTICE";
        journey.RouteRules = true;
        ((HashSet<string>)journey.CompletedEdgeIds).Add(required);
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            RawlsPosition = RawlsPositionState.Create("RAWLS_POSITION:TEST:3", required),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        RawlsPositionAction action)
    {
        Check(RawlsPositionFlow.TryPrepare(shared.RawlsPosition!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(RawlsPositionFlow.TryCommitPending(shared.RawlsPosition!, shared.WesternJourney!, graph),
            $"Commit {action}.");
        Check(!RawlsPositionFlow.TryCommitPending(shared.RawlsPosition!, shared.WesternJourney!, graph),
            "A confirmed Rawls action cannot replay.");
        shared = Reload(shared);
        Check(RawlsPositionFlow.IsRouteConsistent(shared.RawlsPosition!, shared.WesternJourney!),
            "Rawls survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        Check(RawlsPositionFlow.IsRouteConsistent(shared.RawlsPosition!, shared.WesternJourney!),
            "Rawls opens at Kant after 172.");
        Check(RawlsPositionFlow.IsRouteConsistent(Fixture(graph, RawlsPositionState.MillRequiredEdgeId).RawlsPosition!,
            Fixture(graph, RawlsPositionState.MillRequiredEdgeId).WesternJourney!),
            "Rawls also opens at Mill after 169.");

        // The draw is fixed when the room opens: a reload must not redraw it.
        var standing = shared.RawlsPosition!.Standing;
        Check(Reload(shared).RawlsPosition!.Standing == standing,
            "The standing must survive a reload unchanged.");
        Check(RawlsPositionFlow.StandingOf("RAWLS_POSITION:A:3") == RawlsPositionFlow.StandingOf("RAWLS_POSITION:A:3"),
            "The standing is a stable function of the instance id.");
        Check(Enum.IsDefined(standing), "The standing must be one of the three named ones.");

        // The rule is published before the standing is revealed to the player,
        // and the room only completes when the player acts on it.
        PhilosophyRunState published = Commit(shared, graph, RawlsPositionAction.DraftMinimum);
        Check(published.RawlsPosition!.Stage == RawlsPositionStage.RuleFrozen
            && published.RawlsPosition.Rule == RawlsRule.Minimum
            && !published.WesternJourney!.CompletedEdgeIds.Contains(RawlsPositionState.KantCompletionEdgeId),
            "Publishing the rule must not complete the room on its own.");

        PhilosophyRunState executed = Commit(published, graph, RawlsPositionAction.Execute);
        Check(executed.RawlsPosition!.Stage == RawlsPositionStage.ResultExecuted
            && executed.WesternJourney!.CompletedEdgeIds.Contains(RawlsPositionState.KantCompletionEdgeId),
            "Executing the published rule completes 173.");
        executed = Commit(executed, graph, RawlsPositionAction.Leave);
        Check(executed.RawlsPosition!.Closed, "Rawls closes after Leave.");

        // Reopening the scheme is paid work for everyone, and it is charged once.
        PhilosophyRunState amended = Commit(Fixture(graph), graph, RawlsPositionAction.DraftLottery);
        amended = Commit(amended, graph, RawlsPositionAction.AmendUniversally);
        Check(amended.RawlsPosition!.Cost == RawlsPositionState.AmendGoldCost
            && amended.WesternJourney!.CompletedEdgeIds.Contains(RawlsPositionState.KantCompletionEdgeId),
            "Reopening the scheme costs 25 gold and completes the room.");
        decimal gold = 100m;
        int charges = 0;
        var first = EventGoldCostProtocol.ApplyAsync(amended.RawlsPosition, () => (decimal?)gold,
            () => { gold -= amended.RawlsPosition.Cost; charges++; return Task.CompletedTask; },
            () => Task.FromResult(true)).GetAwaiter().GetResult();
        var second = EventGoldCostProtocol.ApplyAsync(amended.RawlsPosition, () => (decimal?)gold,
            () => { gold -= amended.RawlsPosition.Cost; charges++; return Task.CompletedTask; },
            () => Task.FromResult(true)).GetAwaiter().GetResult();
        Check(first == EventGoldCostOutcome.Settled && second == EventGoldCostOutcome.Settled
            && gold == 75m && charges == 1,
            "Reopening the scheme is charged exactly once and never twice on replay.");

        // Withdrawing spends the slot and writes nothing.
        PhilosophyRunState withdrawn = Commit(Fixture(graph), graph, RawlsPositionAction.DraftTotalDiscount);
        withdrawn = Commit(withdrawn, graph, RawlsPositionAction.Withdraw);
        Check(withdrawn.RawlsPosition!.FixedDeclined
            && withdrawn.WesternJourney!.DeclinedFixedActs.Contains(3)
            && !withdrawn.WesternJourney.CompletedEdgeIds.Contains(RawlsPositionState.KantCompletionEdgeId),
            "Withdrawing declines the act-three fixed slot and completes nothing.");

        // A tampered history is rejected; the record names the act it opened in.
        PhilosophyRunState tampered = Fixture(graph);
        tampered.RawlsPosition!.ConfirmedActions.Add(RawlsPositionAction.Execute);
        Check(!tampered.RawlsPosition.IsUsable(), "An outcome without its recorded actions must be rejected.");
        Check(RawlsPositionState.Create("RAWLS_POSITION:TEST:3", RawlsPositionState.KantRequiredEdgeId).ActIndex == 3,
            "Rawls's record names the act the room opens in.");
        Check(RawlsPositionState.Create("RAWLS_POSITION:TEST:3", "WESTERN_EDGE_999").RequiredEdgeId
            == RawlsPositionState.KantRequiredEdgeId,
            "An unknown line falls back to the Kant edge and is then rejected by the route check.");
    }
}
