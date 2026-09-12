using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class WesternGraphChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        Check(graph.Samples.Count == 12, "All twelve sample sequences must be traversable.");
        foreach (WesternGraphSample sample in graph.Samples)
        {
            WesternJourneyState state = graph.Start(sample.EntryNodeId);
            Check(graph.CanFinish(state), "Every entry problem must have a complete three-act path.");
            foreach (string id in sample.EdgeIds)
            {
                WesternGraphEdge edge = graph.GetEdge(id);
                string previous = state.CurrentNodeId;
                Check(graph.GetEligibleEdges(state, edge.Slot).Any(candidate => candidate.EdgeId == id), "Each sample's next step must be available after its own prerequisites.");
                Check(graph.TryApply(state, id), "A valid edge must apply once.");
                Check(!graph.TryApply(state, id), "Repeated callbacks must not consume an edge twice.");
                if (edge.Outcome is "Keep" or "Revise") Check(state.CurrentNodeId == previous, "A question that keeps or revises the position must not silently switch thinkers.");
                else Check(state.CurrentNodeId == edge.ToNodeId, "Adoption and switching must change the next query origin.");
                state = JsonSerializer.Deserialize<WesternJourneyState>(JsonSerializer.Serialize(state))!;
            }
            Check(graph.IsComplete(state), "Each sequence must reach a legitimate third-act conclusion after save/restore at every step.");
            Check(graph.GetEligibleEdges(state, "Fixed").Count == 0, "Fourth-act echoes must remain unavailable.");
        }
        WesternGraphSample first = graph.Samples[0];
        WesternJourneyState unchallenged = graph.Start(first.EntryNodeId);
        Check(!graph.TryApply(unchallenged, first.EdgeIds[1]), "An intervening challenge cannot be erased into a direct succession edge.");
        Check(!graph.TryApply(unchallenged, "NONEXISTENT"), "Unknown edge IDs must not change state.");
        Check(unchallenged.CompletedEdgeIds.Count == 0 && unchallenged.SeenThinkerIds.Count == 1, "Rejected actions must have no side effects.");
        Check(!graph.TryAcceptOffered(unchallenged, "Question", first.EdgeIds[0]), "Eligible but undisplayed actions cannot be accepted through the UI gate.");
        IReadOnlyList<string> offered = graph.OfferCandidates(unchallenged, "Question", 7);
        Check(offered.Count is > 0 and <= 3 && offered.Select(id => graph.GetNode(graph.GetEdge(id).ToNodeId).ThinkerId).Distinct().Count() == offered.Count,
            "Offers must contain at most three distinct people, even when sample paths share a challenge.");
        Check(offered.SequenceEqual(graph.OfferCandidates(unchallenged, "Question", 999)), "Reopening a candidate window cannot reroll it.");
        WesternJourneyState restored = JsonSerializer.Deserialize<WesternJourneyState>(JsonSerializer.Serialize(unchallenged))!;
        Check(offered.SequenceEqual(graph.OfferCandidates(restored, "Question", 0)), "Visible candidates must survive save and restore.");
        Check(graph.TryAcceptOffered(restored, "Question", offered[0]) && !graph.TryAcceptOffered(restored, "Question", offered[0]), "An offered choice must be consumable only once.");
        Check(graph.GetEligibleEdges(restored, "Question").Count == 0 && graph.CanFinish(restored), "A consumed question slot stays closed while a legal ending remains reachable.");

        string json = File.ReadAllText("config/western_graph.json");
        foreach (string defect in new[] { "low", "missing_target", "cycle_requirement", "fourth_act" })
        {
            JsonObject root = JsonNode.Parse(json)!.AsObject();
            JsonObject edge = root["edges"]!.AsArray().Select(node => node!.AsObject()).First(node => node["slot"]!.GetValue<string>() == "Fixed");
            switch (defect)
            {
                case "low": edge["confidence"] = "Low"; break;
                case "missing_target": edge["to_node_id"] = "ABSENT"; break;
                case "cycle_requirement": edge["required_edge_ids"] = new JsonArray(edge["edge_id"]!.GetValue<string>()); break;
                case "fourth_act": edge["act"] = 4; break;
            }
            bool rejected = false;
            try { WesternRouteGraph.ParseJson(root.ToJsonString()); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, $"Invalid graph mutation must be rejected: {defect}");
        }
        Console.WriteLine("Western graph checks passed: twelve three-act paths, prerequisites, question outcomes, repeat gates and save/restore.");
    }
}
