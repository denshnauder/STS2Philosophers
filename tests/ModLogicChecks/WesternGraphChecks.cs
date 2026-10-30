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
        // A save written before the fixed position was recorded has no position at
        // all; without one no edge is eligible and the run cannot move.
        var legacy = new WesternJourneyState { CurrentNodeId = "DESCARTES__SELF_AND_OTHER__CORE", EntryNodeId = "DESCARTES__SELF_AND_OTHER__CORE", LastFixedAct = 0 };
        legacy.NormalizeAfterLoad();
        Check(legacy.LastFixedAct == 1, "A save with no recorded position starts at the entry's slot.");
        var thirdAct = new WesternJourneyState { CurrentNodeId = "GEORG_WILHELM_FRIEDRICH_HEGEL__HISTORY_AND_POWER__CORE", EntryNodeId = "DESCARTES__SELF_AND_OTHER__CORE", LastFixedAct = 0, Act = 3 };
        thirdAct.NormalizeAfterLoad();
        Check(thirdAct.LastFixedAct == 2, "A run in the third act has passed the second act's slot.");
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        Check(graph.Samples.Count == 14 && graph.ExecutableSamples.Count() == 10,
            "Preserve twelve source sequences and the Plotinus/Aquinas continuations; withdrawn paths stay non-executable.");
        foreach (WesternGraphSample sample in graph.ExecutableSamples)
        {
            WesternJourneyState state = graph.Start(sample.EntryNodeId);
            state.RouteId = sample.RouteId ?? string.Empty;
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
        // Each act owns one fixed position. A run whose relics and route allow it
        // must be able to take all three, not two with the third replacing the second.
        var chain = graph.Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
        Check(graph.TryApply(chain, "WESTERN_EDGE_065") && chain.LastFixedAct == 2,
            "The first act's fixed event opens the second act's position.");
        // Taking the first act's branch names the line, so the run continues on
        // that line's own handovers rather than the edges written before routes.
        Check(chain.RouteId == "CONSEQUENCE_AND_FREEDOM", "The first act's branch must name the line it entered.");
        Check(graph.TryApply(chain, "WESTERN_EDGE_169") && chain.LastFixedAct == 3,
            "The second act's fixed event opens the third act's position.");
        Check(graph.TryApply(chain, "WESTERN_EDGE_170") && chain.LastFixedAct == 3,
            "The third act's fixed event must still be adoptable after the second was taken.");
        Check(chain.CompletedEdgeIds.Contains("WESTERN_EDGE_170")
            && chain.CurrentNodeId == "RAWLS__FREEDOM_AND_INSTITUTIONS__CORE",
            "The third fixed adoption must enter its own node.");
        Check(graph.GetEligibleEdges(chain, "Fixed").Count == 0,
            "A completed three-slot line leaves no fixed position behind.");
        }
        // No fixed edge may be blocked by the slot arithmetic itself. Each act's
        // position must be open whether or not the previous act took its own.
        // Rawls and Arendt still have no room; their edges stay out of this
        // arithmetic check until the rooms exist.
        string[] roomless = ["RAWLS__", "HANNAH_ARENDT__"];
        foreach (WesternGraphEdge edge in graph.Edges.Where(edge => edge.Enabled && edge.Slot == "Fixed"
            && edge.Act is 2 or 3 && !roomless.Any(prefix => edge.ToNodeId.StartsWith(prefix, StringComparison.Ordinal))))
        {
            var state = new WesternJourneyState
            {
                CurrentNodeId = edge.FromNodeId,
                LastFixedAct = edge.Act,
                RouteId = edge.RouteIds?.FirstOrDefault() ?? string.Empty,
            };
            foreach (string required in edge.RequiredEdgeIds) state.CompletedEdgeIds.Add(required);
            Check(graph.GetEligibleEdges(state, "Fixed").Any(candidate => candidate.EdgeId == edge.EdgeId),
                $"Fixed edge {edge.EdgeId} in act {edge.Act} must be open at its own act's position.");
        }
        // Sol's sequence: a run that takes the second act's fixed event at the
        // untouched position two lands on three, and the same act's question must
        // still settle as act two rather than leaking into act three.
        var late = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(late, "WESTERN_EDGE_019"), "The first act's question settles at position one.");
        late.LastFixedAct = 2;
        Check(graph.TryApply(late, "WESTERN_EDGE_020") && late.LastFixedAct == 3,
            "The second act's fixed event moves the position from two to three.");
        Check(graph.GetEligibleEdges(late, "Question").Any(edge => edge.EdgeId == "WESTERN_EDGE_021"),
            "The act's question stays open after its fixed event was taken.");
        Check(graph.TryApply(late, "WESTERN_EDGE_021")
            && late.ResolvedQuestionActs.Contains(2) && !late.ResolvedQuestionActs.Contains(3),
            "A question taken after its own act's fixed event records that act only, so the next act's question stays open.");
        WesternJourneyState plato = graph.Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
        Check(graph.TryApply(plato, "WESTERN_EDGE_008") && graph.TryApply(plato, "WESTERN_EDGE_009"),
            "Plotinus requires the completed Socrates-to-Plato sequence.");
        Check(graph.GetEligibleEdges(plato, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_044"),
            "The completed Plato branch exposes one legal third-act Plotinus edge.");
        WesternJourneyState alternative = JsonSerializer.Deserialize<WesternJourneyState>(JsonSerializer.Serialize(plato))!;
        Check(graph.TryApply(alternative, "WESTERN_EDGE_010")
            && !graph.TryApply(alternative, "WESTERN_EDGE_044"),
            "Switching to Diogenes must exclude Plotinus without rewriting prior history.");
        Check(graph.TryApply(plato, "WESTERN_EDGE_044")
            && plato.CurrentNodeId == "PLOTINUS__VIRTUE_AND_HAPPINESS__CORE"
            && !graph.TryApply(plato, "WESTERN_EDGE_044"),
            "Plotinus adoption consumes the third-act fixed opportunity exactly once.");
        WesternGraphSample first = graph.ExecutableSamples.First();
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

        WesternJourneyState rejecting = graph.Start(first.EntryNodeId);
        graph.OfferCandidates(rejecting, "Question", 0);
        Check(graph.TryDecline(rejecting, "Question", 1) && !graph.TryDecline(rejecting, "Question", 1), "Question refusal must consume exactly one act slot.");
        Check(graph.OfferCandidates(rejecting, "Question", 0).Count == 0, "A previously saved invitation window must not reopen after refusal.");
        Check(!graph.TryApply(rejecting, first.EdgeIds[1]), "Refusal must not satisfy an accepted-challenge prerequisite.");
        rejecting.LastFixedAct = 2;
        Check(graph.TryDecline(rejecting, "Fixed", 2) && !graph.TryDecline(rejecting, "Fixed", 2), "A duplicate refusal callback must not skip two fixed acts.");
        Check(rejecting.CurrentNodeId == first.EntryNodeId && rejecting.CompletedEdgeIds.Count == 0 && rejecting.SeenThinkerIds.Count == 1,
            "Refusal must retain the doctrine without inventing historical choices or rewarding a new person.");
        // The refusal is recorded for the act it happened in. Act one's handover
        // edges stay walkable because they are the branch the question names,
        // and the next act's question must still be open: refusing act one may
        // not spend act two's slot.
        Check(rejecting.ResolvedQuestionActs.SetEquals([1])
            && graph.GetEligibleEdges(rejecting, "Question").Any(edge => edge.Act == 2),
            "A refused question is answered for its own act and leaves the next act's question open.");

        // The act written on an edge is not evidence of where the event happens.
        // An act-one edge walked in the second act is a second-act event, and it
        // may not spend the first act's answer or the third act's slot.
        WesternJourneyState lateQuestion = graph.Start(first.EntryNodeId);
        lateQuestion.LastFixedAct = 2;
        lateQuestion.Act = 2;
        Check(graph.TryApply(lateQuestion, first.EdgeIds[0])
            && lateQuestion.ResolvedQuestionActs.SetEquals([2]),
            "A question walked in the second act is recorded as the second act, not as the act written on its edge.");
        Check(!lateQuestion.ResolvedQuestionActs.Contains(3) && !lateQuestion.DeclinedFixedActs.Contains(3),
            "Taking an early edge in the second act leaves the third act's slot open.");

        WesternJourneyState lateRefusal = graph.Start(first.EntryNodeId);
        lateRefusal.LastFixedAct = 2;
        lateRefusal.Act = 2;
        Check(graph.TryDecline(lateRefusal, "Question", 1)
            && lateRefusal.ResolvedQuestionActs.SetEquals([2])
            && !lateRefusal.ResolvedQuestionActs.Contains(3),
            "A question refused in the second act is recorded as the second act and leaves the third act open.");
        Check(!graph.TryApply(rejecting, first.EdgeIds[0]) && !graph.TryApply(rejecting, first.EdgeIds[1]),
            "A refused question cannot be taken later, and its continuation stays locked.");
        Check(rejecting.LastFixedAct == 2, "A refused fixed event records its own act and leaves the position where it was.");
        rejecting.LastFixedAct = 3;
        Check(graph.CanFinish(rejecting), "A run that refused its first question and its second fixed event can still finish in the third act.");

        PhilosophyRunState shared = new() { WesternJourney = rejecting };
        shared.RecordCurrentDoctrine("KONGZI", "REN", ["CONFUCIAN"]);
        PhilosophyRunState roundTrip = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));
        Check(roundTrip.WesternJourney is not null
            && roundTrip.WesternJourney.DeclinedFixedActs.SetEquals([2])
            && roundTrip.WesternJourney.ResolvedQuestionActs.SetEquals([1])
            && roundTrip.CurrentDoctrine?.ThinkerId == "KONGZI" && roundTrip.ThoughtImprints.Count == 1,
            "Shared encoding must preserve western progress without rewriting existing eastern doctrine or imprints.");
        PhilosophyRunState old = PhilosophyRunStateCodec.Decode(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes("{}")));
        Check(old.WesternJourney is null && !old.HasData, "Old saves must not acquire a western journey or entry reward.");
        string missingCollections = "{\"WesternJourney\":{\"SeenThinkerIds\":null,\"CompletedEdgeIds\":null,\"ResolvedQuestionActs\":null,\"CandidateWindows\":null,\"DeclinedFixedActs\":null}}";
        PhilosophyRunState normalized = PhilosophyRunStateCodec.Decode(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(missingCollections)));
        Check(normalized.WesternJourney!.SeenThinkerIds.Count == 0 && normalized.WesternJourney.CandidateWindows.Count == 0
            && graph.GetEligibleEdges(normalized.WesternJourney, "Fixed").Count == 0, "Missing collections must normalize without fabricating a valid journey.");

        string json = File.ReadAllText("config/western_graph.json");
        foreach (string defect in new[] { "low", "missing_target", "cycle_requirement", "fourth_act", "wrong_problem", "missing_context", "missing_mediator", "invented_behavior", "evidence_upgrade" })
        {
            JsonObject root = JsonNode.Parse(json)!.AsObject();
            JsonObject edge = root["edges"]!.AsArray().Select(node => node!.AsObject()).First(node => node["slot"]!.GetValue<string>() == "Fixed");
            switch (defect)
            {
                case "low": edge["confidence"] = "Low"; break;
                case "missing_target": edge["to_node_id"] = "ABSENT"; break;
                case "cycle_requirement": edge["required_edge_ids"] = new JsonArray(edge["edge_id"]!.GetValue<string>()); break;
                case "fourth_act": edge["act"] = 4; break;
                case "wrong_problem": edge["context"]!["to_problem_id"] = "SELF_AND_OTHER"; break;
                case "missing_context": edge.Remove("context"); break;
                case "missing_mediator": edge["context"]!["compressed_mediators"] = ""; break;
                case "invented_behavior": edge["context"]!["behavior_requirement"] = "UNIMPLEMENTED_REQUIREMENT"; break;
                case "evidence_upgrade": edge["context"]!["evidence_status"] = "VerifiedHistoricalFact"; break;
            }
            bool rejected = false;
            try { WesternRouteGraph.ParseJson(root.ToJsonString()); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, $"Invalid graph mutation must be rejected: {defect}");
        }
        Check(graph.GetEdge("WESTERN_EDGE_013").Context.CompressedMediators.Contains("蒙田")
            && graph.GetEdge("WESTERN_EDGE_031").Context.CompressedMediators.Contains("布尔"),
            "Distant comparison paths must preserve their explicit transmission and logic intermediaries.");
        JsonObject promotion = JsonNode.Parse(json)!.AsObject();
        promotion["edges"]![0]!["context"]!["relation_types"] = new JsonArray("DirectInfluence");
        bool refusedPromotion = false;
        try { WesternRouteGraph.ParseJson(promotion.ToJsonString()); }
        catch (InvalidDataException) { refusedPromotion = true; }
        Check(refusedPromotion, "Heraclitus and Parmenides cannot be promoted to a claimed direct influence.");
        Console.WriteLine("Western graph checks passed: nine executable paths, twelve preserved source sequences, prerequisites and save/restore.");
    }
}
