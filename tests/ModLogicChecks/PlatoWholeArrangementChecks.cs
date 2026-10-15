using System.Text.Json;

namespace STS2Philosophers;

internal static class PlatoWholeArrangementChecks
{
    public static void Run()
    {
        DeclineConsumesFixedWithoutAdoption();
        StopAfterAcceptanceConsumesFixedWithoutAdoption();
        EveryFinalResponseAdoptsExactlyOnce();
        SaveRoundTripPreservesArrangementFacts();
        TriggerRequiresTheIntendedSuccessorWindow();
        LocalizationCoversEveryPageAndOption();
        EventDoesNotInventAMechanicalDoctrine();
        Console.WriteLine("Plato whole arrangement checks passed.");
    }

    private static void DeclineConsumesFixedWithoutAdoption()
    {
        (PlatoWholeArrangementState state, WesternJourneyState journey, WesternRouteGraph graph) = NewFixture();
        Commit(state, journey, graph, PlatoArrangementAction.DeclineInvitation);
        Assert(state is
               {
                   Stage: PlatoArrangementStage.ResultDeclined,
                   FixedSlotResolved: true,
                   PracticeCompleted: false,
                   EdgeCommitted: false,
               }
               && journey.LastFixedAct == 2
               && journey.DeclinedFixedActs.SetEquals([2])
               && journey.CurrentNodeId == PlatoWholeArrangementState.SourceNodeId
               && !journey.CompletedEdgeIds.Contains(PlatoWholeArrangementState.CompletionEdgeId),
            "Declining Plato must consume act two Fixed without adopting EDGE009.");
    }

    private static void StopAfterAcceptanceConsumesFixedWithoutAdoption()
    {
        (PlatoWholeArrangementState state, WesternJourneyState journey, WesternRouteGraph graph) = NewFixture();
        Commit(state, journey, graph, PlatoArrangementAction.AcceptInvitation);
        Commit(state, journey, graph, PlatoArrangementAction.StopArrangement);
        Assert(state is
               {
                   Stage: PlatoArrangementStage.ResultIncomplete,
                   InvitationAccepted: true,
                   FixedSlotResolved: true,
                   PracticeCompleted: false,
                   EdgeCommitted: false,
               }
               && journey.LastFixedAct == 2
               && journey.DeclinedFixedActs.SetEquals([2])
               && !journey.CompletedEdgeIds.Contains(PlatoWholeArrangementState.CompletionEdgeId),
            "Stopping after accepting must not create a free repeatable look at the Fixed successor.");
    }

    private static void EveryFinalResponseAdoptsExactlyOnce()
    {
        PlatoArrangementAction[][] paths =
        [
            [PlatoArrangementAction.AcceptInvitation, PlatoArrangementAction.ClaimIndependentPart, PlatoArrangementAction.KeepSeparateWork],
            [PlatoArrangementAction.AcceptInvitation, PlatoArrangementAction.ClaimIndependentPart, PlatoArrangementAction.ReturnChoiceAfterInspection],
            [PlatoArrangementAction.AcceptInvitation, PlatoArrangementAction.CommitDependentPart, PlatoArrangementAction.HonorDependentCommitment],
            [PlatoArrangementAction.AcceptInvitation, PlatoArrangementAction.CommitDependentPart, PlatoArrangementAction.WithdrawAfterCompanionChoice],
        ];

        foreach (PlatoArrangementAction[] path in paths)
        {
            (PlatoWholeArrangementState state, WesternJourneyState journey, WesternRouteGraph graph) = NewFixture();
            foreach (PlatoArrangementAction action in path)
            {
                Commit(state, journey, graph, action);
            }

            Assert(state.PracticeCompleted
                   && state.EdgeCommitted
                   && state.FixedSlotResolved
                   && journey.LastFixedAct == 2
                   && journey.CurrentNodeId == "PLATO__VIRTUE_AND_HAPPINESS__CORE"
                   && journey.CompletedEdgeIds.Count(id => id == PlatoWholeArrangementState.CompletionEdgeId) == 1
                   && !journey.DeclinedFixedActs.Contains(2),
                $"Final response path {string.Join('/', path)} must adopt EDGE009 exactly once.");
            Assert(!PlatoWholeArrangementFlow.TryPrepare(state, path[^1]),
                "A result page must not repeat its completion action.");
        }
    }

