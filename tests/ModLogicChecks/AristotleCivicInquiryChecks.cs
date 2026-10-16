using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class AristotleCivicInquiryChecks
{
    public static void Run()
    {
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        var graph = WesternRouteGraph.LoadEmbedded();
        var paths = new List<AristotleCivicInquiryAction[]>();
        void Walk(AristotleCivicInquiryStage stage, List<AristotleCivicInquiryAction> history)
        {
            foreach (var action in AristotleCivicInquiryFlow.Options(stage))
            {
                var next = history.Append(action).ToList();
                if (action == AristotleCivicInquiryAction.Leave) paths.Add(next.ToArray());
                else Walk(AristotleCivicInquiryFlow.Next(stage, action), next);
            }
        }
        Walk(AristotleCivicInquiryStage.Invitation, []);
        Check(paths.Count == 12, "Four concrete consequences and four incomplete paths must remain reachable.");
        static WesternJourneyState Journey(WesternRouteGraph graph) => graph.Start(AristotleCivicInquiryState.RequiredNodeId);
        foreach (var path in paths)
        {
            PhilosophyRunState run = new()
            {
                WesternJourney = Journey(graph),
                AristotleCivicInquiry = AristotleCivicInquiryState.Create("INSTANCE"),
            };
            foreach (var action in path)
            {
                var state = run.AristotleCivicInquiry!;
                Check(AristotleCivicInquiryFlow.TryPrepare(state, action)
                    && !AristotleCivicInquiryFlow.TryPrepare(state, action), "Only one pending action can be prepared.");
                run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
                Check(run.AristotleCivicInquiry!.IsUsable()
                    && AristotleCivicInquiryFlow.TryCommitPending(run.AristotleCivicInquiry, run.WesternJourney!, graph)
                    && !AristotleCivicInquiryFlow.TryCommitPending(run.AristotleCivicInquiry, run.WesternJourney!, graph),
                    "The prepared save commits once; replay cannot change a concession or consume twice.");
                run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
            }
            var final = run.AristotleCivicInquiry!;
            bool adopted = path.Contains(AristotleCivicInquiryAction.AdoptInquiry);
            Check(final.Closed && final.IsUsable() && final.ConfirmedActions.SequenceEqual(path), "Both save boundaries preserve the whole path.");
            Check(run.WesternJourney!.LastFixedAct == 2
                && run.WesternJourney.CurrentNodeId == (adopted ? AristotleCivicInquiryState.AdoptedNodeId : AristotleCivicInquiryState.RequiredNodeId)
                && run.WesternJourney.CompletedEdgeIds.Contains(AristotleCivicInquiryState.EdgeId) == adopted
                && run.WesternJourney.DeclinedFixedActs.Contains(2) != adopted
                && run.WesternJourney.ResolvedQuestionActs.Count == 0,
                "Only explicit adoption applies 016; every other outcome consumes just fixed act two, never a question.");
            Check(!AristotleCivicInquiryFlow.TryPrepare(final, AristotleCivicInquiryAction.AcceptInvitation), "Closed instances cannot refresh.");
        }
        foreach (var terminal in new[] { AristotleCivicInquiryAction.AdoptInquiry, AristotleCivicInquiryAction.KeepPlato })
        {
            var state = AristotleCivicInquiryState.Create("REPLAY");
            var journey = Journey(graph);
            foreach (var action in new[] { AristotleCivicInquiryAction.AcceptInvitation, AristotleCivicInquiryAction.Rotate, AristotleCivicInquiryAction.GiveTurn })
                Check(AristotleCivicInquiryFlow.TryPrepare(state, action) && AristotleCivicInquiryFlow.TryCommitPending(state, journey, graph), "Reach a real concession.");
            Check(!AristotleCivicInquiryFlow.TryPrepare(state, AristotleCivicInquiryAction.KeepControl), "The player cannot replace a rotation consequence with a central-control answer.");
            Check(AristotleCivicInquiryFlow.TryPrepare(state, terminal), "Prepare explicit route response.");
            var wrong = graph.Start("SOCRATES__VIRTUE_AND_HAPPINESS__CORE");
            Check(!AristotleCivicInquiryFlow.TryCommitPending(state, wrong, graph)
                && state.PendingAction == terminal && state.Stage == AristotleCivicInquiryStage.ExtendedTurn, "Wrong route freezes without erasing the given turn.");
            Check(terminal == AristotleCivicInquiryAction.AdoptInquiry
                ? graph.TryApply(journey, AristotleCivicInquiryState.EdgeId) : graph.TryDecline(journey, "Fixed", 2), "Simulate route already committed before action history save.");
            Check(AristotleCivicInquiryFlow.TryCommitPending(state, journey, graph), "Exact prior adoption or decline resumes idempotently.");
            state.Stage = AristotleCivicInquiryStage.Arrangement;
            Check(!state.IsUsable(), "A forged stage cannot reopen lost authority or a surrendered turn.");
        }
        foreach (var field in new[] { "Version", "Stage", "PendingAction" })
        {
            var json = JsonSerializer.SerializeToNode(AristotleCivicInquiryState.Create("INVALID"))!;
            json[field] = 99;
            Check(!JsonSerializer.Deserialize<AristotleCivicInquiryState>(json)!.IsUsable(), "Unknown versions and actions cannot create a new encounter.");
        }
        Check(PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(new())).AristotleCivicInquiry is null, "Old saves remain absent.");
        Check(!graph.GetEdge("WESTERN_EDGE_002").Enabled && !graph.GetEdge("WESTERN_EDGE_032").Enabled, "Retired paths stay retired.");
        var eligible = new AristotleCivicInquiryTriggerContext(true, true, true, 1, 1,
            AristotleCivicInquiryState.RequiredNodeId, 1, true, false, false, false, true);
        Check(AristotleCivicInquiryTriggerPolicy.ShouldInsert(eligible), "Only the existing civic Plato source gets the act-two event.");
        foreach (var denied in new[]
        {
            eligible with { CurrentActIndex = 0 }, eligible with { CurrentActIndex = 2 }, eligible with { LastFixedAct = 2 },
            eligible with { CurrentNodeId = "PLATO__VIRTUE_AND_HAPPINESS__CORE" }, eligible with { HasRequiredRelic = false },
            eligible with { CompletedCombatCount = 0 }, eligible with { RunInProgress = false }, eligible with { IsSingleplayer = false },
            eligible with { CurrentRoomIsMap = false }, eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!AristotleCivicInquiryTriggerPolicy.ShouldInsert(denied), "Wrong source, act, unavailable or consumed state cannot enter.");
        Check(AristotleCivicInquiryTriggerPolicy.ShouldInsert(eligible with
        { HasSavedInstance = true, SavedInstanceUsable = true, LastFixedAct = 2, CurrentNodeId = AristotleCivicInquiryState.AdoptedNodeId }), "A saved adoption result remains closable.");
        foreach (var language in new[] { "zhs", "eng" })
        {
            using var text = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<AristotleCivicInquiryStage>())
            {
                var page = $"ARISTOTLE_CIVIC_INQUIRY.pages.{AristotleCivicInquiryFlow.StageKey(stage)}";
                Check(text.RootElement.TryGetProperty(page + ".description", out _), "Every consequence is shown bilingually.");
                foreach (var action in AristotleCivicInquiryFlow.Options(stage))
                foreach (var suffix in new[] { "title", "description" })
                    Check(text.RootElement.TryGetProperty($"{page}.options.{AristotleCivicInquiryFlow.ActionKey(action)}.{suffix}", out _), "Every legal response has text.");
            }
        }
        Console.WriteLine("Aristotle civic inquiry checks passed: 12 complete paths, real concessions, explicit fixed adoption/decline, both save boundaries and entry guards.");
    }
}
