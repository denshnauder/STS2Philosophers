using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class AnaximanderOriginInquiryChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var accept = AnaximanderOriginInquiryAction.AcceptInvitation;
        var visible = AnaximanderOriginInquiryAction.NameVisibleOrigin;
        var unfixed = AnaximanderOriginInquiryAction.WithholdPriority;
        var record = AnaximanderOriginInquiryAction.RecordLimitedQuestion;
        var keep = AnaximanderOriginInquiryAction.KeepObservedTension;
        var stop = AnaximanderOriginInquiryAction.StopInquiry;
        var leave = AnaximanderOriginInquiryAction.Leave;
        AnaximanderOriginInquiryAction[][] paths =
        [
            [AnaximanderOriginInquiryAction.DeclineInvitation, leave], [accept, stop, leave],
            [accept, visible, record, leave], [accept, visible, keep, leave], [accept, visible, stop, leave],
            [accept, unfixed, record, leave], [accept, unfixed, keep, leave], [accept, unfixed, stop, leave],
        ];
        foreach (var path in paths)
        {
            PhilosophyRunState run = new()
            {
                WesternJourney = graph.Start(AnaximanderOriginInquiryState.RequiredNodeId),
                AnaximanderOriginInquiry = AnaximanderOriginInquiryState.Create("ANAX_INSTANCE"),
            };
            WesternJourneyState journey = run.WesternJourney;
            Check(graph.TryReserveQuestion(journey, 1)
                && graph.TryDecline(journey, "Fixed", 2)
                && graph.TryReserveQuestion(journey, 2), "Prior question and second act slots must be reserved.");
            foreach (var action in path)
            {
                AnaximanderOriginInquiryState state = run.AnaximanderOriginInquiry!;
                Check(AnaximanderOriginInquiryFlow.TryPrepare(state, action)
                    && !AnaximanderOriginInquiryFlow.TryPrepare(state, action), "Only one action may be pending.");
                run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
                state = run.AnaximanderOriginInquiry!;
                Check(state.IsUsable() && AnaximanderOriginInquiryFlow.TryCommitPending(state, run.WesternJourney!, graph)
                    && !AnaximanderOriginInquiryFlow.TryCommitPending(state, run.WesternJourney!, graph),
                    "Each saved pending choice must commit exactly once.");
                run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
            }
            Check(run.AnaximanderOriginInquiry is { Closed: true } && run.AnaximanderOriginInquiry.IsUsable()
                && run.AnaximanderOriginInquiry.ConfirmedActions.SequenceEqual(path),
                "Every complete path must survive each save boundary.");
            bool revised = path.Contains(record);
            journey = run.WesternJourney!;
            Check(journey.CurrentNodeId == AnaximanderOriginInquiryState.RequiredNodeId
                && journey.CompletedEdgeIds.Contains(AnaximanderOriginInquiryState.EdgeId) == revised
                && journey.SeenThinkerIds.Contains("ANAXIMANDER") == revised
                && journey.ResolvedQuestionActs.SetEquals([1, 2]) && journey.DeclinedFixedActs.SetEquals([2]),
                "Only explicit revision records Anaximander; no path replaces the current doctrine or renews slots.");
            Check(!AnaximanderOriginInquiryFlow.TryPrepare(run.AnaximanderOriginInquiry!, accept),
                "A closed event cannot reopen.");
        }

        var replay = AnaximanderOriginInquiryState.Create("REPLAY");
        var replayJourney = graph.Start(AnaximanderOriginInquiryState.RequiredNodeId);
        Check(graph.TryReserveQuestion(replayJourney, 1)
            && graph.TryDecline(replayJourney, "Fixed", 2)
            && graph.TryReserveQuestion(replayJourney, 2), "Replay prerequisite must be legal.");
        foreach (var action in new[] { accept, unfixed })
        {
            Check(AnaximanderOriginInquiryFlow.TryPrepare(replay, action)
                && AnaximanderOriginInquiryFlow.TryCommitPending(replay, replayJourney, graph),
                "Replay setup must commit.");
        }
        Check(AnaximanderOriginInquiryFlow.TryPrepare(replay, record), "Record may be prepared.");
        var wrongJourney = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(!AnaximanderOriginInquiryFlow.TryCommitPending(replay, wrongJourney, graph)
            && replay.PendingAction == record, "A wrong route preserves the pending action for recovery.");
        Check(graph.TryCompleteReservedQuestion(replayJourney, AnaximanderOriginInquiryState.EdgeId)
            && AnaximanderOriginInquiryFlow.TryCommitPending(replay, replayJourney, graph)
            && replayJourney.CompletedEdgeIds.Count == 1,
            "Recovery accepts exactly the already committed edge without double recording.");
        replay.Stage = AnaximanderOriginInquiryStage.Invitation;
        Check(!replay.IsUsable(), "Forged stage cannot reopen a confirmed path.");
        foreach (string field in new[] { "Version", "Stage", "PendingAction" })
        {
            JsonNode json = JsonSerializer.SerializeToNode(AnaximanderOriginInquiryState.Create("INVALID"))!;
            json[field] = 99;
            Check(!JsonSerializer.Deserialize<AnaximanderOriginInquiryState>(json)!.IsUsable(),
                "Unknown versions and actions are unusable.");
        }
        Check(PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(new())).AnaximanderOriginInquiry is null,
            "A legacy save must not invent an encounter.");

        var eligible = new AnaximanderOriginInquiryTriggerContext(true, true, true, 1, 1,
            AnaximanderOriginInquiryState.RequiredNodeId, 1, true, true, true,
            true, true, true, false, false, false, true);
        Check(AnaximanderOriginInquiryTriggerPolicy.ShouldInsert(eligible), "The second-act Heraclitus path may enter.");
        foreach (var denied in new[]
        {
            eligible with { CurrentActIndex = 0 }, eligible with { CompletedCombatCount = 0 },
            eligible with { CurrentNodeId = "OTHER" }, eligible with { PriorQuestionResolved = false },
            eligible with { PriorInquiryClosed = false }, eligible with { HasRequiredRelic = false },
            eligible with { FixedOpportunityUnresolved = false }, eligible with { NoFixedCandidate = false },
            eligible with { SecondQuestionUnresolved = false }, eligible with { IsSingleplayer = false },
            eligible with { AtSafeMapBoundary = false }, eligible with { ModelAvailable = false },
            eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!AnaximanderOriginInquiryTriggerPolicy.ShouldInsert(denied),
            "Missing prerequisites or consumed slots cannot invite again.");
        Check(AnaximanderOriginInquiryTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true, LastFixedAct = 2 }),
            "A saved open event may resume in the reserved act.");

        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument text = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<AnaximanderOriginInquiryStage>())
            {
                string page = $"ANAXIMANDER_ORIGIN_INQUIRY.pages.{AnaximanderOriginInquiryFlow.StageKey(stage)}";
                Check(text.RootElement.TryGetProperty(page + ".description", out _), "Each stage needs bilingual text.");
                foreach (var action in AnaximanderOriginInquiryFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(text.RootElement.TryGetProperty($"{page}.options.{AnaximanderOriginInquiryFlow.ActionKey(action)}.{suffix}", out _),
                        "Each legal option needs bilingual text.");
            }
        }
        Console.WriteLine("Anaximander inquiry checks passed: eight complete paths, reserved-slot and replay recovery, entry guards, old saves and bilingual choices.");
    }
}
