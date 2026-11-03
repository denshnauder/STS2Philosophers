using STS2Philosophers;

internal static class VoltairePublicCritiqueChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static VoltairePublicCritiqueContext At(bool question = true, int act = 1, string? route = "CHECKS_AND_JUSTICE",
        string? node = MontesquieuBalanceState.AdoptedNodeId, int lastFixed = 2, bool host = true, bool usable = true,
        bool resolved = false, bool saved = false, bool savedUsable = true, bool savedClosed = false) =>
        new(true, question, true, act, route, node, lastFixed, host, usable, resolved,
            saved, savedUsable, savedClosed, true);

    public static void Run()
    {
        Check(VoltairePublicCritiquePolicy.ShouldInsert(At()),
            "The checks line meets Voltaire at an act-two question node after Montesquieu.");
        Check(!VoltairePublicCritiquePolicy.ShouldInsert(At(question: false)),
            "Voltaire must not replace a non-question node.");
        Check(!VoltairePublicCritiquePolicy.ShouldInsert(At(act: 0)),
            "Voltaire must not appear in act one.");
        Check(!VoltairePublicCritiquePolicy.ShouldInsert(At(act: 2)),
            "Voltaire must not appear in act three.");
        Check(!VoltairePublicCritiquePolicy.ShouldInsert(At(host: false)),
            "Voltaire must wait for Montesquieu's fixed room.");
        Check(!VoltairePublicCritiquePolicy.ShouldInsert(At(lastFixed: 1)),
            "Voltaire must not stand in for the act's fixed slot.");
        Check(!VoltairePublicCritiquePolicy.ShouldInsert(At(node: "PLATO__FREEDOM_AND_INSTITUTIONS__CORE")),
            "Voltaire must appear on the figure the checks line reached, not on its entry.");
        Check(!VoltairePublicCritiquePolicy.ShouldInsert(At(route: "CONSEQUENCE_AND_FREEDOM")),
            "Another line does not reach Voltaire here.");
        Check(!VoltairePublicCritiquePolicy.ShouldInsert(At(resolved: true)),
            "A spent act-two question must not be replaced again.");
        Check(VoltairePublicCritiquePolicy.ShouldInsert(At(saved: true, savedClosed: false)),
            "An open saved critique resumes.");
        Check(!VoltairePublicCritiquePolicy.ShouldInsert(At(saved: true, savedClosed: true)),
            "A closed critique is not handed out twice.");

        // The event is the same tolerance case as the in-batch segment: a write
        // that lands between the two saves must be completed on reload, not lost.
        var state = VoltaireToleranceState.Create("VOLTAIRE_PUBLIC_CRITIQUE:TEST:2");
        Check(VoltaireToleranceFlow.TryPrepare(state, VoltaireToleranceAction.ProtectSpeech),
            "Protecting the pamphlet must be selectable.");
        var reloaded = System.Text.Json.JsonSerializer.Deserialize<VoltaireToleranceState>(
            System.Text.Json.JsonSerializer.Serialize(state))!;
        Check(reloaded.IsUsable() && reloaded.PendingAction == VoltaireToleranceAction.ProtectSpeech
            && VoltaireToleranceFlow.Options(reloaded.Stage).Contains(reloaded.PendingAction),
            "A prepared choice must survive a reload and stay legal on its page.");
        VoltaireToleranceFlow.Apply(reloaded, reloaded.PendingAction);
        reloaded.PendingAction = VoltaireToleranceAction.None;
        Check(reloaded.Conclusion == VoltaireToleranceConclusion.Protect
            && reloaded.Stage == VoltaireToleranceStage.ResultProtect,
            "Recovering the prepared choice must record its verdict.");
        Check(VoltaireToleranceFlow.TryPrepare(reloaded, VoltaireToleranceAction.Leave),
            "The result page must still let the player leave.");
        VoltaireToleranceFlow.Apply(reloaded, VoltaireToleranceAction.Leave);
        Check(reloaded.Closed, "Leaving closes the critique.");
        Check(!VoltaireToleranceFlow.TryPrepare(reloaded, VoltaireToleranceAction.Leave),
            "A closed critique offers nothing further.");
    }
}
