using STS2Philosophers;

internal static class MontesquieuBalanceChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey(WesternRouteGraph graph) =>
        graph.Start(MontesquieuBalanceState.SourceNodeId);

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = MontesquieuBalanceState.Create("MONTESQUIEU_BALANCE:TEST:1");
        Check(MontesquieuBalanceFlow.Options(state.Stage).Count == 3,
            "The room must offer separation, faction balance, or keeping the unified arrangement.");

        Check(MontesquieuBalanceFlow.TryPrepare(state, MontesquieuBalanceAction.SeparatePowers),
            "Separation must be selectable.");
        var separated = Journey(graph);
        Check(MontesquieuBalanceFlow.IsRouteConsistent(state, separated),
            "The room must sit on the first-act fixed slot.");
        Check(MontesquieuBalanceFlow.TryCommitPending(state, separated, graph),
            "Separation must take the first-act fixed link.");
        Check(state.Standard == MontesquieuBalanceStandard.Separation && state.EdgeCommitted
            && state.Stage == MontesquieuBalanceStage.ResultSeparation,
            "Separation must record its standard and stage.");
        Check(separated.CompletedEdgeIds.Contains(MontesquieuBalanceState.CompletionEdgeId)
            && separated.CurrentNodeId == MontesquieuBalanceState.AdoptedNodeId
            && separated.LastFixedAct == 2,
            "Adopting must move the journey to Montesquieu and consume act one.");
        Check(MontesquieuBalanceFlow.IsRouteConsistent(state, separated),
            "The committed room must stay route-consistent.");
        Check(MontesquieuBalanceFlow.TryPrepare(state, MontesquieuBalanceAction.Leave),
            "A finished room must close.");
        MontesquieuBalanceFlow.Apply(state, MontesquieuBalanceAction.Leave);
        Check(state.Closed && state.IsUsable(), "A closed room must stay replayable.");

        var kept = MontesquieuBalanceState.Create("MONTESQUIEU_BALANCE:TEST:2");
        MontesquieuBalanceFlow.TryPrepare(kept, MontesquieuBalanceAction.PreservePrivilege);
        var declinedJourney = Journey(graph);
        Check(MontesquieuBalanceFlow.TryCommitPending(kept, declinedJourney, graph),
            "Keeping the unified arrangement must decline the act's fixed slot.");
        Check(declinedJourney.DeclinedFixedActs.Contains(2)
            && declinedJourney.CurrentNodeId == MontesquieuBalanceState.SourceNodeId,
            "Declining must leave the player on Plato's institutions node.");

        var voltairian = MontesquieuBalanceState.Create("MONTESQUIEU_BALANCE:TEST:4");
        VoltaireToleranceState segment = voltairian.Voltaire!;
        Check(segment is { Closed: false } && segment.IsUsable(),
            "A new room must carry an open Voltaire segment.");
        Check(VoltaireToleranceFlow.Options(segment.Stage).Count == 3,
            "Voltaire must offer protection, restriction, or keeping the balance.");
        Check(!VoltaireToleranceFlow.TryPrepare(segment, VoltaireToleranceAction.Leave),
            "Voltaire must not close before a verdict.");
        Check(VoltaireToleranceFlow.TryPrepare(segment, VoltaireToleranceAction.ProtectSpeech),
            "Protecting the pamphlet must be selectable.");
        VoltaireToleranceFlow.Apply(segment, VoltaireToleranceAction.ProtectSpeech);
        Check(segment.Conclusion == VoltaireToleranceConclusion.Protect
            && segment.Stage == VoltaireToleranceStage.ResultProtect,
            "Protection must record its conclusion.");
        Check(VoltaireToleranceFlow.TryPrepare(segment, VoltaireToleranceAction.Leave),
            "A verdict page must close.");
        VoltaireToleranceFlow.Apply(segment, VoltaireToleranceAction.Leave);
        Check(segment.Closed && segment.IsUsable(), "A closed Voltaire segment must stay replayable.");
        var reopened = MontesquieuBalanceState.Create("MONTESQUIEU_BALANCE:TEST:5");
        reopened.Inspecting = true;
        reopened.Voltaire!.Closed = true;
        Check(!reopened.IsUsable(), "Inspecting a closed Voltaire segment must be rejected.");

        var tampered = MontesquieuBalanceState.Create("MONTESQUIEU_BALANCE:TEST:3");
        tampered.Standard = MontesquieuBalanceStandard.Balance;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");
        tampered.Version = MontesquieuBalanceState.CurrentVersion + 1;
        Check(!tampered.IsUsable(), "An unknown state version must be rejected.");
    }
}
