using System.Text.Json;
using STS2Philosophers;

internal static class ZenoCivicChallengeChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        ZenoCivicAction[][] paths =
        [
            [ZenoCivicAction.DeclineInvitation],
            [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.DemandPublicRule,
                ZenoCivicAction.PublishPublicObjection],
            [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.ProposeConditionalException,
                ZenoCivicAction.PublishConditionalObjection],
            [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.DemandPublicRule,
                ZenoCivicAction.WithdrawObjection],
            [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.ProposeConditionalException,
                ZenoCivicAction.WithdrawObjection],
        ];
        foreach (ZenoCivicAction[] path in paths)
        {
            WesternJourneyState journey = graph.Start(ZenoCivicChallengeState.SourceNodeId);
            Check(graph.TryReserveQuestion(journey, ZenoCivicChallengeState.QuestionAct),
                "Opening the event reserves the first-act question once.");
            PhilosophyRunState run = new()
            {
                WesternJourney = journey,
                ZenoCivicChallenge = ZenoCivicChallengeState.Create("ZENOCIVIC:test:1"),
            };
            foreach (ZenoCivicAction action in path.Append(ZenoCivicAction.Leave))
            {
                ZenoCivicChallengeState state = run.ZenoCivicChallenge!;
                Check(ZenoCivicChallengeFlow.TryPrepare(state, action), $"Can prepare {action}.");
                run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
                state = run.ZenoCivicChallenge!;
                Check(ZenoCivicChallengeFlow.TryCommitPending(state, run.WesternJourney!, graph),
                    $"Can commit {action} from the prepared save.");
                Check(!ZenoCivicChallengeFlow.TryCommitPending(state, run.WesternJourney!, graph),
                    "A confirmed action cannot replay.");
                run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
                Check(ZenoCivicChallengeFlow.IsRouteConsistent(run.ZenoCivicChallenge!, run.WesternJourney!),
                    "The confirmed save retains its exact question result.");
            }
            bool published = path.Contains(ZenoCivicAction.PublishPublicObjection)
                || path.Contains(ZenoCivicAction.PublishConditionalObjection);
            Check(run.ZenoCivicChallenge!.Closed && run.WesternJourney!.LastFixedAct == 1
                && run.WesternJourney.CurrentNodeId == ZenoCivicChallengeState.SourceNodeId
                && run.WesternJourney.ResolvedQuestionActs.SetEquals([1])
                && run.WesternJourney.CompletedEdgeIds.Contains(ZenoCivicChallengeState.EdgeId) == published,
                "Every terminal choice consumes only the question; only a published objection records 019.");
            bool augustineEligible = graph.GetEligibleEdges(run.WesternJourney!, "Fixed")
                .Any(edge => edge.EdgeId == "WESTERN_EDGE_020");
            Check(augustineEligible == published,
                "A published objection alone unlocks 020; withdrawal preserves the Aristotle alternative.");
            Check(!ZenoCivicChallengeFlow.TryPrepare(run.ZenoCivicChallenge, ZenoCivicAction.Leave),
                "Closing cannot repeat or change the recorded choice.");
        }

        WesternJourneyState malformedJourney = graph.Start(ZenoCivicChallengeState.SourceNodeId);
        Check(graph.TryReserveQuestion(malformedJourney, 1), "Prepare malformed-route fixture.");
        ZenoCivicChallengeState malformed = ZenoCivicChallengeState.Create("bad");
        malformed.Stage = ZenoCivicStage.ResultPublicObjection;
        Check(!malformed.IsUsable() && !ZenoCivicChallengeFlow.IsRouteConsistent(malformed, malformedJourney),
            "A forged result without confirmed actions cannot claim 019.");
        malformed = ZenoCivicChallengeState.Create("missing-reservation");
        malformedJourney.ResolvedQuestionActs.Clear();
        Check(!ZenoCivicChallengeFlow.IsRouteConsistent(malformed, malformedJourney),
            "A missing reserved question is not repaired on load.");

        ZenoCivicChallengeTriggerContext eligible = new(true, true, true, 0, true, 1,
            ZenoCivicChallengeState.SourceNodeId, 1, false, true, true,
            false, false, false, false, true);
        Check(ZenoCivicChallengeTriggerPolicy.ShouldInsert(eligible),
            "A completed first-act nonboss combat opens the unused civic question.");
        Check(!ZenoCivicChallengeTriggerPolicy.ShouldInsert(eligible with { SourceIsCompletedNonBossCombat = false })
            && !ZenoCivicChallengeTriggerPolicy.ShouldInsert(eligible with { QuestionResolved = true })
            && !ZenoCivicChallengeTriggerPolicy.ShouldInsert(eligible with { EdgeEligible = false })
            && !ZenoCivicChallengeTriggerPolicy.ShouldInsert(eligible with { HasPlatoCivicSeal = false })
            && !ZenoCivicChallengeTriggerPolicy.ShouldInsert(eligible with { CurrentActIndex = 1 }),
            "Other rooms, consumed questions, wrong relics and acts cannot create a new invitation.");
        Check(ZenoCivicChallengeTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true, SavedRouteConsistent = true,
                SourceIsCompletedNonBossCombat = false, QuestionResolved = true }),
            "A saved open instance may resume at its own safe map boundary.");

        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            JsonElement entries = document.RootElement;
            Check(entries.TryGetProperty("ZENO_CIVIC_CHALLENGE.title", out _), "Civic title is localized.");
            foreach (ZenoCivicStage stage in Enum.GetValues<ZenoCivicStage>())
            {
                string page = $"ZENO_CIVIC_CHALLENGE.pages.{ZenoCivicChallengeFlow.StageKey(stage)}";
                Check(entries.TryGetProperty($"{page}.description", out _), $"Missing {language} {page}.");
                foreach (ZenoCivicAction action in ZenoCivicChallengeFlow.Options(stage))
                {
                    string option = $"{page}.options.{ZenoCivicChallengeFlow.ActionKey(action)}";
                    Check(entries.TryGetProperty($"{option}.title", out _)
                        && entries.TryGetProperty($"{option}.description", out _),
                        $"Missing {language} {option}.");
                }
            }
        }
        Console.WriteLine("Zeno civic challenge checks passed: five saved paths, first-act question reservation, 019/020 eligibility and bilingual options.");
    }
}
