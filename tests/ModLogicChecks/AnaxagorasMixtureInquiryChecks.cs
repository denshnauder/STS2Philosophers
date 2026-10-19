using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class AnaxagorasMixtureInquiryChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        List<AnaxagorasMixtureInquiryAction[]> paths = CompletePaths();
        Check(paths.Count == 10, "The independent question has ten complete paths.");
        foreach (AnaxagorasMixtureInquiryAction[] path in paths)
        {
            PhilosophyRunState run = NewRun(graph);
            foreach (AnaxagorasMixtureInquiryAction action in path)
            {
                AnaxagorasMixtureInquiryState state = run.AnaxagorasMixtureInquiry!;
                Check(AnaxagorasMixtureInquiryFlow.TryPrepare(state, action)
                    && !AnaxagorasMixtureInquiryFlow.TryPrepare(state, action), "Only one choice can be prepared.");
                run = Reload(run);
                state = run.AnaxagorasMixtureInquiry!;
                Check(state.IsUsable() && AnaxagorasMixtureInquiryFlow.IsJourneyConsistent(state, run.WesternJourney!)
                    && AnaxagorasMixtureInquiryFlow.TryCommitPending(state, run.WesternJourney!)
                    && !AnaxagorasMixtureInquiryFlow.TryCommitPending(state, run.WesternJourney!),
                    "A pending choice commits exactly once after save.");
                run = Reload(run);
            }
            WesternJourneyState journey = run.WesternJourney!;
            Check(run.AnaxagorasMixtureInquiry is { Closed: true }
                && run.AnaxagorasMixtureInquiry.IsUsable()
                && run.AnaxagorasMixtureInquiry.ConfirmedActions.SequenceEqual(path)
                && AnaxagorasMixtureInquiryFlow.IsJourneyConsistent(run.AnaxagorasMixtureInquiry, journey)
                && journey.CurrentNodeId == AnaxagorasMixtureInquiryState.RequiredNodeId
                && journey.CompletedEdgeIds.SetEquals([AnaxagorasMixtureInquiryState.PrerequisiteEdgeId])
                && journey.SeenThinkerIds.SetEquals(["HERACLITUS", "PARMENIDES"])
                && journey.ResolvedQuestionActs.SetEquals([1, 2])
                && journey.DeclinedFixedActs.SetEquals([2]),
                "All outcomes consume only the free question; none invents a route edge or changes the doctrine.");
        }

        PhilosophyRunState pending = NewRun(graph);
        Check(AnaxagorasMixtureInquiryFlow.TryPrepare(pending.AnaxagorasMixtureInquiry!,
                AnaxagorasMixtureInquiryAction.AcceptInvitation), "The invitation can be prepared.");
        WesternJourneyState wrong = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(!AnaxagorasMixtureInquiryFlow.TryCommitPending(pending.AnaxagorasMixtureInquiry!, wrong)
            && pending.AnaxagorasMixtureInquiry!.PendingAction == AnaxagorasMixtureInquiryAction.AcceptInvitation,
            "A wrong route preserves the pending action.");
        Check(AnaxagorasMixtureInquiryFlow.TryCommitPending(pending.AnaxagorasMixtureInquiry!, pending.WesternJourney!),
            "The exact reserved route resumes.");
        pending.AnaxagorasMixtureInquiry!.Stage = AnaxagorasMixtureInquiryStage.Invitation;
        Check(!pending.AnaxagorasMixtureInquiry.IsUsable(), "A forged page cannot rewind history.");
        Check(Reload(new()).AnaxagorasMixtureInquiry is null, "An old save does not invent the event.");

        foreach (string field in new[] { "Version", "InstanceId", "Stage", "PendingAction", "ConfirmedActions", "Closed" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(AnaxagorasMixtureInquiryState.Create("BAD"))!;
            node.AsObject().Remove(field);
            Check(!JsonSerializer.Deserialize<AnaxagorasMixtureInquiryState>(node)!.IsUsable(),
                "Missing save fields cannot turn into a new invitation.");
        }
        foreach (string field in new[] { "Version", "Stage", "PendingAction" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(AnaxagorasMixtureInquiryState.Create("BAD"))!;
            node[field] = 99;
            Check(!JsonSerializer.Deserialize<AnaxagorasMixtureInquiryState>(node)!.IsUsable(),
                "Unknown versions or enum values are unusable.");
        }

        var eligible = new AnaxagorasMixtureInquiryTriggerContext(true, true, true, 1,
            AnaxagorasMixtureInquiryState.RequiredNodeId, 2, true, true, true, true,
            false, false, false, true);
        Check(AnaxagorasMixtureInquiryTriggerPolicy.ShouldInsert(eligible),
            "The declined atomist fixed branch leaves a legal second-act question.");
        foreach (var denied in new[]
        {
            eligible with { CurrentActIndex = 0 }, eligible with { CurrentNodeId = "OTHER" },
            eligible with { LastFixedAct = 1 }, eligible with { ParmenidesReached = false },
            eligible with { SecondFixedDeclined = false }, eligible with { PriorEventClosed = false },
            eligible with { QuestionUnresolved = false }, eligible with { IsSingleplayer = false },
            eligible with { AtSafeMapBoundary = false }, eligible with { ModelAvailable = false },
            eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!AnaxagorasMixtureInquiryTriggerPolicy.ShouldInsert(denied),
            "A wrong or consumed branch cannot invite again.");
        Check(AnaxagorasMixtureInquiryTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true, QuestionUnresolved = false }),
            "A valid open event resumes after reservation.");

        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument localization = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<AnaxagorasMixtureInquiryStage>())
            {
                string page = $"ANAXAGORAS_MIXTURE_INQUIRY.pages.{AnaxagorasMixtureInquiryFlow.StageKey(stage)}";
                Check(localization.RootElement.TryGetProperty(page + ".description", out _), "Every page has bilingual copy.");
                foreach (var action in AnaxagorasMixtureInquiryFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(localization.RootElement.TryGetProperty(
                        $"{page}.options.{AnaxagorasMixtureInquiryFlow.ActionKey(action)}.{suffix}", out _),
                        "Every legal option has bilingual copy.");
            }
        }
        Console.WriteLine("Anaxagoras inquiry checks passed: ten paths, save boundaries, vacant question and no invented graph edge, malformed saves and bilingual text.");
    }

    private static PhilosophyRunState NewRun(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start("HERACLITUS__BEING_AND_CHANGE__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_001")
            && graph.TryDecline(journey, "Fixed", 2)
            && graph.TryReserveQuestion(journey, 2), "Fixture needs the real declined second-act fixed route.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            AnaxagorasMixtureInquiry = AnaxagorasMixtureInquiryState.Create("ANAXAGORAS_MIXTURE_INQUIRY:FIXTURE:2"),
        };
    }

    private static PhilosophyRunState Reload(PhilosophyRunState run) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));

    private static List<AnaxagorasMixtureInquiryAction[]> CompletePaths()
    {
        List<AnaxagorasMixtureInquiryAction[]> paths = [];
        void Walk(AnaxagorasMixtureInquiryStage stage, List<AnaxagorasMixtureInquiryAction> history)
        {
            foreach (var action in AnaxagorasMixtureInquiryFlow.Options(stage))
            {
                List<AnaxagorasMixtureInquiryAction> next = history.Append(action).ToList();
                if (action == AnaxagorasMixtureInquiryAction.Leave) paths.Add(next.ToArray());
                else Walk(AnaxagorasMixtureInquiryFlow.Next(stage, action), next);
            }
        }
        Walk(AnaxagorasMixtureInquiryStage.Invitation, []);
        return paths;
    }
}
