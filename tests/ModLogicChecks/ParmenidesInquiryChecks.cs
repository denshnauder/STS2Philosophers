using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class ParmenidesInquiryChecks
{
    public static void Run()
    {
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        var graph = WesternRouteGraph.LoadEmbedded();
        var accept = ParmenidesInquiryAction.AcceptInvitation;
        var being = ParmenidesInquiryAction.SpeakOfBeing;
        var cup = ParmenidesInquiryAction.SpeakOfCup;
        var distinguish = ParmenidesInquiryAction.DistinguishNegation;
        var adopt = ParmenidesInquiryAction.AdoptBoundary;
        var keep = ParmenidesInquiryAction.KeepChange;
        var stop = ParmenidesInquiryAction.StopInquiry;
        var leave = ParmenidesInquiryAction.Leave;
        ParmenidesInquiryAction[][] paths =
        [
            [ParmenidesInquiryAction.DeclineInvitation, leave], [accept, stop, leave],
            [accept, being, adopt, leave], [accept, being, keep, leave], [accept, being, stop, leave],
            [accept, cup, adopt, leave], [accept, cup, stop, leave],
            [accept, cup, distinguish, adopt, leave], [accept, cup, distinguish, keep, leave],
            [accept, cup, distinguish, stop, leave],
        ];
        foreach (var path in paths)
        {
            PhilosophyRunState run = new()
            {
                WesternJourney = graph.Start(ParmenidesInquiryState.RequiredNodeId),
                ParmenidesInquiry = ParmenidesInquiryState.Create("INSTANCE"),
            };
            Check(graph.TryReserveQuestion(run.WesternJourney, 1), "One question must be reserved before presentation.");
            foreach (var action in path)
            {
                var state = run.ParmenidesInquiry!;
                Check(ParmenidesInquiryFlow.TryPrepare(state, action)
                    && !ParmenidesInquiryFlow.TryPrepare(state, action), "Only one pending action may be prepared.");
                run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
                state = run.ParmenidesInquiry!;
                Check(state.IsUsable() && ParmenidesInquiryFlow.TryCommitPending(state, run.WesternJourney!, graph)
                    && !ParmenidesInquiryFlow.TryCommitPending(state, run.WesternJourney!, graph),
                    "A saved pending action must commit once and cannot pay twice.");
                run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
            }
            Check(run.ParmenidesInquiry is { Closed: true } && run.ParmenidesInquiry.IsUsable()
                && run.ParmenidesInquiry.ConfirmedActions.SequenceEqual(path), "The complete path must survive every save boundary.");
            bool adopted = path.Contains(adopt);
            Check(run.WesternJourney!.CurrentNodeId == (adopted ? ParmenidesInquiryState.AdoptedNodeId : ParmenidesInquiryState.RequiredNodeId)
                && run.WesternJourney.CompletedEdgeIds.Contains(ParmenidesInquiryState.EdgeId) == adopted
                && run.WesternJourney.ResolvedQuestionActs.SetEquals([1]),
                "Only explicit adoption changes the route; all outcomes consume exactly the reserved question.");
            Check(!ParmenidesInquiryFlow.TryPrepare(run.ParmenidesInquiry!, accept), "Closed invitations cannot be refreshed.");
        }

        var stateForReplay = ParmenidesInquiryState.Create("REPLAY");
        var journeyForReplay = graph.Start(ParmenidesInquiryState.RequiredNodeId);
        graph.TryReserveQuestion(journeyForReplay, 1);
        foreach (var action in new[] { accept, cup })
        {
            ParmenidesInquiryFlow.TryPrepare(stateForReplay, action);
            ParmenidesInquiryFlow.TryCommitPending(stateForReplay, journeyForReplay, graph);
        }
        Check(!ParmenidesInquiryFlow.Options(stateForReplay.Stage).Contains(keep)
            && !ParmenidesInquiryFlow.TryPrepare(stateForReplay, being),
            "The cup branch cannot silently switch referents or claim a completed reply before facing its scope.");
        ParmenidesInquiryFlow.TryPrepare(stateForReplay, adopt);
        var wrongJourney = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        graph.TryReserveQuestion(wrongJourney, 1);
        Check(!ParmenidesInquiryFlow.TryCommitPending(stateForReplay, wrongJourney, graph)
            && stateForReplay.PendingAction == adopt && stateForReplay.Stage == ParmenidesInquiryStage.CupChallenge,
            "A wrong route freezes the pending action rather than recording a false result.");
        Check(graph.TryCompleteReservedQuestion(journeyForReplay, ParmenidesInquiryState.EdgeId)
            && ParmenidesInquiryFlow.TryCommitPending(stateForReplay, journeyForReplay, graph)
            && journeyForReplay.CompletedEdgeIds.Count == 1,
            "Recovery accepts the exact already-committed edge without adding a second route operation.");
        stateForReplay.Stage = ParmenidesInquiryStage.Invitation;
        Check(!stateForReplay.IsUsable(), "A forged stage cannot reopen a confirmed path.");
        foreach (string field in new[] { "Version", "Stage", "PendingAction" })
        {
            var json = JsonSerializer.SerializeToNode(ParmenidesInquiryState.Create("INVALID"))!;
            json[field] = 99;
            Check(!JsonSerializer.Deserialize<ParmenidesInquiryState>(json)!.IsUsable(), "Unknown state versions and enums are unusable.");
        }
        Check(PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(new())).ParmenidesInquiry is null,
            "A legacy run lacking this feature must remain absent, not invent a completed encounter.");

        var eligible = new ParmenidesInquiryTriggerContext(true, true, true, 0, 1,
            ParmenidesInquiryState.RequiredNodeId, 1, false, true, false, false, false, true);
        Check(ParmenidesInquiryTriggerPolicy.ShouldInsert(eligible), "The actual Heraclitus route receives the inquiry after combat.");
        foreach (var denied in new[]
        {
            eligible with { CurrentActIndex = 1 }, eligible with { HasRequiredRelic = false },
            eligible with { CurrentNodeId = "OTHER" }, eligible with { CompletedCombatCount = 0 },
            eligible with { QuestionAlreadyResolved = true }, eligible with { IsSingleplayer = false },
            eligible with { AtSafeMapBoundary = false }, eligible with { RunInProgress = false },
            eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!ParmenidesInquiryTriggerPolicy.ShouldInsert(denied), "Invalid or consumed entry cannot invite again.");
        Check(ParmenidesInquiryTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true, QuestionAlreadyResolved = true, CurrentNodeId = ParmenidesInquiryState.AdoptedNodeId }),
            "A saved open result can resume after explicit route adoption.");
        foreach (string language in new[] { "zhs", "eng" })
        {
            using var text = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<ParmenidesInquiryStage>())
            {
                string page = $"PARMENIDES_INQUIRY.pages.{ParmenidesInquiryFlow.StageKey(stage)}";
                Check(text.RootElement.TryGetProperty(page + ".description", out _), "Every stage needs bilingual text.");
                foreach (var action in ParmenidesInquiryFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(text.RootElement.TryGetProperty($"{page}.options.{ParmenidesInquiryFlow.ActionKey(action)}.{suffix}", out _),
                        "Every legal option needs bilingual text.");
            }
        }
        Console.WriteLine("Parmenides inquiry checks passed: 10 complete paths, both save boundaries, explicit adoption, referent constraints and entry guards.");
    }
}
