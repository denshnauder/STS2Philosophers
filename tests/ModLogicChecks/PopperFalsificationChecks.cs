using System.Text.Json;
using STS2Philosophers;

internal static class PopperFalsificationChecks
{
    internal static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState NewJourney()
        {
            var j = graph.Start("ARISTOTLE__LANGUAGE_AND_MEANING__CORE");
            Check(graph.TryDecline(j, "Fixed", 2), "Popper fixture declines the second fixed slot.");
            Check(graph.TryApply(j, "WESTERN_EDGE_048"), "Popper fixture adopts Bacon 048.");
            return j;
        }

        var journey = NewJourney();
        var state = PopperFalsificationState.Create("POPPER_FALSIFICATION:TEST:3");
        Check(PopperFalsificationFlow.IsRouteConsistent(state, journey),
            "Popper opens at Bacon's terminal node after 048 with question 3 unused.");
        Check(PopperFalsificationFlow.TryPrepare(state, PopperFalsificationAction.SelectBoundaryTest)
            && PopperFalsificationFlow.TryCommitPending(state, journey)
            && state.Stage == PopperFalsificationStage.Evidence && state.FalsifiableTest,
            "Popper selects a test that can fail before the bridge is used.");
        Check(PopperFalsificationFlow.TryPrepare(state, PopperFalsificationAction.CalibrateAndReport)
            && PopperFalsificationFlow.TryCommitPending(state, journey)
            && state.Outcome == PopperFalsificationOutcome.Calibrated
            && state.CalibrationPaid && state.Cost == 12,
            "Popper calibration costs 12 gold and reports a bounded failure risk.");
        Check(PopperFalsificationFlow.TryPrepare(state, PopperFalsificationAction.Leave)
            && PopperFalsificationFlow.TryCommitPending(state, journey) && state.Closed,
            "Popper calibrated result closes.");

        var unfalsifiableJourney = NewJourney();
        var unfalsifiable = PopperFalsificationState.Create("POPPER_FALSIFICATION:TEST:3");
        Check(PopperFalsificationFlow.TryPrepare(unfalsifiable,
                PopperFalsificationAction.SelectUnfalsifiableTest)
            && PopperFalsificationFlow.TryCommitPending(unfalsifiable, unfalsifiableJourney)
            && unfalsifiable.Outcome == PopperFalsificationOutcome.Unfalsifiable
            && !unfalsifiable.FalsifiableTest && unfalsifiable.Cost == 0,
            "Popper records the unfalsifiable report without granting immunity.");

        var counterJourney = NewJourney();
        var counter = PopperFalsificationState.Create("POPPER_FALSIFICATION:TEST:3");
        Check(PopperFalsificationFlow.TryPrepare(counter, PopperFalsificationAction.SelectBoundaryTest)
            && PopperFalsificationFlow.TryCommitPending(counter, counterJourney)
            && PopperFalsificationFlow.TryPrepare(counter, PopperFalsificationAction.PublishCounterexample)
            && PopperFalsificationFlow.TryCommitPending(counter, counterJourney)
            && counter.Outcome == PopperFalsificationOutcome.Counterexample
            && counter.AuxiliaryUnknown && counter.Cost == 0,
            "Popper publishes a counterexample while marking auxiliary conditions unknown.");
        string encoded = JsonSerializer.Serialize(counter);
        PopperFalsificationState reloaded = JsonSerializer.Deserialize<PopperFalsificationState>(encoded)!;
        reloaded.NormalizeAfterLoad();
        Check(reloaded.IsUsable() && PopperFalsificationFlow.IsRouteConsistent(reloaded, counterJourney),
            "Popper counterexample state reloads at Bacon.");

