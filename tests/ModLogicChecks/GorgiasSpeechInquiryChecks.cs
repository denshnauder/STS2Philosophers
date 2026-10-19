using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class GorgiasSpeechInquiryChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        List<GorgiasSpeechInquiryAction[]> paths = CompletePaths();
        Check(paths.Count == 10, "The independent question has ten complete paths.");
        foreach (GorgiasSpeechInquiryAction[] path in paths)
        {
            PhilosophyRunState run = NewRun(graph);
            foreach (GorgiasSpeechInquiryAction action in path)
            {
                GorgiasSpeechInquiryState state = run.GorgiasSpeechInquiry!;
                Check(GorgiasSpeechInquiryFlow.TryPrepare(state, action)
                    && !GorgiasSpeechInquiryFlow.TryPrepare(state, action), "Only one choice can be prepared.");
                run = Reload(run);
                state = run.GorgiasSpeechInquiry!;
                Check(state.IsUsable() && GorgiasSpeechInquiryFlow.IsJourneyConsistent(state, run.WesternJourney!)
                    && GorgiasSpeechInquiryFlow.TryCommitPending(state, run.WesternJourney!)
                    && !GorgiasSpeechInquiryFlow.TryCommitPending(state, run.WesternJourney!),
                    "A pending choice commits exactly once after save.");
                run = Reload(run);
            }
            WesternJourneyState journey = run.WesternJourney!;
            Check(run.GorgiasSpeechInquiry is { Closed: true }
                && run.GorgiasSpeechInquiry.IsUsable()
                && run.GorgiasSpeechInquiry.ConfirmedActions.SequenceEqual(path)
                && GorgiasSpeechInquiryFlow.IsJourneyConsistent(run.GorgiasSpeechInquiry, journey)
                && journey.CurrentNodeId == GorgiasSpeechInquiryState.RequiredNodeId
                && journey.CompletedEdgeIds.Count == 0
                && journey.SeenThinkerIds.SetEquals(["ARISTOTLE"])
                && journey.ResolvedQuestionActs.SetEquals([1])
                && journey.DeclinedFixedActs.Count == 0,
                "All outcomes consume only the free question; none invents a route edge or changes the doctrine.");
        }

        PhilosophyRunState pending = NewRun(graph);
        Check(GorgiasSpeechInquiryFlow.TryPrepare(pending.GorgiasSpeechInquiry!,
                GorgiasSpeechInquiryAction.AcceptInvitation), "The invitation can be prepared.");
        WesternJourneyState wrong = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(!GorgiasSpeechInquiryFlow.TryCommitPending(pending.GorgiasSpeechInquiry!, wrong)
            && pending.GorgiasSpeechInquiry!.PendingAction == GorgiasSpeechInquiryAction.AcceptInvitation,
            "A wrong route preserves the pending action.");
        Check(GorgiasSpeechInquiryFlow.TryCommitPending(pending.GorgiasSpeechInquiry!, pending.WesternJourney!),
            "The exact reserved route resumes.");
        pending.GorgiasSpeechInquiry!.Stage = GorgiasSpeechInquiryStage.Invitation;
        Check(!pending.GorgiasSpeechInquiry.IsUsable(), "A forged page cannot rewind history.");
        Check(Reload(new()).GorgiasSpeechInquiry is null, "An old save does not invent the event.");

        foreach (string field in new[] { "Version", "InstanceId", "Stage", "PendingAction", "ConfirmedActions", "Closed" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(GorgiasSpeechInquiryState.Create("BAD"))!;
            node.AsObject().Remove(field);
            Check(!JsonSerializer.Deserialize<GorgiasSpeechInquiryState>(node)!.IsUsable(),
                "Missing save fields cannot turn into a new invitation.");
        }
        foreach (string field in new[] { "Version", "Stage", "PendingAction" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(GorgiasSpeechInquiryState.Create("BAD"))!;
            node[field] = 99;
            Check(!JsonSerializer.Deserialize<GorgiasSpeechInquiryState>(node)!.IsUsable(),
                "Unknown versions or enum values are unusable.");
        }

        var eligible = new GorgiasSpeechInquiryTriggerContext(true, true, true, 0, 1,
            GorgiasSpeechInquiryState.RequiredNodeId, 1, true, true,
            false, false, false, true);
        Check(GorgiasSpeechInquiryTriggerPolicy.ShouldInsert(eligible),
            "The Aristotle language entry leaves a legal first-act question.");
        foreach (var denied in new[]
        {
            eligible with { CurrentActIndex = 1 }, eligible with { CurrentNodeId = "OTHER" },
            eligible with { LastFixedAct = 2 }, eligible with { CompletedCombatCount = 0 },
            eligible with { HasRequiredRelic = false },
            eligible with { QuestionUnresolved = false }, eligible with { IsSingleplayer = false },
            eligible with { AtSafeMapBoundary = false }, eligible with { ModelAvailable = false },
            eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!GorgiasSpeechInquiryTriggerPolicy.ShouldInsert(denied),
            "A wrong or consumed branch cannot invite again.");
        Check(GorgiasSpeechInquiryTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true, QuestionUnresolved = false }),
            "A valid open event resumes after reservation.");

        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument localization = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<GorgiasSpeechInquiryStage>())
            {
                string page = $"GORGIAS_SPEECH_INQUIRY.pages.{GorgiasSpeechInquiryFlow.StageKey(stage)}";
                Check(localization.RootElement.TryGetProperty(page + ".description", out _), "Every page has bilingual copy.");
                foreach (var action in GorgiasSpeechInquiryFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(localization.RootElement.TryGetProperty(
                        $"{page}.options.{GorgiasSpeechInquiryFlow.ActionKey(action)}.{suffix}", out _),
                        "Every legal option has bilingual copy.");
            }
        }
        Console.WriteLine("Gorgias inquiry checks passed: ten paths, save boundaries, first-act language question and no invented graph edge, malformed saves and bilingual text.");
    }

    private static PhilosophyRunState NewRun(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start(GorgiasSpeechInquiryState.RequiredNodeId);
        Check(graph.TryReserveQuestion(journey, 1), "Fixture needs the real first-act language question.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            GorgiasSpeechInquiry = GorgiasSpeechInquiryState.Create("GORGIAS_SPEECH_INQUIRY:FIXTURE:1"),
        };
    }

    private static PhilosophyRunState Reload(PhilosophyRunState run) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));

    private static List<GorgiasSpeechInquiryAction[]> CompletePaths()
    {
        List<GorgiasSpeechInquiryAction[]> paths = [];
        void Walk(GorgiasSpeechInquiryStage stage, List<GorgiasSpeechInquiryAction> history)
        {
            foreach (var action in GorgiasSpeechInquiryFlow.Options(stage))
            {
                List<GorgiasSpeechInquiryAction> next = history.Append(action).ToList();
                if (action == GorgiasSpeechInquiryAction.Leave) paths.Add(next.ToArray());
                else Walk(GorgiasSpeechInquiryFlow.Next(stage, action), next);
            }
        }
        Walk(GorgiasSpeechInquiryStage.Invitation, []);
        return paths;
    }
}
