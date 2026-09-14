using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class WesternGraphRevisionChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        string json = File.ReadAllText("config/western_graph.json");
        WesternRouteGraph revised = WesternRouteGraph.LoadEmbedded();
        string[] retiredIds = ["WESTERN_EDGE_002", "WESTERN_EDGE_004", "WESTERN_EDGE_031", "WESTERN_EDGE_032", "WESTERN_EDGE_036"];
        Check(revised.Samples.Where(s => s.Disposition == "SourceOnly").Select(s => s.SampleId)
            .SequenceEqual(["SAMPLE_01", "SAMPLE_09", "SAMPLE_10", "SAMPLE_11"]), "The four withdrawn sequences must remain identifiable source inputs.");

        // Reconstruct the previous graph's execution permissions, retaining the same IDs and payload shapes.
        JsonObject oldData = JsonNode.Parse(json)!.AsObject();
        foreach (JsonNode? item in oldData["edges"]!.AsArray())
        {
            JsonObject edge = item!.AsObject();
            if (!retiredIds.Contains(edge["edge_id"]!.GetValue<string>())) continue;
            edge["enabled"] = true;
            edge.Remove("retirement_reason");
        }
        foreach (JsonNode? item in oldData["samples"]!.AsArray())
        {
            item!["disposition"] = "Executable";
            item["review_note"] = "";
        }
        WesternRouteGraph previous = WesternRouteGraph.ParseJson(oldData.ToJsonString());
        foreach (string id in retiredIds)
        {
            WesternGraphEdge edge = revised.GetEdge(id);
            Check(!edge.Enabled && !string.IsNullOrWhiteSpace(edge.RetirementReason), "A withdrawn edge must carry its design reason.");
            WesternJourneyState before = new()
            {
                CurrentNodeId = edge.FromNodeId,
                LastFixedAct = edge.Slot == "Fixed" ? edge.Act - 1 : edge.Act,
                SeenThinkerIds = [previous.GetNode(edge.FromNodeId).ThinkerId],
                CompletedEdgeIds = new(edge.RequiredEdgeIds, StringComparer.Ordinal),
            };
            IReadOnlyList<string> oldOffer = previous.OfferCandidates(before, edge.Slot, 0);
            Check(oldOffer.Contains(id), $"The fixture must reproduce the former invitation: {id}");
            WesternJourneyState restored = JsonSerializer.Deserialize<WesternJourneyState>(JsonSerializer.Serialize(before))!;
            string snapshot = JsonSerializer.Serialize(restored);
            Check(!revised.TryAcceptOffered(restored, edge.Slot, id) && !revised.TryApply(restored, id),
                "A saved invitation cannot bypass retirement, including direct callback application.");
            Check(JsonSerializer.Serialize(restored) == snapshot, "Rejected old choices must not mutate the saved journey.");
            IReadOnlyList<string> refreshed = revised.OfferCandidates(restored, edge.Slot, 7);
            Check(!refreshed.Any(retiredIds.Contains), "Refreshing a stale mixed candidate window must remove withdrawn choices.");
            Check(refreshed.SequenceEqual(revised.OfferCandidates(restored, edge.Slot, 999)), "The refreshed window must be stable.");
            Check(restored.CurrentNodeId == before.CurrentNodeId && restored.LastFixedAct == before.LastFixedAct
                && restored.CompletedEdgeIds.SetEquals(before.CompletedEdgeIds) && restored.SeenThinkerIds.SetEquals(before.SeenThinkerIds),
                "Refreshing candidates may not rewind progress, erase history or replace the current doctrine.");

            // A previously accepted edge is history, not permission to repeat or undo it.
            Check(previous.TryAcceptOffered(before, edge.Slot, id), "The old accepted-state fixture must be valid.");
            PhilosophyRunState run = new() { WesternJourney = before };
            WesternJourneyState accepted = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run)).WesternJourney!;
            Check(accepted.CurrentNodeId == edge.ToNodeId && accepted.CompletedEdgeIds.Contains(id),
                "Previously accepted nodes and edge IDs must survive shared save decoding.");
            int oldAct = accepted.LastFixedAct;
            Check(!revised.TryApply(accepted, id) && accepted.LastFixedAct == oldAct, "Retirement never replays an old reward or changes the saved act.");
            Check(revised.CanFinish(accepted), "An accepted legacy node must still admit a legal retained ending.");
            while (accepted.LastFixedAct < 3)
                Check(revised.TryDecline(accepted, "Fixed", accepted.LastFixedAct + 1), "Legacy positions must be retainable across remaining acts.");
            if (!revised.IsComplete(accepted)) Check(revised.TryRetainEnding(accepted), "Nonterminal legacy nodes must admit explicit retention.");
            Check(revised.IsComplete(accepted) && accepted.CurrentNodeId == edge.ToNodeId, "Retention must not replace an accepted legacy node.");
        }

        WesternGraphNode[] entries = revised.Nodes.Where(n => WesternRouteCatalog.IsEntryProblem(n.ThinkerId, n.ProblemId)).ToArray();
        Check(entries.Length == 7, "Retiring edges must preserve all seven entry problem faces.");
        foreach (WesternGraphNode node in entries)
        {
            WesternJourneyState state = revised.Start(node.NodeId);
            Check(revised.CanFinish(state), "Every approved entry must still allow continuation or retention.");
            for (int act = 1; act <= 3; act++)
            {
                Check(!revised.OfferCandidates(state, "Question", 0).Any(retiredIds.Contains), "An entry's question pool cannot offer a retired edge.");
                Check(!revised.OfferCandidates(state, "Fixed", 0).Any(retiredIds.Contains), "An entry's fixed pool cannot offer a retired edge.");
                if (act < 3) Check(revised.TryDecline(state, "Fixed", act + 1), "Retention must not require a removed succession edge.");
            }
        }

        foreach (string defect in new[] { "executable_source", "missing_reason", "missing_note", "broken_history", "unknown_disposition", "unnecessary_archive" })
        {
            JsonObject root = JsonNode.Parse(json)!.AsObject();
            JsonObject source = root["samples"]![0]!.AsObject();
            JsonObject edge = root["edges"]!.AsArray().Select(n => n!.AsObject())
                .Single(n => n["edge_id"]!.GetValue<string>() == "WESTERN_EDGE_002");
            switch (defect)
            {
                case "executable_source": source["disposition"] = "Executable"; break;
                case "missing_reason": edge.Remove("retirement_reason"); break;
                case "missing_note": source["review_note"] = ""; break;
                case "broken_history": source["edge_ids"]![0] = "WESTERN_EDGE_031"; break;
                case "unknown_disposition": source["disposition"] = "Approved"; break;
                case "unnecessary_archive":
                    root["samples"]![1]!["disposition"] = "SourceOnly";
                    root["samples"]![1]!["review_note"] = "Cannot hide a runnable path to silence its tests.";
                    break;
            }
            bool rejected = false;
            try { WesternRouteGraph.ParseJson(root.ToJsonString()); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, $"Invalid withdrawal metadata must fail validation: {defect}");
        }
        Console.WriteLine("Western graph revision checks passed: five retired edges, four preserved source paths, stale windows and legacy retention.");
    }
}
