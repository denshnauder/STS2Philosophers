using System.Text.Json;

namespace STS2Philosophers;

internal static class ProtagorasSharedDeliberationChecks
{
    public static void Run()
    {
        DeclineDoesNotConsumeQuestion();
        IncompletePracticeConsumesQuestionWithoutCompletingEdge();
        EveryFinalResponseCompletesExactlyOnce();
        SaveRoundTripPreservesAuthorizationFacts();
        TriggerRequiresTheIntendedRouteAndOneCombat();
        LocalizationCoversEveryPageAndOption();
        Console.WriteLine("Protagoras shared deliberation checks passed.");
    }

    private static void DeclineDoesNotConsumeQuestion()
    {
        (ProtagorasSharedDeliberationState state, WesternJourneyState journey, WesternRouteGraph graph) = NewFixture();
        Commit(state, journey, graph, ProtagorasDeliberationAction.DeclineInvitation);
        Assert(state.Stage == ProtagorasDeliberationStage.ResultDeclined
               && !state.QuestionReserved
               && !journey.ResolvedQuestionActs.Contains(1)
               && !journey.CompletedEdgeIds.Contains(ProtagorasSharedDeliberationState.CompletionEdgeId),
            "Declining the invitation must neither occupy the Question nor complete EDGE008.");
    }

    private static void IncompletePracticeConsumesQuestionWithoutCompletingEdge()
    {
        (ProtagorasSharedDeliberationState state, WesternJourneyState journey, WesternRouteGraph graph) = NewFixture();
        Commit(state, journey, graph, ProtagorasDeliberationAction.AcceptInvitation);
        Commit(state, journey, graph, ProtagorasDeliberationAction.StopPractice);
        Assert(state.Stage == ProtagorasDeliberationStage.ResultIncomplete
               && state.QuestionReserved
               && journey.ResolvedQuestionActs.Contains(1)
               && !state.PracticeCompleted
               && !state.EdgeCommitted
               && !journey.CompletedEdgeIds.Contains(ProtagorasSharedDeliberationState.CompletionEdgeId),
            "Stopping after acceptance must preserve the occupied Question while withholding completion.");
    }

    private static void EveryFinalResponseCompletesExactlyOnce()
    {
        ProtagorasDeliberationAction[][] paths =
        [
            [ProtagorasDeliberationAction.AcceptInvitation, ProtagorasDeliberationAction.DemonstrateOriginalMethod, ProtagorasDeliberationAction.ChoosePersonalHost, ProtagorasDeliberationAction.HearFullReason],
            [ProtagorasDeliberationAction.AcceptInvitation, ProtagorasDeliberationAction.DemonstrateOriginalMethod, ProtagorasDeliberationAction.ChoosePersonalHost, ProtagorasDeliberationAction.WithdrawRepresentationRequest],
            [ProtagorasDeliberationAction.AcceptInvitation, ProtagorasDeliberationAction.DemonstrateOriginalMethod, ProtagorasDeliberationAction.ChooseNegotiation, ProtagorasDeliberationAction.RestateWithoutAuthorization],
            [ProtagorasDeliberationAction.AcceptInvitation, ProtagorasDeliberationAction.DemonstrateOriginalMethod, ProtagorasDeliberationAction.ChooseNegotiation, ProtagorasDeliberationAction.AuthorizeCompanion, ProtagorasDeliberationAction.ConfirmLimitedAuthorization],
            [ProtagorasDeliberationAction.AcceptInvitation, ProtagorasDeliberationAction.DemonstrateOriginalMethod, ProtagorasDeliberationAction.ChooseNegotiation, ProtagorasDeliberationAction.AuthorizeCompanion, ProtagorasDeliberationAction.StopFutureDelegation],
            [ProtagorasDeliberationAction.AcceptInvitation, ProtagorasDeliberationAction.DemonstrateOriginalMethod, ProtagorasDeliberationAction.ChooseNegotiation, ProtagorasDeliberationAction.ProposeSpecificSentence, ProtagorasDeliberationAction.AuthorizeNarrowedScope, ProtagorasDeliberationAction.ConfirmLimitedAuthorization],
            [ProtagorasDeliberationAction.AcceptInvitation, ProtagorasDeliberationAction.DemonstrateOriginalMethod, ProtagorasDeliberationAction.ChooseNegotiation, ProtagorasDeliberationAction.ProposeSpecificSentence, ProtagorasDeliberationAction.RestateAfterRevision],
        ];

        foreach (ProtagorasDeliberationAction[] path in paths)
        {
            (ProtagorasSharedDeliberationState state, WesternJourneyState journey, WesternRouteGraph graph) = NewFixture();
            foreach (ProtagorasDeliberationAction action in path)
            {
                Commit(state, journey, graph, action);
            }

            Assert(state.PracticeCompleted
                   && state.EdgeCommitted
                   && journey.CompletedEdgeIds.Count(id => id == ProtagorasSharedDeliberationState.CompletionEdgeId) == 1
                   && journey.ResolvedQuestionActs.SetEquals([1])
                   && journey.CurrentNodeId == ProtagorasSharedDeliberationState.SourceNodeId,
                $"Final response path {string.Join('/', path)} must complete Keep edge exactly once without changing doctrine node.");
            Assert(!ProtagorasSharedDeliberationFlow.TryPrepare(state, path[^1]),
                "A result page must not repeat its completion action.");
        }
    }

