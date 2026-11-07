using System.Text.Json;
using STS2Philosophers;

internal static class DeweyPublicExperimentChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = DeweyPublicExperimentState.SourceNodeId,
        LastFixedAct = 3,
        CompletedEdgeIds = new(StringComparer.Ordinal) { DeweyPublicExperimentState.RequiredEdgeId },
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = DeweyPublicExperimentState.Create("DEWEY_PUBLIC_EXPERIMENT:TEST:1");
        Check(DeweyPublicExperimentFlow.Options(state.Stage).Count == 3,
            "Marx must offer a public experiment, keeping the inquiry community, or keeping Peirce.");
        Check(DeweyPublicExperimentFlow.TryPrepare(state, DeweyPublicExperimentAction.PublicExperiment),
            "Making it public must be selectable.");
        var journey = Journey();
        Check(DeweyPublicExperimentFlow.IsRouteConsistent(state, journey),
            "The room must sit on the third-act question slot after Peirce.");
        Check(DeweyPublicExperimentFlow.TryCommitPending(state, journey, graph),
            "Making it public must take the third-act question.");
        Check(state.Standard == DeweyPublicExperimentStandard.Experiment && state.EdgeCommitted
            && state.Stage == DeweyPublicExperimentStage.ResultExperiment,
            "Alienation must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(DeweyPublicExperimentState.CompletionEdgeId)
            && journey.CurrentNodeId == DeweyPublicExperimentState.AdoptedNodeId
            && journey.ResolvedQuestionActs.Contains(3),
            "Adopting must switch the journey to Marx and resolve act three's question.");

        var kept = DeweyPublicExperimentState.Create("DEWEY_PUBLIC_EXPERIMENT:TEST:2");
        DeweyPublicExperimentFlow.TryPrepare(kept, DeweyPublicExperimentAction.KeepPeirce);
        var declined = Journey();
        Check(DeweyPublicExperimentFlow.TryCommitPending(kept, declined, graph),
            "Keeping Peirce must decline the act's question.");
        Check(declined.ResolvedQuestionActs.Contains(3)
            && declined.CurrentNodeId == DeweyPublicExperimentState.SourceNodeId,
            "Declining must leave the player on Peirce's node.");


        var fixedState = DeweyPublicExperimentState.CreateAt("DEWEY_PUBLIC_EXPERIMENT:TEST:4", 3, "WESTERN_EDGE_109");
        fixedState.RoutePlaced = true;
        var fixedJourney = new WesternJourneyState
        {
            CurrentNodeId = DeweyPublicExperimentState.SourceNodeId,
            RouteId = "FALLIBLE_INQUIRY",
            LastFixedAct = 3,
            CompletedEdgeIds = new(StringComparer.Ordinal) { "WESTERN_EDGE_108" },
        };
        Check(DeweyPublicExperimentFlow.IsRouteConsistent(fixedState, fixedJourney),
            "The default Dewey room must sit on the third-act fixed slot.");
        DeweyPublicExperimentFlow.TryPrepare(fixedState, DeweyPublicExperimentAction.PublicExperiment);
        Check(DeweyPublicExperimentFlow.TryCommitPending(fixedState, fixedJourney, graph),
            "The default Dewey choice must take the third-act fixed link.");
        Check(fixedJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_109")
            && fixedJourney.CurrentNodeId == DeweyPublicExperimentState.AdoptedNodeId
            && fixedJourney.LastFixedAct == 3,
            "Adopting on the default line must move to Dewey.");

        var tampered = DeweyPublicExperimentState.Create("DEWEY_PUBLIC_EXPERIMENT:TEST:5");
        tampered.Standard = DeweyPublicExperimentStandard.Community;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");

        foreach (var (action, stage) in new[]
        {
            (DeweyPublicExperimentAction.PublicExperiment, DeweyPublicExperimentStage.ResultExperiment),
            (DeweyPublicExperimentAction.KeepInquiryCommunity, DeweyPublicExperimentStage.ResultCommunity),
            (DeweyPublicExperimentAction.KeepPeirce, DeweyPublicExperimentStage.ResultKept)
        })
        {
            var page = DeweyPublicExperimentState.Create("DEWEY_PUBLIC_EXPERIMENT:PAGE:" + stage);
            DeweyPublicExperimentFlow.Apply(page, action);
            string key = $"DEWEY_PUBLIC_EXPERIMENT.pages.{DeweyPublicExperimentFlow.DescriptionKey(page)}.description";
            foreach (string language in new[] { "eng", "zhs" })
            {
                using JsonDocument text = JsonDocument.Parse(File.ReadAllText(
                    $"content/STS2Philosophers/localization/{language}/events.json"));
                Check(text.RootElement.TryGetProperty(key, out _),
                    $"DEWEY_PUBLIC_EXPERIMENT page key {key} must exist in {language}.");
            }
        }
    }
}