        var notConclusiveJourney = NewJourney();
        var notConclusive = PopperFalsificationState.Create("POPPER_FALSIFICATION:TEST:3");
        Check(PopperFalsificationFlow.TryPrepare(notConclusive, PopperFalsificationAction.SelectBoundaryTest)
            && PopperFalsificationFlow.TryCommitPending(notConclusive, notConclusiveJourney)
            && PopperFalsificationFlow.TryPrepare(notConclusive, PopperFalsificationAction.RefuseFinalVerdict)
            && PopperFalsificationFlow.TryCommitPending(notConclusive, notConclusiveJourney)
            && notConclusive.Outcome == PopperFalsificationOutcome.NotConclusive
            && notConclusive.TheoryNotKilled && notConclusive.Cost == 0,
            "Popper refuses to treat one test as the theory's end.");

        var before = graph.Start("ARISTOTLE__LANGUAGE_AND_MEANING__CORE");
        var early = PopperFalsificationState.Create("POPPER_FALSIFICATION:TEST:3");
        Check(!PopperFalsificationFlow.IsRouteConsistent(early, before),
            "Popper cannot open before Bacon 048.");

        string[] keys =
        [
            "POPPER_FALSIFICATION.title",
            "POPPER_FALSIFICATION.pages.REPORTS.description",
            "POPPER_FALSIFICATION.pages.REPORTS.options.SELECT_BOUNDARY_TEST.title",
            "POPPER_FALSIFICATION.pages.REPORTS.options.SELECT_BOUNDARY_TEST.description",
            "POPPER_FALSIFICATION.pages.REPORTS.options.SELECT_UNFALSIFIABLE_TEST.title",
            "POPPER_FALSIFICATION.pages.REPORTS.options.SELECT_UNFALSIFIABLE_TEST.description",
            "POPPER_FALSIFICATION.pages.EVIDENCE.description",
            "POPPER_FALSIFICATION.pages.EVIDENCE.options.CALIBRATE_AND_REPORT.title",
            "POPPER_FALSIFICATION.pages.EVIDENCE.options.CALIBRATE_AND_REPORT.description",
            "POPPER_FALSIFICATION.pages.EVIDENCE.options.PUBLISH_COUNTEREXAMPLE.title",
            "POPPER_FALSIFICATION.pages.EVIDENCE.options.PUBLISH_COUNTEREXAMPLE.description",
            "POPPER_FALSIFICATION.pages.EVIDENCE.options.REFUSE_FINAL_VERDICT.title",
            "POPPER_FALSIFICATION.pages.EVIDENCE.options.REFUSE_FINAL_VERDICT.description",
            "POPPER_FALSIFICATION.pages.RESULT_UNFALSIFIABLE.description",
            "POPPER_FALSIFICATION.pages.RESULT_UNFALSIFIABLE.options.LEAVE.title",
            "POPPER_FALSIFICATION.pages.RESULT_UNFALSIFIABLE.options.LEAVE.description",
            "POPPER_FALSIFICATION.pages.RESULT_CALIBRATED.description",
            "POPPER_FALSIFICATION.pages.RESULT_CALIBRATED.options.LEAVE.title",
            "POPPER_FALSIFICATION.pages.RESULT_CALIBRATED.options.LEAVE.description",
            "POPPER_FALSIFICATION.pages.RESULT_COUNTEREXAMPLE.description",
            "POPPER_FALSIFICATION.pages.RESULT_COUNTEREXAMPLE.options.LEAVE.title",
            "POPPER_FALSIFICATION.pages.RESULT_COUNTEREXAMPLE.options.LEAVE.description",
            "POPPER_FALSIFICATION.pages.RESULT_NOT_CONCLUSIVE.description",
            "POPPER_FALSIFICATION.pages.RESULT_NOT_CONCLUSIVE.options.LEAVE.title",
            "POPPER_FALSIFICATION.pages.RESULT_NOT_CONCLUSIVE.options.LEAVE.description",
        ];
        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (string key in keys)
                Check(document.RootElement.TryGetProperty(key, out _),
                    $"Popper localization is missing {language}/{key}.");
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
