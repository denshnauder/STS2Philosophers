using System.Text.Json;

namespace STS2Philosophers;

internal static class DescartesMethodicReconstructionChecks
{
    public static void Run()
    {
        BothTestsSupportLimitedRebuildingOrWithholding();
        DeclineKeepAndStopDoNotPretendToCompleteTheMethod();
        SaveRoundTripPreservesBasisTestAndReconstruction();
        TriggerRequiresTheKnowledgeRouteAfterSextusAtANaturalFork();
        LocalizationCoversEveryPageAndOption();
        EventDoesNotRewardDisbeliefOrChangeDoctrine();
        Console.WriteLine("Descartes methodic reconstruction checks passed.");
    }

    private static void BothTestsSupportLimitedRebuildingOrWithholding()
    {
        (DescartesReconstructionAction test, DescartesReconstructionAction conclusion,
            DescartesReconstructionStage expected)[] paths =
        [
            (DescartesReconstructionAction.InspectCurrentLandmark,
                DescartesReconstructionAction.RebuildCoveredJudgment,
                DescartesReconstructionStage.ResultLandmarkRebuilt),
            (DescartesReconstructionAction.InspectCurrentLandmark,
                DescartesReconstructionAction.KeepUnverifiedClaimsWithheld,
                DescartesReconstructionStage.ResultLandmarkWithheld),
            (DescartesReconstructionAction.CompareIndependentTracks,
                DescartesReconstructionAction.RebuildCoveredJudgment,
                DescartesReconstructionStage.ResultTracksRebuilt),
            (DescartesReconstructionAction.CompareIndependentTracks,
                DescartesReconstructionAction.KeepUnverifiedClaimsWithheld,
                DescartesReconstructionStage.ResultTracksWithheld),
        ];

        foreach (var path in paths)
        {
            DescartesMethodicReconstructionState state = NewAcceptedState();
            Commit(state, DescartesReconstructionAction.WithdrawSharedBasis);
            Commit(state, path.test);
            Commit(state, path.conclusion);
            Assert(state.MethodCompleted
                   && state.SharedBasisWithdrawn
                   && state.VerificationChoice == path.test
                   && state.Stage == path.expected
                   && state.NativeForkPreserved
                   && (state.LimitedJudgmentRebuilt ^ state.UnverifiedClaimsWithheld),
                $"Path {path.test}/{path.conclusion} must produce one bounded reconstruction record.");
        }
    }

    private static void DeclineKeepAndStopDoNotPretendToCompleteTheMethod()
    {
        DescartesMethodicReconstructionState declined = DescartesMethodicReconstructionState.Create("DECLINE");
        Commit(declined, DescartesReconstructionAction.DeclineInvitation);
        Assert(declined.Stage == DescartesReconstructionStage.ResultDeclined
               && !declined.MethodCompleted && declined.FixedSlotResolved,
            "Declining must consume the fixed encounter without inventing method completion.");

        DescartesMethodicReconstructionState kept = NewAcceptedState();
        Commit(kept, DescartesReconstructionAction.KeepBasisProvisional);
        Assert(kept.Stage == DescartesReconstructionStage.ResultKeptBasis
               && kept.OldCopyKeptProvisionally && !kept.MethodCompleted,
            "Keeping the old copy provisional must remain explicit, not be scored as failure or completion.");

        DescartesMethodicReconstructionState stopped = NewAcceptedState();
        Commit(stopped, DescartesReconstructionAction.WithdrawSharedBasis);
        Commit(stopped, DescartesReconstructionAction.StopMethod);
        Assert(stopped.Stage == DescartesReconstructionStage.ResultStopped
               && stopped.SharedBasisWithdrawn && !stopped.MethodCompleted,
            "Stopping after withdrawal must preserve what happened without awarding disbelief.");
    }

    private static void SaveRoundTripPreservesBasisTestAndReconstruction()
    {
        DescartesMethodicReconstructionState state = NewAcceptedState();
        Commit(state, DescartesReconstructionAction.WithdrawSharedBasis);
        Commit(state, DescartesReconstructionAction.CompareIndependentTracks);
        Commit(state, DescartesReconstructionAction.RebuildCoveredJudgment);
        PhilosophyRunState run = new() { DescartesMethodicReconstruction = state };
        DescartesMethodicReconstructionState? restored = PhilosophyRunStateCodec.Decode(
            PhilosophyRunStateCodec.Encode(run)).DescartesMethodicReconstruction;
        Assert(restored is
               {
                   Stage: DescartesReconstructionStage.ResultTracksRebuilt,
                   SharedBasisWithdrawn: true,
                   VerificationChoice: DescartesReconstructionAction.CompareIndependentTracks,
                   LimitedJudgmentRebuilt: true,
                   MethodCompleted: true,
                   NativeForkPreserved: true,
                   Closed: false,
               },
            "Save restore must retain the selected independent test and limited reconstruction separately.");
    }

