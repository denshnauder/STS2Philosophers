using System.Text.Json;

namespace STS2Philosophers;

internal static class EpicurusSharedMarginChecks
{
    public static void Run()
    {
        FinalResponsesPreserveDifferentRelationshipFacts();
        DeclineAndStopDoNotCompletePractice();
        SaveRoundTripPreservesPendingRelationship();
        TriggerRequiresCurrentRouteRelicAndThirdActCombat();
        LocalizationCoversEveryPageAndOption();
        EventDoesNotInventDoctrineOrRewards();
        Console.WriteLine("Epicurus shared margin checks passed.");
    }

    private static void FinalResponsesPreserveDifferentRelationshipFacts()
    {
        EpicurusMarginAction[][] paths =
        [
            [EpicurusMarginAction.AcceptInvitation, EpicurusMarginAction.OfferMutualGuarantee, EpicurusMarginAction.HonorRequest],
            [EpicurusMarginAction.AcceptInvitation, EpicurusMarginAction.OfferMutualGuarantee, EpicurusMarginAction.RefuseRequest],
            [EpicurusMarginAction.AcceptInvitation, EpicurusMarginAction.KeepSeparateMargins, EpicurusMarginAction.AdjustVoluntarily],
            [EpicurusMarginAction.AcceptInvitation, EpicurusMarginAction.KeepSeparateMargins, EpicurusMarginAction.KeepPlansSeparate],
        ];

        foreach (EpicurusMarginAction[] path in paths)
        {
            EpicurusSharedMarginState state = EpicurusSharedMarginState.Create("TEST");
            foreach (EpicurusMarginAction action in path) Commit(state, action);
            Assert(state.PracticeCompleted && state.ConfirmedActions.SequenceEqual(path),
                $"Path {string.Join('/', path)} must complete exactly its own saved actions.");
            Assert(!EpicurusSharedMarginFlow.TryPrepare(state, path[^1]),
                "A result page must not repeat its final response.");
        }
    }

    private static void DeclineAndStopDoNotCompletePractice()
    {
        EpicurusSharedMarginState declined = EpicurusSharedMarginState.Create("DECLINE");
        Commit(declined, EpicurusMarginAction.DeclineInvitation);
        EpicurusSharedMarginState stopped = EpicurusSharedMarginState.Create("STOP");
        Commit(stopped, EpicurusMarginAction.AcceptInvitation);
        Commit(stopped, EpicurusMarginAction.StopPractice);
        Assert(declined.Stage == EpicurusMarginStage.ResultDeclined && !declined.PracticeCompleted
               && stopped.Stage == EpicurusMarginStage.ResultStopped && !stopped.PracticeCompleted,
            "Declining or stopping must not fabricate a completed Epicurean practice.");
    }

    private static void SaveRoundTripPreservesPendingRelationship()
    {
        EpicurusSharedMarginState state = EpicurusSharedMarginState.Create("ROUND_TRIP");
        Commit(state, EpicurusMarginAction.AcceptInvitation);
        Commit(state, EpicurusMarginAction.OfferMutualGuarantee);
        PhilosophyRunState run = new() { EpicurusSharedMargin = state };
        EpicurusSharedMarginState? saved = PhilosophyRunStateCodec.Decode(
            PhilosophyRunStateCodec.Encode(run)).EpicurusSharedMargin;
        Assert(saved is
               {
                   Stage: EpicurusMarginStage.MutualRequest,
                   MutualGuaranteeOffered: true,
                   CompanionRequestedAdjustment: true,
                   RequestHonored: false,
                   RequestRefused: false,
                   PracticeCompleted: false,
               },
            "Save restore must keep guarantee, request, and response as separate facts.");
    }

    private static void TriggerRequiresCurrentRouteRelicAndThirdActCombat()
    {
        EpicurusSharedMarginTriggerContext eligible = new(
            true, true, true, 2, 1,
            EpicurusSharedMarginState.RequiredNodeId, true, true,
            false, false, false, true);
        Assert(EpicurusSharedMarginTriggerPolicy.ShouldInsert(eligible),
            "The deployed Plato virtue route must offer Epicurus after one third-act combat.");
        Assert(!EpicurusSharedMarginTriggerPolicy.ShouldInsert(eligible with { CurrentActIndex = 1 })
               && !EpicurusSharedMarginTriggerPolicy.ShouldInsert(eligible with { CompletedCombatCount = 0 })
               && !EpicurusSharedMarginTriggerPolicy.ShouldInsert(eligible with { CurrentNodeId = "OTHER" })
               && !EpicurusSharedMarginTriggerPolicy.ShouldInsert(eligible with { RequiredEdgeCompleted = false })
               && !EpicurusSharedMarginTriggerPolicy.ShouldInsert(eligible with { HasRequiredRelic = false }),
            "A new instance must require the third-act route, predecessor edge, current relic, and one combat.");
        Assert(EpicurusSharedMarginTriggerPolicy.ShouldInsert(eligible with
               {
                   CompletedCombatCount = 0,
                   HasRequiredRelic = false,
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
               })
               && !EpicurusSharedMarginTriggerPolicy.ShouldInsert(eligible with
               {
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
                   SavedInstanceClosed = true,
               }),
            "An open saved instance may resume, but a closed instance may not reopen.");
    }

    private static void LocalizationCoversEveryPageAndOption()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(Directory.GetCurrentDirectory(), "content", "STS2Philosophers", "localization", language, "events.json")));
            foreach (EpicurusMarginStage stage in Enum.GetValues<EpicurusMarginStage>())
            {
                string page = $"EPICURUS_SHARED_MARGIN.pages.{EpicurusSharedMarginFlow.StageKey(stage)}";
                Assert(document.RootElement.TryGetProperty($"{page}.description", out _),
                    $"Missing {language} page localization for {stage}.");
                foreach (EpicurusMarginAction action in EpicurusSharedMarginFlow.Options(stage))
                {
                    string option = $"{page}.options.{EpicurusSharedMarginFlow.ActionKey(action)}";
                    Assert(document.RootElement.TryGetProperty($"{option}.title", out _)
                           && document.RootElement.TryGetProperty($"{option}.description", out _),
                        $"Missing {language} option localization for {stage}/{action}.");
                }
            }
        }
    }

    private static void EventDoesNotInventDoctrineOrRewards()
    {
        string source = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "src", "Events", "EpicurusSharedMargin.cs"));
        Assert(!source.Contains("RecordCurrentDoctrine", StringComparison.Ordinal)
               && !source.Contains("RelicCmd", StringComparison.Ordinal)
               && !source.Contains("Gold", StringComparison.Ordinal)
               && !source.Contains("Heal", StringComparison.Ordinal),
            "The event must not invent a doctrine relic or numeric pleasure reward.");
    }

    private static void Commit(EpicurusSharedMarginState state, EpicurusMarginAction action)
    {
        Assert(EpicurusSharedMarginFlow.TryPrepare(state, action),
            $"Action {action} should be legal from {state.Stage}.");
        Assert(EpicurusSharedMarginFlow.TryCommitPending(state),
            $"Action {action} should commit from its saved pending state.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