    private static void SaveRoundTripPreservesArrangementFacts()
    {
        (PlatoWholeArrangementState state, WesternJourneyState journey, WesternRouteGraph graph) = NewFixture();
        Commit(state, journey, graph, PlatoArrangementAction.AcceptInvitation);
        Commit(state, journey, graph, PlatoArrangementAction.ClaimIndependentPart);

        PhilosophyRunState run = new()
        {
            WesternJourney = journey,
            PlatoWholeArrangement = state,
        };
        PhilosophyRunState restored = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
        Assert(restored.PlatoWholeArrangement is
               {
                   Stage: PlatoArrangementStage.IndependentResponse,
                   InvitationAccepted: true,
                   PlayerClaimedIndependentPart: true,
                   CompanionRefusedJointAttribution: true,
                   FixedSlotResolved: false,
                   PracticeCompleted: false,
                   EdgeCommitted: false,
               },
            "Save restore must preserve the player's control and the companion's refusal as separate facts.");
    }

    private static void TriggerRequiresTheIntendedSuccessorWindow()
    {
        PlatoWholeArrangementTriggerContext eligible = new(
            true, true, true, 1, 1,
            PlatoWholeArrangementState.SourceNodeId,
            1, true, false, false, true, false, false, false, true);
        Assert(PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible),
            "The Socrates virtue route must open Plato after one act-two combat and EDGE008.");
        Assert(!PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible with { CompletedCombatCount = 0 })
               && !PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible with { CurrentActIndex = 0 })
               && !PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible with { CurrentNodeId = "OTHER" })
               && !PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible with { RequiredEdgeCompleted = false })
               && !PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible with { FixedActDeclined = true })
               && !PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible with { CompletionEdgeCompleted = true })
               && !PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible with { HasRequiredRelic = false }),
            "A new Plato instance must respect combat, route, predecessor, current relic, and Fixed resolution boundaries.");
        Assert(PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible with
               {
                   CompletedCombatCount = 0,
                   RequiredEdgeCompleted = false,
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
               })
               && !PlatoWholeArrangementTriggerPolicy.ShouldInsert(eligible with
               {
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
                   SavedInstanceClosed = true,
               }),
            "A valid open instance may resume independently of new eligibility, but a closed one may not reopen.");
    }

    private static void LocalizationCoversEveryPageAndOption()
    {
        string root = Directory.GetCurrentDirectory();
        foreach (string language in new[] { "zhs", "eng" })
        {
            string path = Path.Combine(root, "content", "STS2Philosophers", "localization", language, "events.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement entries = document.RootElement;
            foreach (PlatoArrangementStage stage in Enum.GetValues<PlatoArrangementStage>())
            {
                string page = $"PLATO_WHOLE_ARRANGEMENT.pages.{PlatoWholeArrangementFlow.StageKey(stage)}";
                Assert(entries.TryGetProperty($"{page}.description", out _),
                    $"Missing {language} page localization for {stage}.");
                foreach (PlatoArrangementAction action in PlatoWholeArrangementFlow.Options(stage))
                {
                    string option = $"{page}.options.{PlatoWholeArrangementFlow.ActionKey(action)}";
                    Assert(entries.TryGetProperty($"{option}.title", out _)
                           && entries.TryGetProperty($"{option}.description", out _),
                        $"Missing {language} option localization for {stage}/{action}.");
                }
            }
        }
    }

    private static void EventDoesNotInventAMechanicalDoctrine()
    {
        string source = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "src", "Events", "PlatoWholeArrangement.cs"));
        Assert(!source.Contains("RecordCurrentDoctrine", StringComparison.Ordinal),
            "Adopting Plato's route must not invent a mechanical doctrine while the Socrates relic remains equipped.");
    }

    private static (PlatoWholeArrangementState, WesternJourneyState, WesternRouteGraph) NewFixture()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start(PlatoWholeArrangementState.SourceNodeId);
        Assert(graph.TryApply(journey, "WESTERN_EDGE_008"),
            "The fixture must complete Protagoras before offering Plato.");
        return (PlatoWholeArrangementState.Create("TEST_INSTANCE"), journey, graph);
    }

    private static void Commit(
        PlatoWholeArrangementState state,
        WesternJourneyState journey,
        WesternRouteGraph graph,
        PlatoArrangementAction action)
    {
        Assert(PlatoWholeArrangementFlow.TryPrepare(state, action),
            $"Action {action} should be legal from {state.Stage}.");
        Assert(PlatoWholeArrangementFlow.TryCommitPending(state, journey, graph),
            $"Action {action} should commit from its saved pending state.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