    private static void TriggerRequiresTheKnowledgeRouteAfterSextusAtANaturalFork()
    {
        DescartesMethodicReconstructionTriggerContext eligible = new(
            true, true, true, 1, 1,
            DescartesMethodicReconstructionState.RequiredNodeId,
            1, false, true, true, true,
            false, false, false, true);
        Assert(DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible),
            "Act two must offer Descartes only after the closed Sextus encounter at a natural fork.");
        Assert(!DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with { CurrentActIndex = 0 })
               && !DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with { CompletedCombatCount = 0 })
               && !DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with { CurrentNodeId = "OTHER" })
               && !DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with { LastFixedAct = 3 })
               && !DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with { FixedActAlreadyDeclined = true })
               && !DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with { HasRequiredRelic = false })
               && !DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with { SextusEncounterClosed = false })
               && !DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with { HasNaturalFork = false }),
            "A new Descartes instance must respect act, route, relic, Sextus, fixed-slot, combat, and fork gates.");
        Assert(DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with
               {
                   LastFixedAct = 2,
                   FixedActAlreadyDeclined = true,
                   HasNaturalFork = false,
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
               })
               && !DescartesMethodicReconstructionTriggerPolicy.ShouldInsert(eligible with
               {
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
                   SavedInstanceClosed = true,
               }),
            "An open saved instance may resume after fixed-slot consumption, but a closed one may not reopen.");

        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start(DescartesMethodicReconstructionState.RequiredNodeId);
        Assert(graph.TryReserveQuestion(journey, 1)
               && graph.TryDecline(journey, "Fixed", 2)
               && journey.LastFixedAct == 2
               && journey.DeclinedFixedActs.SetEquals([2])
               && journey.CurrentNodeId == DescartesMethodicReconstructionState.RequiredNodeId,
            "Presenting the method challenge must consume act two Fixed without adopting a doctrine edge.");
    }

    private static void LocalizationCoversEveryPageAndOption()
    {
        string root = Directory.GetCurrentDirectory();
        foreach (string language in new[] { "zhs", "eng" })
        {
            string path = Path.Combine(root, "content", "STS2Philosophers", "localization", language, "events.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement entries = document.RootElement;
            foreach (DescartesReconstructionStage stage in Enum.GetValues<DescartesReconstructionStage>())
            {
                string page = $"DESCARTES_METHODIC_RECONSTRUCTION.pages.{DescartesMethodicReconstructionFlow.StageKey(stage)}";
                Assert(entries.TryGetProperty($"{page}.description", out _),
                    $"Missing {language} page localization for {stage}.");
                foreach (DescartesReconstructionAction action in DescartesMethodicReconstructionFlow.Options(stage))
                {
                    string option = $"{page}.options.{DescartesMethodicReconstructionFlow.ActionKey(action)}";
                    Assert(entries.TryGetProperty($"{option}.title", out _)
                           && entries.TryGetProperty($"{option}.description", out _),
                        $"Missing {language} option localization for {stage}/{action}.");
                }
            }
        }
    }

    private static void EventDoesNotRewardDisbeliefOrChangeDoctrine()
    {
        string source = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "src", "Events", "DescartesMethodicReconstruction.cs"));
        Assert(!source.Contains("Reward", StringComparison.Ordinal)
               && !source.Contains("RecordCurrentDoctrine", StringComparison.Ordinal)
               && !source.Contains("TryApply", StringComparison.Ordinal),
            "Descartes must not reward disbelief or silently turn one exercise into doctrine adoption.");
    }

    private static DescartesMethodicReconstructionState NewAcceptedState()
    {
        DescartesMethodicReconstructionState state = DescartesMethodicReconstructionState.Create("TEST");
        Commit(state, DescartesReconstructionAction.AcceptInvitation);
        return state;
    }

    private static void Commit(
        DescartesMethodicReconstructionState state,
        DescartesReconstructionAction action)
    {
        Assert(DescartesMethodicReconstructionFlow.TryPrepare(state, action),
            $"Action {action} should be legal from {state.Stage}.");
        Assert(DescartesMethodicReconstructionFlow.TryCommitPending(state),
            $"Action {action} should commit from its saved pending state.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
