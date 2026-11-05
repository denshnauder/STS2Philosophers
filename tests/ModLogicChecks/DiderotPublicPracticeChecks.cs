using STS2Philosophers;

internal static class DiderotPublicPracticeChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static DiderotPublicPracticeContext At(bool question = true, int act = 1, string? route = "SOCIAL_KNOWLEDGE",
        string? node = BaconSampleLedgerState.AdoptedNodeId, int lastFixed = 2, bool host = true, bool usable = true,
        bool resolved = false, bool saved = false, bool savedUsable = true, bool savedClosed = false) =>
        new(true, question, true, act, route, node, lastFixed, host, usable, resolved,
            saved, savedUsable, savedClosed, true);

    public static void Run()
    {
        Check(DiderotPublicPracticePolicy.ShouldInsert(At()),
            "The social-knowledge line meets Diderot at an act-two question node after Bacon.");
        Check(!DiderotPublicPracticePolicy.ShouldInsert(At(question: false)),
            "Diderot must not replace a non-question node.");
        Check(!DiderotPublicPracticePolicy.ShouldInsert(At(act: 0)),
            "Diderot must not appear in act one.");
        Check(!DiderotPublicPracticePolicy.ShouldInsert(At(act: 2)),
            "Diderot must not appear in act three.");
        Check(!DiderotPublicPracticePolicy.ShouldInsert(At(host: false)),
            "Diderot must wait for Bacon's fixed room.");
        Check(DiderotPublicPracticePolicy.ShouldInsert(At(lastFixed: 3)),
            "The real boundary advances the fixed slot to three before the extra question.");
        Check(!DiderotPublicPracticePolicy.ShouldInsert(At(lastFixed: 1)),
            "Diderot must not stand in for the act's fixed slot.");
        Check(!DiderotPublicPracticePolicy.ShouldInsert(At(node: "ROUSSEAU__HISTORY_AND_POWER__CORE")),
            "Diderot must appear on the figure the line reached, not on its entry.");
        Check(!DiderotPublicPracticePolicy.ShouldInsert(At(route: "SCIENTIFIC_MODEL")),
            "The scientific-model line does not reach Diderot here.");
        Check(!DiderotPublicPracticePolicy.ShouldInsert(At(resolved: true)),
            "A spent act-two question must not be replaced again.");
        Check(DiderotPublicPracticePolicy.ShouldInsert(At(saved: true, savedClosed: false)),
            "An open saved practice resumes.");
        Check(!DiderotPublicPracticePolicy.ShouldInsert(At(saved: true, savedClosed: true)),
            "A closed practice is not handed out twice.");

        // A write that lands between the two saves must be completed on reload.
        var state = DiderotPracticeState.Create("DIDEROT_PUBLIC_PRACTICE:TEST:2");
        Check(DiderotPracticeFlow.TryPrepare(state, DiderotPracticeAction.CollectIntoSystem),
            "Collecting the sample into a system must be selectable.");
        var reloaded = System.Text.Json.JsonSerializer.Deserialize<DiderotPracticeState>(
            System.Text.Json.JsonSerializer.Serialize(state))!;
        Check(reloaded.IsUsable() && reloaded.PendingAction == DiderotPracticeAction.CollectIntoSystem
            && DiderotPracticeFlow.Options(reloaded.Stage).Contains(reloaded.PendingAction),
            "A prepared choice must survive a reload and stay legal on its page.");
        DiderotPracticeFlow.Apply(reloaded, reloaded.PendingAction);
        reloaded.PendingAction = DiderotPracticeAction.None;
        Check(reloaded.Conclusion == DiderotPracticeConclusion.System,
            "Recovering the prepared choice must record its conclusion.");
        Check(DiderotPracticeFlow.TryPrepare(reloaded, DiderotPracticeAction.Leave),
            "The result page must still let the player leave.");
        DiderotPracticeFlow.Apply(reloaded, DiderotPracticeAction.Leave);
        Check(reloaded.Closed, "Leaving closes the practice.");
        Check(!DiderotPracticeFlow.TryPrepare(reloaded, DiderotPracticeAction.Leave),
            "A closed practice offers nothing further.");
    }
}
