using System.Text.Json;
using STS2Philosophers;

internal static class SchopenhauerWillDenialChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start(LockeCargoIdentityState.SourceNodeId);
        Check(graph.TryApply(journey, LockeCargoIdentityState.EdgeId), "Locke 026 must be completed first.");
        Check(graph.TryApply(journey, HumeInductionState.SecondActEdgeId), "Hume must be reached in the second act first.");
        var kant = KantApperceptionState.Create("KANT_APPERCEPTION:TEST:3");
        var shared = new PhilosophyRunState { WesternJourney = journey, KantApperception = kant };
        foreach (var action in new[] { KantApperceptionAction.InspectLedger, KantApperceptionAction.Constitutive, KantApperceptionAction.Leave })
        {
            Check(KantApperceptionFlow.TryPrepare(kant, action), $"Prepare Kant {action}.");
            Check(KantApperceptionFlow.TryCommitPending(kant, journey, graph), $"Commit Kant {action}.");
        }
        Check(kant.Closed && journey.CurrentNodeId == KantApperceptionState.AdoptedNodeId,
            "Kant 028 closes at the Kant self node.");
        return shared;
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        SchopenhauerWillDenialAction action)
    {
        Check(SchopenhauerWillDenialFlow.TryPrepare(shared.SchopenhauerWillDenial!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(SchopenhauerWillDenialFlow.TryCommitPending(shared.SchopenhauerWillDenial!,
            shared.WesternJourney!, shared.KantApperception!, graph), $"Commit {action}.");
        Check(!SchopenhauerWillDenialFlow.TryCommitPending(shared.SchopenhauerWillDenial!,
            shared.WesternJourney!, shared.KantApperception!, graph), "A confirmed Schopenhauer action cannot replay.");
        shared = Reload(shared);
        Check(SchopenhauerWillDenialFlow.IsRouteConsistent(shared.SchopenhauerWillDenial!,
            shared.WesternJourney!, shared.KantApperception!), "Schopenhauer survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture(graph);
        var state = SchopenhauerWillDenialState.Create("SCHOPENHAUER_WILL_DENIAL:TEST:3",
            shared.KantApperception!.InstanceId);
        shared.SchopenhauerWillDenial = state;
        Check(SchopenhauerWillDenialFlow.IsRouteConsistent(state, shared.WesternJourney!, shared.KantApperception!),
            "Schopenhauer opens at Kant's self node after 028.");
        Check(!shared.WesternJourney!.ResolvedQuestionActs.Contains(3),
            "The act-three question is still open before the first choice.");

        shared = Commit(shared, graph, SchopenhauerWillDenialAction.AskPorter);
        Check(shared.WesternJourney!.ResolvedQuestionActs.Contains(3)
            && shared.SchopenhauerWillDenial!.QuestionReserved
            && !shared.SchopenhauerWillDenial.ExtraDemandAccepted,
            "Asking the porter reserves the act-three question without accepting the extra demand.");
        shared = Commit(shared, graph, SchopenhauerWillDenialAction.PayCompensation);
        Check(shared.SchopenhauerWillDenial!.Outcome == SchopenhauerWillDenialOutcome.PaidCompensation
            && shared.SchopenhauerWillDenial.Cost == 8m
            && !shared.SchopenhauerWillDenial.CostPaid,
            "Compensation records its named cost before payment.");
        shared.SchopenhauerWillDenial.CostAttempted = true;
        shared.SchopenhauerWillDenial.CostPaid = true;
        shared = Reload(shared);
        Check(SchopenhauerWillDenialFlow.IsRouteConsistent(shared.SchopenhauerWillDenial!,
            shared.WesternJourney!, shared.KantApperception!), "A paid Schopenhauer record survives reload.");
        shared = Commit(shared, graph, SchopenhauerWillDenialAction.Leave);
        Check(shared.SchopenhauerWillDenial!.Closed, "Schopenhauer closes after Leave.");

        shared = Fixture(graph);
        var refused = SchopenhauerWillDenialState.Create("SCHOPENHAUER_WILL_DENIAL:TEST:3",
            shared.KantApperception!.InstanceId);
        shared.SchopenhauerWillDenial = refused;
        shared = Commit(shared, graph, SchopenhauerWillDenialAction.BuyExtra);
        Check(shared.SchopenhauerWillDenial!.ExtraDemandAccepted,
            "Buying the extra crate records the accepted visible demand.");
        shared = Commit(shared, graph, SchopenhauerWillDenialAction.RefuseProfit);
        Check(shared.SchopenhauerWillDenial!.Outcome == SchopenhauerWillDenialOutcome.RefusedProfit
            && shared.SchopenhauerWillDenial.Cost == 15m,
            "Refusing profit keeps its distinct cost.");

        shared = new PhilosophyRunState
        {
            WesternJourney = graph.Start(LockeCargoIdentityState.SourceNodeId),
            KantApperception = KantApperceptionState.Create("KANT_APPERCEPTION:TEST:3"),
        };
        var withoutKant = SchopenhauerWillDenialState.Create("SCHOPENHAUER_WILL_DENIAL:TEST:3",
            shared.KantApperception.InstanceId);
        Check(!SchopenhauerWillDenialFlow.IsRouteConsistent(withoutKant,
            shared.WesternJourney!, shared.KantApperception),
            "Schopenhauer cannot open before Kant 028 is closed.");

        var corruptExtra = SchopenhauerWillDenialState.Create("SCHOPENHAUER_WILL_DENIAL:TEST:3",
            shared.KantApperception.InstanceId);
        corruptExtra.ExtraDemandAccepted = true;
        Check(!corruptExtra.IsUsable(),
            "Schopenhauer rejects an accepted extra demand before any invitation action.");

        var corruptQuestion = SchopenhauerWillDenialState.Create("SCHOPENHAUER_WILL_DENIAL:TEST:3",
            shared.KantApperception.InstanceId);
        corruptQuestion.QuestionReserved = true;
        Check(!corruptQuestion.IsUsable(),
            "Schopenhauer rejects a reserved question without a committed action.");

        var corruptOutcome = SchopenhauerWillDenialState.Create("SCHOPENHAUER_WILL_DENIAL:TEST:3",
            shared.KantApperception.InstanceId);
        corruptOutcome.Outcome = SchopenhauerWillDenialOutcome.PaidCompensation;
        Check(!corruptOutcome.IsUsable(),
            "Schopenhauer rejects a paid-compensation outcome recorded on the invitation page.");

        var corruptClosed = SchopenhauerWillDenialState.Create("SCHOPENHAUER_WILL_DENIAL:TEST:3",
            shared.KantApperception.InstanceId);
        corruptClosed.Closed = true;
        Check(!corruptClosed.IsUsable(),
            "Schopenhauer rejects a closed invitation with no outcome.");

        string[] keys =
        [
            "SCHOPENHAUER_WILL_DENIAL.title",
            "SCHOPENHAUER_WILL_DENIAL.pages.INVITATION.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.INVITATION.options.BUY_EXTRA.title",
            "SCHOPENHAUER_WILL_DENIAL.pages.INVITATION.options.BUY_EXTRA.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.INVITATION.options.ASK_PORTER.title",
            "SCHOPENHAUER_WILL_DENIAL.pages.INVITATION.options.ASK_PORTER.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.EVIDENCE.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.EVIDENCE.options.REFUSE_PROFIT.title",
            "SCHOPENHAUER_WILL_DENIAL.pages.EVIDENCE.options.REFUSE_PROFIT.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.EVIDENCE.options.PAY_COMPENSATION.title",
            "SCHOPENHAUER_WILL_DENIAL.pages.EVIDENCE.options.PAY_COMPENSATION.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.EVIDENCE.options.SHARE_LABOR.title",
            "SCHOPENHAUER_WILL_DENIAL.pages.EVIDENCE.options.SHARE_LABOR.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.RESULT_REFUSED.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.RESULT_REFUSED.options.LEAVE.title",
            "SCHOPENHAUER_WILL_DENIAL.pages.RESULT_REFUSED.options.LEAVE.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.RESULT_COMPENSATED.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.RESULT_COMPENSATED.options.LEAVE.title",
            "SCHOPENHAUER_WILL_DENIAL.pages.RESULT_COMPENSATED.options.LEAVE.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.RESULT_SHARED.description",
            "SCHOPENHAUER_WILL_DENIAL.pages.RESULT_SHARED.options.LEAVE.title",
            "SCHOPENHAUER_WILL_DENIAL.pages.RESULT_SHARED.options.LEAVE.description",
        ];
        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (string key in keys)
                Check(document.RootElement.TryGetProperty(key, out _),
                    $"Schopenhauer localization is missing {language}/{key}.");
        }
    }
}
