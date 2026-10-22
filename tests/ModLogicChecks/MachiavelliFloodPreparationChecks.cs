using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class MachiavelliFloodPreparationChecks
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static PhilosophyRunState Reload(PhilosophyRunState shared) => PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));
    private static PhilosophyRunState Fixture(WesternRouteGraph graph) => new()
    {
        WesternJourney = graph.Start(MachiavelliFloodPreparationState.SourceNodeId),
        MachiavelliFloodPreparation = MachiavelliFloodPreparationState.Create("MACHIAVELLI:TEST:2"),
    };
    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph, MachiavelliFloodPreparationAction action)
    {
        Check(MachiavelliFloodPreparationFlow.TryPrepare(shared.MachiavelliFloodPreparation!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(MachiavelliFloodPreparationFlow.TryCommitPending(shared.MachiavelliFloodPreparation!, shared.WesternJourney!, graph), $"Commit {action} after a saved preparation.");
        Check(!MachiavelliFloodPreparationFlow.TryCommitPending(shared.MachiavelliFloodPreparation!, shared.WesternJourney!, graph), "A confirmed action cannot replay.");
        shared = Reload(shared);
        Check(MachiavelliFloodPreparationFlow.IsRouteConsistent(shared.MachiavelliFloodPreparation!, shared.WesternJourney!), "Event and route survive reload together.");
        return shared;
    }
    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var shared = Commit(Commit(Fixture(graph), graph, MachiavelliFloodPreparationAction.DeclineInvitation), graph, MachiavelliFloodPreparationAction.Leave);
        Check(shared.WesternJourney!.LastFixedAct == 2 && shared.WesternJourney.DeclinedFixedActs.Contains(2)
            && shared.WesternJourney.CurrentNodeId == MachiavelliFloodPreparationState.SourceNodeId, "Refusal spends only the fixed opportunity, retaining Rousseau.");
        foreach (var preparation in new[] { MachiavelliFloodPreparationAction.ReinforceBank, MachiavelliFloodPreparationAction.CoordinateEvacuation })
        foreach (var decision in new[] { MachiavelliFloodPreparationAction.AdoptPrepared, MachiavelliFloodPreparationAction.KeepRousseau })
        {
            shared = Fixture(graph);
            Check(graph.CanFinish(shared.WesternJourney!), "Source route can still finish.");
            shared = Commit(shared, graph, MachiavelliFloodPreparationAction.AcceptInvitation);
            Check(!MachiavelliFloodPreparationFlow.TryPrepare(shared.MachiavelliFloodPreparation!, decision), "Adoption cannot precede preparation or the flood.");
            shared = Commit(shared, graph, preparation);
            Check(!MachiavelliFloodPreparationFlow.TryPrepare(shared.MachiavelliFloodPreparation!, preparation), "The revealed flood cannot reroll the earlier preparation.");
            shared = Commit(Commit(shared, graph, decision), graph, MachiavelliFloodPreparationAction.Leave);
            bool adopts = decision == MachiavelliFloodPreparationAction.AdoptPrepared;
            Check(shared.MachiavelliFloodPreparation!.Closed && shared.MachiavelliFloodPreparation.EdgeCommitted == adopts
                && shared.WesternJourney!.LastFixedAct == 2 && shared.WesternJourney.ResolvedQuestionActs.Count == 0
                && shared.WesternJourney.CompletedEdgeIds.SetEquals(adopts ? ["WESTERN_EDGE_046"] : Array.Empty<string>())
                && (!adopts || !graph.GetEligibleEdges(shared.WesternJourney, "Fixed").Any(e => e.EdgeId == "WESTERN_EDGE_040")),
                "Actual Fixed2 is consumed without a fabricated 017 question or later 040.");
            WesternJourneyState journey = shared.WesternJourney!;
            Check(graph.CanFinish(journey) && graph.TryDecline(journey, "Fixed", 3)
                && graph.TryRetainEnding(journey) && graph.IsComplete(journey)
                && MachiavelliFloodPreparationFlow.IsRouteConsistent(shared.MachiavelliFloodPreparation, journey),
                "The current branch has a legal third-act retained ending, with no fourth act or invented successor.");
        }
        shared = Fixture(graph);
        Check(graph.TryApply(shared.WesternJourney!, "WESTERN_EDGE_040")
            && !MachiavelliFloodPreparationFlow.IsRouteConsistent(shared.MachiavelliFloodPreparation!, shared.WesternJourney!),
            "Wollstonecraft's fixed adoption excludes the parallel candidate.");
        shared = Fixture(graph); shared.WesternJourney = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(shared.WesternJourney, "WESTERN_EDGE_016")
            && !MachiavelliFloodPreparationFlow.IsRouteConsistent(shared.MachiavelliFloodPreparation!, shared.WesternJourney)
            && graph.GetEligibleEdges(shared.WesternJourney, "Fixed").Any(e => e.EdgeId == "WESTERN_EDGE_045"),
            "The institution branch keeps Aquinas and never acquires history 046.");
        shared = Fixture(graph);
        foreach (string field in new[] { "Closed", "Version", "Stage", "PendingAction", "ConfirmedActions" })
        {
            var json = JsonSerializer.SerializeToNode(shared.MachiavelliFloodPreparation)!.AsObject(); json.Remove(field);
            Check(!json.Deserialize<MachiavelliFloodPreparationState>()!.IsUsable(), $"Missing {field} cannot recreate an invitation.");
        }
        shared.MachiavelliFloodPreparation!.EdgeCommitted = true;
        Check(!shared.MachiavelliFloodPreparation.IsUsable(), "A forged adoption without history is rejected.");
        MachiavelliFloodPreparationTriggerContext eligible = new(true, true, true, 1, true, 1,
            MachiavelliFloodPreparationState.SourceNodeId, 1, true, true, false, false, false, false, true);
        Check(MachiavelliFloodPreparationTriggerPolicy.ShouldInsert(eligible), "Real history fixed is reachable.");
        foreach (var blocked in new[] { eligible with { AtSafeMapBoundary = false }, eligible with { IsSingleplayer = false },
            eligible with { CurrentActIndex = 0 }, eligible with { CurrentActIndex = 2 }, eligible with { SourceIsCompletedNonBossCombat = false },
            eligible with { LastFixedAct = 2 }, eligible with { CompletionEdgeEligible = false }, eligible with { HasRequiredRelic = false },
            eligible with { HasSavedInstance = true }, eligible with { ModelAvailable = false } })
            Check(!MachiavelliFloodPreparationTriggerPolicy.ShouldInsert(blocked), "A missing native or route fact blocks insertion.");
        Check(MachiavelliFloodPreparationTriggerPolicy.ShouldInsert(eligible with { HasSavedInstance = true,
            SavedInstanceUsable = true, SavedRouteConsistent = true, LastFixedAct = 2,
            CompletionEdgeEligible = false, SourceIsCompletedNonBossCombat = false }), "A saved result resumes without a second fixed adoption.");
        var practice = MachiavelliCombatPreparationState.Create();
        Check(practice.ObserveFirstManual("1:4:21", true) && !practice.ObserveFirstManual("1:4:21", false)
            && practice.Counter("1:4:21") == 1 && MachiavelliCombatPreparationState.TryRestore(practice.Encode(), out practice)
            && practice.TryClaim("1:4:21") && !practice.TryClaim("1:4:21")
            && practice.ObserveFirstManual("1:5:22", false) && !practice.TryClaim("1:5:22")
            && !practice.ObserveFirstManual("1:5:22", true) && !practice.TryClaim("1:4:21"),
            "Saved first-card failure and reward receipts cannot be reopened by later cards, callbacks or old rooms.");
        foreach (string bad in new[] { "broken", "{}", "{\"Version\":99,\"Receipts\":{}}", "{\"Version\":1,\"Receipts\":{\"1:2:3\":{\"Prepared\":true}}}" })
            Check(!MachiavelliCombatPreparationState.TryRestore(bad, out _), "Unknown or incomplete preparation payloads are inert.");
        Localizations();
        Console.WriteLine("Machiavelli checks passed: five saved paths, preparation-before-reveal, true Fixed2 and 040/institution exclusions, legal retained ending, exact combat receipts and bilingual pages.");
    }
    private static void Localizations()
    {
        foreach (string lang in new[] { "eng", "zhs" })
        {
            using var events = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{lang}/events.json"));
            foreach (var stage in Enum.GetValues<MachiavelliFloodPreparationStage>())
            {
                foreach (var preparation in stage is MachiavelliFloodPreparationStage.Flood or MachiavelliFloodPreparationStage.ResultAdopted or MachiavelliFloodPreparationStage.ResultKept
                    ? new[] { MachiavelliPreparation.Bank, MachiavelliPreparation.Coordination } : new[] { MachiavelliPreparation.None })
                {
                    var state = MachiavelliFloodPreparationState.Create("translation"); state.Stage = stage; state.Preparation = preparation;
                    Check(events.RootElement.TryGetProperty($"MACHIAVELLI_FLOOD_PREPARATION.pages.{MachiavelliFloodPreparationFlow.DescriptionKey(state)}.description", out _), "Every page is bilingual.");
                }
                foreach (var action in MachiavelliFloodPreparationFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(events.RootElement.TryGetProperty($"MACHIAVELLI_FLOOD_PREPARATION.pages.{MachiavelliFloodPreparationFlow.StageKey(stage)}.options.{MachiavelliFloodPreparationFlow.ActionKey(action)}.{suffix}", out _), "Every option is bilingual.");
            }
            using var relics = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{lang}/relics.json"));
            foreach (string branch in new[] { "bank", "coordination", "unavailable" })
                Check(relics.RootElement.TryGetProperty($"ROUSSEAU_UNCARVED_STONE.machiavelli.{branch}.description", out _), "The actual replacement and interrupted practice have bilingual tips.");
        }
    }
}
