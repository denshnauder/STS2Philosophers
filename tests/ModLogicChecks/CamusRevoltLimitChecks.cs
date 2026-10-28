using System.Text.Json;
using STS2Philosophers;

internal static class CamusRevoltLimitChecks
{
    internal static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState NewJourney()
        {
            var j = graph.Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
            Check(graph.TryApply(j, "WESTERN_EDGE_008"), "Camus fixture completes Socrates 008.");
            Check(graph.TryApply(j, "WESTERN_EDGE_009"), "Camus fixture adopts Plato 009.");
            Check(graph.TryApply(j, "WESTERN_EDGE_010"), "Camus fixture switches to Diogenes 010.");
            Check(graph.TryApply(j, "WESTERN_EDGE_011"), "Camus fixture adopts Zeno 011.");
            return j;
        }
        var journey = NewJourney();

        var state = CamusRevoltLimitState.Create("CAMUS_REVOLT_LIMIT:TEST:3");
        Check(CamusRevoltLimitFlow.IsRouteConsistent(state, journey),
            "Camus opens at Zeno's terminal node after 011 with question 3 unused.");
        Check(CamusRevoltLimitFlow.TryPrepare(state, CamusRevoltLimitAction.AcknowledgeLevy)
            && CamusRevoltLimitFlow.TryCommitPending(state, journey, graph)
            && state.Stage == CamusRevoltLimitStage.Proposal && state.LevyAcknowledged
            && state.QuestionReserved && journey.ResolvedQuestionActs.Contains(3),
            "Camus reserves the third-act question and records the acknowledged levy.");
        Check(CamusRevoltLimitFlow.TryPrepare(state, CamusRevoltLimitAction.RefuseHostage)
            && CamusRevoltLimitFlow.TryCommitPending(state, journey, graph)
            && state.Outcome == CamusRevoltLimitOutcome.Restrained && state.Cost == 6
            && !state.ThirdPartyHarmed,
            "Camus restraint refuses the hostage and records the 6-gold cost.");
        Check(CamusRevoltLimitFlow.TryPrepare(state, CamusRevoltLimitAction.Leave)
            && CamusRevoltLimitFlow.TryCommitPending(state, journey, graph) && state.Closed,
            "Camus restraint result closes.");
        Check(state.IsUsable(), "Camus restraint state replays after close.");

        var withdrawnJourney = NewJourney();
        var withdrawn = CamusRevoltLimitState.Create("CAMUS_REVOLT_LIMIT:TEST:3");
        Check(CamusRevoltLimitFlow.TryPrepare(withdrawn, CamusRevoltLimitAction.QuestionLevy)
            && CamusRevoltLimitFlow.TryCommitPending(withdrawn, withdrawnJourney, graph)
            && withdrawn.LevyQuestioned
            && CamusRevoltLimitFlow.TryPrepare(withdrawn, CamusRevoltLimitAction.WithdrawAid)
            && CamusRevoltLimitFlow.TryCommitPending(withdrawn, withdrawnJourney, graph)
            && withdrawn.Outcome == CamusRevoltLimitOutcome.Withdrawn && withdrawn.Cost == 4,
            "Camus withdrawal questions the levy, aids the traveler and costs 4 gold.");
        withdrawn.CostAttempted = true;
        withdrawn.CostPaid = true;
        string encoded = JsonSerializer.Serialize(withdrawn);
        CamusRevoltLimitState reloaded = JsonSerializer.Deserialize<CamusRevoltLimitState>(encoded)!;
        reloaded.NormalizeAfterLoad();
        Check(reloaded.IsUsable() && reloaded.Cost == 4
            && CamusRevoltLimitFlow.IsRouteConsistent(reloaded, withdrawnJourney),
            "Camus withdrawal state reloads with its reserved question.");

