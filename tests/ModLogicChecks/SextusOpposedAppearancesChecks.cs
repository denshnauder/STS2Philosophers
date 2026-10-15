using System.Text.Json;

namespace STS2Philosophers;

internal static class SextusOpposedAppearancesChecks
{
    public static void Run()
    {
        EveryJudgmentPreservesActionWithoutChoosingAWinner();
        DeclineConsumesTheQuestionWithoutInventingAJudgment();
        SaveRoundTripPreservesTheSubmittedJudgment();
        TriggerRequiresAKnowledgeRouteNaturalFork();
        LocalizationCoversEveryPageAndOption();
        EventDoesNotRewardInactionOrChangeDoctrine();
        Console.WriteLine("Sextus opposed appearances checks passed.");
    }

    private static void EveryJudgmentPreservesActionWithoutChoosingAWinner()
    {
        (SextusAppearancesAction action, SextusAppearancesStage result)[] judgments =
        [
            (SextusAppearancesAction.AgreeLampA, SextusAppearancesStage.ResultAgreeLampA),
            (SextusAppearancesAction.AgreeLampB, SextusAppearancesStage.ResultAgreeLampB),
            (SextusAppearancesAction.SuspendAssent, SextusAppearancesStage.ResultSuspend),
            (SextusAppearancesAction.ContinueInquiry, SextusAppearancesStage.ResultContinueInquiry),
            (SextusAppearancesAction.RejectProposal, SextusAppearancesStage.ResultRejectProposal),
        ];

        foreach ((SextusAppearancesAction action, SextusAppearancesStage result) in judgments)
        {
            SextusOpposedAppearancesState state = SextusOpposedAppearancesState.Create("TEST");
            Commit(state, SextusAppearancesAction.AcceptInvitation);
            Commit(state, action);
            Assert(state is
                   {
                       InvitationAccepted: true,
                       JudgmentSubmitted: true,
                       NativeForkPreserved: true,
                       Closed: false,
                   }
                   && state.SubmittedJudgment == action
                   && state.Stage == result
                   && SextusOpposedAppearancesFlow.Options(state.Stage)
                       .SequenceEqual([SextusAppearancesAction.Leave]),
                $"Judgment {action} must preserve the same native route action without ranking a winner.");
            Assert(!SextusOpposedAppearancesFlow.TryPrepare(state, action),
                "A submitted judgment must not be repeatable from its result page.");
        }
    }

    private static void DeclineConsumesTheQuestionWithoutInventingAJudgment()
    {
        SextusOpposedAppearancesState state = SextusOpposedAppearancesState.Create("DECLINE");
        Commit(state, SextusAppearancesAction.DeclineInvitation);
        Assert(state is
               {
                   Stage: SextusAppearancesStage.ResultDeclined,
                   QuestionSlotReserved: true,
                   JudgmentSubmitted: false,
                   SubmittedJudgment: SextusAppearancesAction.None,
                   NativeForkPreserved: true,
               },
            "Declining Sextus must consume the one encounter without fabricating assent or suspension.");
    }

    private static void SaveRoundTripPreservesTheSubmittedJudgment()
    {
        SextusOpposedAppearancesState state = SextusOpposedAppearancesState.Create("ROUND_TRIP");
        Commit(state, SextusAppearancesAction.AcceptInvitation);
        Commit(state, SextusAppearancesAction.SuspendAssent);
        PhilosophyRunState run = new() { SextusOpposedAppearances = state };
        SextusOpposedAppearancesState? restored = PhilosophyRunStateCodec.Decode(
            PhilosophyRunStateCodec.Encode(run)).SextusOpposedAppearances;
        Assert(restored is
               {
                   InstanceId: "ROUND_TRIP",
                   Stage: SextusAppearancesStage.ResultSuspend,
                   InvitationAccepted: true,
                   JudgmentSubmitted: true,
                   SubmittedJudgment: SextusAppearancesAction.SuspendAssent,
                   NativeForkPreserved: true,
                   Closed: false,
               },
            "Save restore must keep the explicit narrow judgment and unchanged-fork fact.");
    }