    private static void SaveRoundTripPreservesAuthorizationFacts()
    {
        (ProtagorasSharedDeliberationState state, WesternJourneyState journey, WesternRouteGraph graph) = NewFixture();
        foreach (ProtagorasDeliberationAction action in new[]
                 {
                     ProtagorasDeliberationAction.AcceptInvitation,
                     ProtagorasDeliberationAction.DemonstrateOriginalMethod,
                     ProtagorasDeliberationAction.ChooseNegotiation,
                     ProtagorasDeliberationAction.ProposeSpecificSentence,
                     ProtagorasDeliberationAction.AuthorizeNarrowedScope,
                 })
        {
            Commit(state, journey, graph, action);
        }

        PhilosophyRunState run = new()
        {
            WesternJourney = journey,
            ProtagorasSharedDeliberation = state,
        };
        PhilosophyRunState restored = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
        ProtagorasSharedDeliberationState? saved = restored.ProtagorasSharedDeliberation;
        Assert(saved is
               {
                   Stage: ProtagorasDeliberationStage.ActiveHosting,
                   AuthorizationScope: ProtagorasAuthorizationScope.SpecificSentence,
                   PlayerSpecificSentenceProposed: true,
                   CompanionAcceptedSpecificSentence: true,
                   ImmediatePausePerformed: true,
                   PracticeCompleted: false,
                   EdgeCommitted: false,
               },
            "Save restore must preserve proposal, authorization, actual pause, and incomplete completion as separate facts.");
    }

    private static void TriggerRequiresTheIntendedRouteAndOneCombat()
    {
        ProtagorasSharedDeliberationTriggerContext eligible = new(
            true, true, true, 0, 1,
            ProtagorasSharedDeliberationState.SourceNodeId,
            1, false, false, false, false, false, true);
        Assert(ProtagorasSharedDeliberationTriggerPolicy.ShouldInsert(eligible),
            "The intended first-act route must open after one completed combat.");
        Assert(!ProtagorasSharedDeliberationTriggerPolicy.ShouldInsert(eligible with { CompletedCombatCount = 0 })
               && !ProtagorasSharedDeliberationTriggerPolicy.ShouldInsert(eligible with { CurrentActIndex = 1 })
               && !ProtagorasSharedDeliberationTriggerPolicy.ShouldInsert(eligible with { CurrentNodeId = "OTHER" })
               && !ProtagorasSharedDeliberationTriggerPolicy.ShouldInsert(eligible with { QuestionResolved = true }),
            "New instances must not open before combat, in another act or route, or after Question resolution.");
        Assert(ProtagorasSharedDeliberationTriggerPolicy.ShouldInsert(eligible with
               {
                   CompletedCombatCount = 0,
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
               })
               && !ProtagorasSharedDeliberationTriggerPolicy.ShouldInsert(eligible with
               {
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
                   SavedInstanceClosed = true,
               }),
            "A valid open instance may resume independently of new-instance eligibility, but a closed one may not reopen.");
    }

    private static void LocalizationCoversEveryPageAndOption()
    {
        string root = Directory.GetCurrentDirectory();
        foreach (string language in new[] { "zhs", "eng" })
        {
            string path = Path.Combine(root, "content", "STS2Philosophers", "localization", language, "events.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement entries = document.RootElement;
            foreach (ProtagorasDeliberationStage stage in Enum.GetValues<ProtagorasDeliberationStage>())
            {
                string page = $"PROTAGORAS_SHARED_DELIBERATION.pages.{ProtagorasSharedDeliberationFlow.StageKey(stage)}";
                Assert(entries.TryGetProperty($"{page}.description", out _),
                    $"Missing {language} page localization for {stage}.");
                foreach (ProtagorasDeliberationAction action in ProtagorasSharedDeliberationFlow.Options(stage))
                {
                    string option = $"{page}.options.{ProtagorasSharedDeliberationFlow.ActionKey(action)}";
                    Assert(entries.TryGetProperty($"{option}.title", out _)
                           && entries.TryGetProperty($"{option}.description", out _),
                        $"Missing {language} option localization for {stage}/{action}.");
                }
            }
        }
    }

    private static (ProtagorasSharedDeliberationState, WesternJourneyState, WesternRouteGraph) NewFixture()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start(ProtagorasSharedDeliberationState.SourceNodeId);
        return (ProtagorasSharedDeliberationState.Create("TEST_INSTANCE"), journey, graph);
    }

    private static void Commit(
        ProtagorasSharedDeliberationState state,
        WesternJourneyState journey,
        WesternRouteGraph graph,
        ProtagorasDeliberationAction action)
    {
        Assert(ProtagorasSharedDeliberationFlow.TryPrepare(state, action),
            $"Action {action} should be legal from {state.Stage}.");
        Assert(ProtagorasSharedDeliberationFlow.TryCommitPending(state, journey, graph),
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
