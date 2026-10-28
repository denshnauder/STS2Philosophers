using System.Text.Json;
using STS2Philosophers;

internal static class CarnapFrameworkChoiceChecks
{
    internal static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState NewJourney()
        {
            var j = graph.Start("ARISTOTLE__LANGUAGE_AND_MEANING__CORE");
            Check(graph.TryDecline(j, "Fixed", 2), "Carnap fixture declines the second fixed slot.");
            Check(graph.TryReserveQuestion(j, 2), "Carnap fixture resolves Ockham's second-act question.");
            return j;
        }

        var journey = NewJourney();
        var state = CarnapFrameworkChoiceState.Create("CARNAP_FRAMEWORK_CHOICE:TEST:2");
        Check(CarnapFrameworkChoiceFlow.IsRouteConsistent(state, journey),
            "Carnap opens at Aristotle after Ockham resolves question 2, before 029/030.");
        Check(CarnapFrameworkChoiceFlow.TryPrepare(state, CarnapFrameworkChoiceAction.ChooseObservation)
            && CarnapFrameworkChoiceFlow.TryCommitPending(state, journey)
            && state.Stage == CarnapFrameworkChoiceStage.Evidence
            && state.Framework == CarnapFramework.Observation,
            "Carnap records the observation-event framework.");
        Check(CarnapFrameworkChoiceFlow.TryPrepare(state, CarnapFrameworkChoiceAction.BuildTranslation)
            && CarnapFrameworkChoiceFlow.TryCommitPending(state, journey)
            && state.Outcome == CarnapFrameworkChoiceOutcome.Translated
            && state.TranslationBuilt && state.Cost == 15,
            "Carnap translation costs 15 gold and records the translation table.");
        Check(CarnapFrameworkChoiceFlow.TryPrepare(state, CarnapFrameworkChoiceAction.Leave)
            && CarnapFrameworkChoiceFlow.TryCommitPending(state, journey) && state.Closed,
            "Carnap translation result closes.");

        var limitedJourney = NewJourney();
        var limited = CarnapFrameworkChoiceState.Create("CARNAP_FRAMEWORK_CHOICE:TEST:2");
        Check(CarnapFrameworkChoiceFlow.TryPrepare(limited, CarnapFrameworkChoiceAction.ChooseRegion)
            && CarnapFrameworkChoiceFlow.TryCommitPending(limited, limitedJourney)
            && limited.Framework == CarnapFramework.Region
            && CarnapFrameworkChoiceFlow.TryPrepare(limited, CarnapFrameworkChoiceAction.PublishLimited)
            && CarnapFrameworkChoiceFlow.TryCommitPending(limited, limitedJourney)
            && limited.Outcome == CarnapFrameworkChoiceOutcome.Limited
            && limited.CrossFrameworkLimitShown && limited.Cost == 0,
            "Carnap limited publication is framework-relative and costs no gold.");
        limited.CostAttempted = true;
        limited.CostPaid = true;
        string encoded = JsonSerializer.Serialize(limited);
        CarnapFrameworkChoiceState reloaded = JsonSerializer.Deserialize<CarnapFrameworkChoiceState>(encoded)!;
        reloaded.NormalizeAfterLoad();
        Check(reloaded.IsUsable() && CarnapFrameworkChoiceFlow.IsRouteConsistent(reloaded, limitedJourney),
            "Carnap limited-publication state reloads on Aristotle.");

        var convenientJourney = NewJourney();
        var convenient = CarnapFrameworkChoiceState.Create("CARNAP_FRAMEWORK_CHOICE:TEST:2");
        Check(CarnapFrameworkChoiceFlow.TryPrepare(convenient, CarnapFrameworkChoiceAction.ChooseObservation)
            && CarnapFrameworkChoiceFlow.TryCommitPending(convenient, convenientJourney)
            && CarnapFrameworkChoiceFlow.TryPrepare(convenient, CarnapFrameworkChoiceAction.KeepConvenient)
            && CarnapFrameworkChoiceFlow.TryCommitPending(convenient, convenientJourney)
            && convenient.Outcome == CarnapFrameworkChoiceOutcome.Convenient
            && convenient.CrossFrameworkLimitShown && convenient.Cost == 0,
            "Carnap convenient framework records its limit without a universal claim.");

        var before = graph.Start("ARISTOTLE__LANGUAGE_AND_MEANING__CORE");
        var early = CarnapFrameworkChoiceState.Create("CARNAP_FRAMEWORK_CHOICE:TEST:2");
        Check(!CarnapFrameworkChoiceFlow.IsRouteConsistent(early, before),
            "Carnap cannot open before Ockham's question 2 is resolved.");

        string[] keys =
        [
            "CARNAP_FRAMEWORK_CHOICE.title",
            "CARNAP_FRAMEWORK_CHOICE.pages.RULES.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.RULES.options.CHOOSE_OBSERVATION.title",
            "CARNAP_FRAMEWORK_CHOICE.pages.RULES.options.CHOOSE_OBSERVATION.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.RULES.options.CHOOSE_REGION.title",
            "CARNAP_FRAMEWORK_CHOICE.pages.RULES.options.CHOOSE_REGION.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.EVIDENCE.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.EVIDENCE.options.BUILD_TRANSLATION.title",
            "CARNAP_FRAMEWORK_CHOICE.pages.EVIDENCE.options.BUILD_TRANSLATION.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.EVIDENCE.options.PUBLISH_LIMITED.title",
            "CARNAP_FRAMEWORK_CHOICE.pages.EVIDENCE.options.PUBLISH_LIMITED.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.EVIDENCE.options.KEEP_CONVENIENT.title",
            "CARNAP_FRAMEWORK_CHOICE.pages.EVIDENCE.options.KEEP_CONVENIENT.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.RESULT_TRANSLATED.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.RESULT_TRANSLATED.options.LEAVE.title",
            "CARNAP_FRAMEWORK_CHOICE.pages.RESULT_TRANSLATED.options.LEAVE.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.RESULT_LIMITED.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.RESULT_LIMITED.options.LEAVE.title",
            "CARNAP_FRAMEWORK_CHOICE.pages.RESULT_LIMITED.options.LEAVE.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.RESULT_CONVENIENT.description",
            "CARNAP_FRAMEWORK_CHOICE.pages.RESULT_CONVENIENT.options.LEAVE.title",
            "CARNAP_FRAMEWORK_CHOICE.pages.RESULT_CONVENIENT.options.LEAVE.description",
        ];
        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (string key in keys)
                Check(document.RootElement.TryGetProperty(key, out _),
                    $"Carnap localization is missing {language}/{key}.");
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