    private static void TriggerRequiresAKnowledgeRouteNaturalFork()
    {
        SextusOpposedAppearancesTriggerContext eligible = new(
            true, true, true, 0, 1,
            SextusOpposedAppearancesState.RequiredNodeId,
            1, false, true, true,
            false, false, false, true);
        Assert(SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible),
            "The first-act Socrates knowledge route must open Sextus only at a real fork after combat.");
        Assert(!SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible with { CurrentActIndex = 1 })
               && !SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible with { CompletedCombatCount = 0 })
               && !SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible with { CurrentNodeId = "OTHER" })
               && !SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible with { LastFixedAct = 2 })
               && !SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible with { QuestionAlreadyResolved = true })
               && !SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible with { HasRequiredRelic = false })
               && !SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible with { HasNaturalFork = false }),
            "A new Sextus instance must respect act, combat, route, relic, question-slot, and natural-fork gates.");
        Assert(SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible with
               {
                   HasNaturalFork = false,
                   QuestionAlreadyResolved = true,
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
               })
               && !SextusOpposedAppearancesTriggerPolicy.ShouldInsert(eligible with
               {
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
                   SavedInstanceClosed = true,
               }),
            "An open saved instance may resume after reservation, but a closed one may not reopen.");

        WesternJourneyState journey = WesternRouteGraph.LoadEmbedded().Start(
            SextusOpposedAppearancesState.RequiredNodeId);
        Assert(WesternRouteGraph.LoadEmbedded().TryReserveQuestion(journey, 1)
               && journey.ResolvedQuestionActs.SetEquals([1])
               && journey.CurrentNodeId == SextusOpposedAppearancesState.RequiredNodeId,
            "Presenting the challenge must reserve the one question slot without switching doctrine.");
    }

    private static void LocalizationCoversEveryPageAndOption()
    {
        string root = Directory.GetCurrentDirectory();
        foreach (string language in new[] { "zhs", "eng" })
        {
            string path = Path.Combine(root, "content", "STS2Philosophers", "localization", language, "events.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement entries = document.RootElement;
            foreach (SextusAppearancesStage stage in Enum.GetValues<SextusAppearancesStage>())
            {
                string page = $"SEXTUS_OPPOSED_APPEARANCES.pages.{SextusOpposedAppearancesFlow.StageKey(stage)}";
                Assert(entries.TryGetProperty($"{page}.description", out _),
                    $"Missing {language} page localization for {stage}.");
                foreach (SextusAppearancesAction action in SextusOpposedAppearancesFlow.Options(stage))
                {
                    string option = $"{page}.options.{SextusOpposedAppearancesFlow.ActionKey(action)}";
                    Assert(entries.TryGetProperty($"{option}.title", out _)
                           && entries.TryGetProperty($"{option}.description", out _),
                        $"Missing {language} option localization for {stage}/{action}.");
                }
            }
        }
    }

    private static void EventDoesNotRewardInactionOrChangeDoctrine()
    {
        string source = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "src", "Events", "SextusOpposedAppearances.cs"));
        Assert(!source.Contains("Reward", StringComparison.Ordinal)
               && !source.Contains("RecordCurrentDoctrine", StringComparison.Ordinal)
               && !source.Contains("TryCompleteReservedQuestion", StringComparison.Ordinal),
            "Sextus must not reward suspension or silently turn the encounter into route adoption.");
    }

    private static void Commit(SextusOpposedAppearancesState state, SextusAppearancesAction action)
    {
        Assert(SextusOpposedAppearancesFlow.TryPrepare(state, action),
            $"Action {action} should be legal from {state.Stage}.");
        Assert(SextusOpposedAppearancesFlow.TryCommitPending(state),
            $"Action {action} should commit from its saved pending state.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
