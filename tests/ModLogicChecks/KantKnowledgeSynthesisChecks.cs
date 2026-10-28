using System.Text.Json;
using STS2Philosophers;

internal static class KantKnowledgeSynthesisChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static WesternJourneyState Fixture(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_012"), "Kant knowledge fixture resolves Sextus's first question.");
        Check(graph.TryApply(journey, "WESTERN_EDGE_013"), "Kant knowledge fixture adopts Descartes's second fixed act.");
        Check(graph.TryApply(journey, "WESTERN_EDGE_014"), "Kant knowledge fixture resolves Hume's second-act question.");
        Check(journey.CurrentNodeId == KantKnowledgeSynthesisState.SourceNodeId,
            "The Hume question is a revision edge and keeps the route at Descartes.");
        return journey;
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        KantKnowledgeSynthesisAction action)
    {
        Check(KantKnowledgeSynthesisFlow.TryPrepare(shared.KantKnowledgeSynthesis!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(KantKnowledgeSynthesisFlow.TryCommitPending(shared.KantKnowledgeSynthesis!,
            shared.WesternJourney!, graph), $"Commit {action}.");
        Check(!KantKnowledgeSynthesisFlow.TryCommitPending(shared.KantKnowledgeSynthesis!,
            shared.WesternJourney!, graph), "A confirmed Kant knowledge action cannot replay.");
        shared = Reload(shared);
        Check(KantKnowledgeSynthesisFlow.IsRouteConsistent(shared.KantKnowledgeSynthesis!,
            shared.WesternJourney!), "Kant knowledge survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var shared = new PhilosophyRunState
        {
            WesternJourney = Fixture(graph),
            KantKnowledgeSynthesis = KantKnowledgeSynthesisState.Create("KANT_KNOWLEDGE_SYNTHESIS:TEST:3"),
        };
        Check(KantKnowledgeSynthesisFlow.IsRouteConsistent(shared.KantKnowledgeSynthesis!,
            shared.WesternJourney!), "Kant knowledge opens at Descartes after Hume's revision question.");

        shared = Commit(shared, graph, KantKnowledgeSynthesisAction.InspectHume);
        Check(shared.KantKnowledgeSynthesis!.EvidenceChecked
            && shared.KantKnowledgeSynthesis.Stage == KantKnowledgeSynthesisStage.Evidence,
            "The Humean record is inspected before judgment.");
        shared = Commit(shared, graph, KantKnowledgeSynthesisAction.Synthesize);
        var synthesized = shared.KantKnowledgeSynthesis!;
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(KantKnowledgeSynthesisState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == KantKnowledgeSynthesisState.AdoptedNodeId
            && shared.WesternJourney.LastFixedAct == 3
            && synthesized.Judgment == KantKnowledgeJudgment.Synthetic,
            "Synthetic a priori synthesis completes 015 and enters Kant's knowledge node.");
        shared = Commit(shared, graph, KantKnowledgeSynthesisAction.Leave);
        Check(shared.KantKnowledgeSynthesis!.Closed, "Kant knowledge closes after Leave.");

        var limited = new PhilosophyRunState
        {
            WesternJourney = Fixture(graph),
            KantKnowledgeSynthesis = KantKnowledgeSynthesisState.Create("KANT_KNOWLEDGE_SYNTHESIS:TEST:3"),
        };
        limited = Commit(limited, graph, KantKnowledgeSynthesisAction.InspectHume);
        limited = Commit(limited, graph, KantKnowledgeSynthesisAction.DrawLimit);
        Check(limited.WesternJourney!.CompletedEdgeIds.Contains(KantKnowledgeSynthesisState.CompletionEdgeId)
            && limited.KantKnowledgeSynthesis!.Judgment == KantKnowledgeJudgment.Limit,
            "Drawing the limit still enters Kant's knowledge node without a universal reward.");

        var declined = new PhilosophyRunState
        {
            WesternJourney = Fixture(graph),
            KantKnowledgeSynthesis = KantKnowledgeSynthesisState.Create("KANT_KNOWLEDGE_SYNTHESIS:TEST:3"),
        };
        declined = Commit(declined, graph, KantKnowledgeSynthesisAction.KeepHume);
        Check(declined.WesternJourney!.DeclinedFixedActs.Contains(3)
            && !declined.WesternJourney.CompletedEdgeIds.Contains(KantKnowledgeSynthesisState.CompletionEdgeId)
            && declined.WesternJourney.CurrentNodeId == KantKnowledgeSynthesisState.SourceNodeId
            && declined.WesternJourney.LastFixedAct == 3,
            "Declining Kant's knowledge event spends only the act-three fixed slot.");

        var early = new PhilosophyRunState
        {
            WesternJourney = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE"),
            KantKnowledgeSynthesis = KantKnowledgeSynthesisState.Create("KANT_KNOWLEDGE_SYNTHESIS:TEST:3"),
        };
        Check(!KantKnowledgeSynthesisFlow.IsRouteConsistent(early.KantKnowledgeSynthesis!,
            early.WesternJourney!), "Kant knowledge cannot open without Hume's revision question.");

        var corrupt = KantKnowledgeSynthesisState.Create("KANT_KNOWLEDGE_SYNTHESIS:TEST:3");
        corrupt.EvidenceChecked = true;
        Check(!corrupt.IsUsable(),
            "Kant knowledge rejects an inspected record on the untouched invitation page.");

        string[] keys =
        [
            "KANT_KNOWLEDGE_SYNTHESIS.title",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.INVITATION.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.INVITATION.options.INSPECT_HUME.title",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.INVITATION.options.INSPECT_HUME.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.INVITATION.options.KEEP_HUME.title",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.INVITATION.options.KEEP_HUME.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.EVIDENCE.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.EVIDENCE.options.SYNTHESIZE.title",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.EVIDENCE.options.SYNTHESIZE.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.EVIDENCE.options.DRAW_LIMIT.title",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.EVIDENCE.options.DRAW_LIMIT.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.RESULT_SYNTHETIC.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.RESULT_SYNTHETIC.options.LEAVE.title",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.RESULT_SYNTHETIC.options.LEAVE.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.RESULT_LIMIT.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.RESULT_LIMIT.options.LEAVE.title",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.RESULT_LIMIT.options.LEAVE.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.RESULT_KEPT.description",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.RESULT_KEPT.options.LEAVE.title",
            "KANT_KNOWLEDGE_SYNTHESIS.pages.RESULT_KEPT.options.LEAVE.description",
        ];
        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument text = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (string key in keys)
            {
                Check(text.RootElement.TryGetProperty(key, out JsonElement value)
                    && !string.IsNullOrWhiteSpace(value.GetString()),
                    $"Kant knowledge localization key is missing in {language}: {key}");
            }
        }
    }
}
