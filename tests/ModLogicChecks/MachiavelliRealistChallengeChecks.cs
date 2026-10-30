using System.Text.Json;
using STS2Philosophers;

internal static class MachiavelliRealistChallengeChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_016"), "Aristotle's 016 must be producible first.");
        Check(graph.GetEligibleEdges(journey, "Question").Any(edge => edge.EdgeId == MachiavelliRealistChallengeRoute.EdgeId),
            "017 must be eligible after 016 at Aristotle's node.");
        Check(graph.GetEligibleEdges(journey, "Fixed").All(edge => edge.EdgeId != "WESTERN_EDGE_018"),
            "Hobbes 018 must remain blocked until 017 is completed.");

        MachiavelliRealistChallengeTriggerContext eligible = new(
            true, true, true, 1, 1, MachiavelliRealistChallengeRoute.SourceNodeId, 2, true, false, true, true, true);
        Check(MachiavelliRealistChallengeTriggerPolicy.ShouldInsert(eligible), "The bridge must be offered at the real act-two question boundary.");

        foreach (MachiavelliRealistChallengeTriggerContext blocked in new[]
        {
            eligible with { RunInProgress = false },
            eligible with { AtSafeMapBoundary = false },
            eligible with { IsSingleplayer = false },
            eligible with { CurrentActIndex = 0 },
            eligible with { CurrentActIndex = 2 },
            eligible with { CompletedCombatCount = 0 },
            eligible with { CurrentNodeId = "ARISTOTLE__BEING_AND_CHANGE__CORE" },
            eligible with { LastFixedAct = 3 },
            eligible with { LastFixedAct = 3 },
            eligible with { RequiredEdgeCompleted = false },
            eligible with { QuestionResolved = true },
            eligible with { EdgeEligible = false },
            eligible with { AristotleClosed = false },
            eligible with { ModelAvailable = false },
        })
        {
            Check(!MachiavelliRealistChallengeTriggerPolicy.ShouldInsert(blocked), $"Blocked context must not offer 017: {blocked}");
        }

        Check(graph.TryApply(journey, MachiavelliRealistChallengeRoute.EdgeId), "The player must be able to complete 017.");
        Check(journey.CompletedEdgeIds.Contains(MachiavelliRealistChallengeRoute.EdgeId)
            && journey.ResolvedQuestionActs.Contains(2)
            && journey.LastFixedAct == 2
            && journey.CurrentNodeId == MachiavelliRealistChallengeRoute.SourceNodeId,
            "Completing 017 keeps the player at Aristotle's node and spends only the question slot.");
        Check(graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_018"),
            "Hobbes 018 must become eligible only after 017.");

        WesternJourneyState declined = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(declined, "WESTERN_EDGE_016"), "016 for the decline path.");
        Check(graph.TryDecline(declined, "Question", 2), "The player must be able to decline the question slot.");
        Check(declined.ResolvedQuestionActs.Contains(2) && !declined.CompletedEdgeIds.Contains(MachiavelliRealistChallengeRoute.EdgeId),
            "Declining 017 spends the question slot without completing the edge.");
        Check(!graph.GetEligibleEdges(declined, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_018"),
            "Declining 017 must keep Hobbes 018 unreachable.");

        string[] keys =
        [
            "MACHIAVELLI_REALIST_CHALLENGE.title",
            "MACHIAVELLI_REALIST_CHALLENGE.pages.INVITATION.description",
            "MACHIAVELLI_REALIST_CHALLENGE.pages.RESULT_ACCEPTED.description",
            "MACHIAVELLI_REALIST_CHALLENGE.pages.RESULT_KEPT.description",
            "MACHIAVELLI_REALIST_CHALLENGE.pages.INVITATION.options.ACCEPT_QUESTION.title",
            "MACHIAVELLI_REALIST_CHALLENGE.pages.INVITATION.options.ACCEPT_QUESTION.description",
            "MACHIAVELLI_REALIST_CHALLENGE.pages.INVITATION.options.KEEP_ARISTOTLE.title",
            "MACHIAVELLI_REALIST_CHALLENGE.pages.INVITATION.options.KEEP_ARISTOTLE.description",
        ];
        foreach (string lang in new[] { "eng", "zhs" })
        {
            using JsonDocument localization = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{lang}/events.json"));
            foreach (string key in keys)
            {
                Check(localization.RootElement.TryGetProperty(key, out JsonElement value)
                    && !string.IsNullOrWhiteSpace(value.GetString()), $"{lang} must localize {key}.");
            }
        }
    }
}