        var escalatedJourney = NewJourney();
        var escalated = CamusRevoltLimitState.Create("CAMUS_REVOLT_LIMIT:TEST:3");
        Check(CamusRevoltLimitFlow.TryPrepare(escalated, CamusRevoltLimitAction.AcknowledgeLevy)
            && CamusRevoltLimitFlow.TryCommitPending(escalated, escalatedJourney, graph)
            && CamusRevoltLimitFlow.TryPrepare(escalated, CamusRevoltLimitAction.AllowEscalation)
            && CamusRevoltLimitFlow.TryCommitPending(escalated, escalatedJourney, graph)
            && escalated.Outcome == CamusRevoltLimitOutcome.Escalated
            && escalated.ThirdPartyHarmed && escalated.Cost == 0,
            "Camus escalation records third-party harm without a combat reward.");

        var before = graph.Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
        Check(graph.TryApply(before, "WESTERN_EDGE_008") && graph.TryApply(before, "WESTERN_EDGE_009")
            && graph.TryApply(before, "WESTERN_EDGE_010"),
            "Camus negative fixture reaches Diogenes without 011.");
        var early = CamusRevoltLimitState.Create("CAMUS_REVOLT_LIMIT:TEST:3");
        Check(!CamusRevoltLimitFlow.IsRouteConsistent(early, before),
            "Camus cannot open before Zeno 011 is complete.");

        string[] keys =
        [
            "CAMUS_REVOLT_LIMIT.title",
            "CAMUS_REVOLT_LIMIT.pages.SITUATION.description",
            "CAMUS_REVOLT_LIMIT.pages.SITUATION.options.ACKNOWLEDGE_LEVY.title",
            "CAMUS_REVOLT_LIMIT.pages.SITUATION.options.ACKNOWLEDGE_LEVY.description",
            "CAMUS_REVOLT_LIMIT.pages.SITUATION.options.QUESTION_LEVY.title",
            "CAMUS_REVOLT_LIMIT.pages.SITUATION.options.QUESTION_LEVY.description",
            "CAMUS_REVOLT_LIMIT.pages.PROPOSAL.description",
            "CAMUS_REVOLT_LIMIT.pages.PROPOSAL.options.REFUSE_HOSTAGE.title",
            "CAMUS_REVOLT_LIMIT.pages.PROPOSAL.options.REFUSE_HOSTAGE.description",
            "CAMUS_REVOLT_LIMIT.pages.PROPOSAL.options.WITHDRAW_AID.title",
            "CAMUS_REVOLT_LIMIT.pages.PROPOSAL.options.WITHDRAW_AID.description",
            "CAMUS_REVOLT_LIMIT.pages.PROPOSAL.options.ALLOW_ESCALATION.title",
            "CAMUS_REVOLT_LIMIT.pages.PROPOSAL.options.ALLOW_ESCALATION.description",
            "CAMUS_REVOLT_LIMIT.pages.RESULT_RESTRAINED.description",
            "CAMUS_REVOLT_LIMIT.pages.RESULT_RESTRAINED.options.LEAVE.title",
            "CAMUS_REVOLT_LIMIT.pages.RESULT_RESTRAINED.options.LEAVE.description",
            "CAMUS_REVOLT_LIMIT.pages.RESULT_WITHDRAWN.description",
            "CAMUS_REVOLT_LIMIT.pages.RESULT_WITHDRAWN.options.LEAVE.title",
            "CAMUS_REVOLT_LIMIT.pages.RESULT_WITHDRAWN.options.LEAVE.description",
            "CAMUS_REVOLT_LIMIT.pages.RESULT_ESCALATED.description",
            "CAMUS_REVOLT_LIMIT.pages.RESULT_ESCALATED.options.LEAVE.title",
            "CAMUS_REVOLT_LIMIT.pages.RESULT_ESCALATED.options.LEAVE.description",
        ];
        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (string key in keys)
                Check(document.RootElement.TryGetProperty(key, out _),
                    $"Camus localization is missing {language}/{key}.");
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
