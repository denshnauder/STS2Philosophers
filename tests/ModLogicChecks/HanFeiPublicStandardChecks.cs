using System.Text.Json;

namespace STS2Philosophers;

internal static class HanFeiPublicStandardChecks
{
    public static void Run()
    {
        XunziAndHanFeiAreBothCompleteEndpoints();
        PublicRuleMustBeDeclaredBeforeCaseDisclosure();
        RevocationPreservesThePublishedRuleHistory();
        ClosingIsIdempotentAndCannotReopenTheRoute();
        SaveRoundTripPreservesPendingAction();
        TriggerRequiresCurrentInkLineAndThirdActCombat();
        LocalizationCoversEveryPageAndOption();
        EventDoesNotInventRewardsOrReplaceTheRelic();
        Console.WriteLine("Han Fei public standard checks passed.");
    }

    private static void XunziAndHanFeiAreBothCompleteEndpoints()
    {
        HanFeiPublicStandardState contextual = HanFeiPublicStandardState.Create("XUNZI_CONTEXT");
        Commit(contextual, HanFeiPublicStandardAction.StayWithXunzi);
        Commit(contextual, HanFeiPublicStandardAction.InterpretRescueAsDuty);
        Assert(contextual is
               {
                   RouteDecision: HanFeiRouteDecision.Xunzi,
                   Stage: HanFeiPublicStandardStage.ResultXunziContext,
                   PracticeCompleted: true,
                   PublicStandardDeclared: false,
               },
            "Contextual judgment must be a complete Xunzi endpoint without a fabricated public rule.");

        HanFeiPublicStandardState role = HanFeiPublicStandardState.Create("XUNZI_ROLE");
        Commit(role, HanFeiPublicStandardAction.StayWithXunzi);
        Commit(role, HanFeiPublicStandardAction.KeepRoleBoundary);
        Assert(role.RouteDecision == HanFeiRouteDecision.Xunzi
               && role.Stage == HanFeiPublicStandardStage.ResultXunziRole,
            "Keeping role boundaries must remain a distinct complete Xunzi judgment.");

        foreach ((HanFeiPublicStandardAction declaration,
                  HanFeiPublicStandardKind standard,
                  HanFeiPublicStandardStage result) in new[]
                 {
                     (HanFeiPublicStandardAction.DeclareQuotaStandard,
                      HanFeiPublicStandardKind.Quota,
                      HanFeiPublicStandardStage.ResultHanFeiQuota),
                     (HanFeiPublicStandardAction.DeclarePublicStockStandard,
                      HanFeiPublicStandardKind.PublicStock,
                      HanFeiPublicStandardStage.ResultHanFeiPublicStock),
                 })
        {
            HanFeiPublicStandardState state = HanFeiPublicStandardState.Create(standard.ToString());
            Commit(state, HanFeiPublicStandardAction.TestPublicStandard);
            Commit(state, declaration);
            Commit(state, HanFeiPublicStandardAction.EnforceDeclaredStandard);
            Assert(state.RouteDecision == HanFeiRouteDecision.HanFei
                   && state.Stage == result
                   && state.DeclaredStandard == standard
                   && state.PublicStandardDeclared
                   && state.PracticeCompleted,
                $"Enforcing {standard} must preserve the declared standard and complete the Han Fei route.");
        }
    }

    private static void PublicRuleMustBeDeclaredBeforeCaseDisclosure()
    {
        HanFeiPublicStandardState state = HanFeiPublicStandardState.Create("ORDER");
        Commit(state, HanFeiPublicStandardAction.TestPublicStandard);
        Assert(!HanFeiPublicStandardFlow.Options(state.Stage).Contains(
                   HanFeiPublicStandardAction.EnforceDeclaredStandard),
            "The player must not enforce a standard before declaring it.");
        Commit(state, HanFeiPublicStandardAction.DeclareQuotaStandard);
        Assert(state.Stage == HanFeiPublicStandardStage.QuotaCase
               && state.PublicStandardDeclared,
            "The concrete case must only appear after the quota rule is public.");
    }

    private static void RevocationPreservesThePublishedRuleHistory()
    {
        HanFeiPublicStandardState state = HanFeiPublicStandardState.Create("REVOKE");
        Commit(state, HanFeiPublicStandardAction.TestPublicStandard);
        Commit(state, HanFeiPublicStandardAction.DeclarePublicStockStandard);
        Commit(state, HanFeiPublicStandardAction.RevokeDeclaredStandard);
        Assert(state is
               {
                   RouteDecision: HanFeiRouteDecision.Xunzi,
                   Stage: HanFeiPublicStandardStage.ResultRuleRevoked,
                   DeclaredStandard: HanFeiPublicStandardKind.PublicStock,
                   PublicStandardDeclared: true,
                   RuleRevokedAfterDisclosure: true,
                   PracticeCompleted: true,
               },
            "Revoking after disclosure must return to Xunzi without erasing the public rule history.");
    }

    private static void SaveRoundTripPreservesPendingAction()
    {
        HanFeiPublicStandardState state = HanFeiPublicStandardState.Create("ROUND_TRIP");
        Commit(state, HanFeiPublicStandardAction.TestPublicStandard);
        Assert(HanFeiPublicStandardFlow.TryPrepare(
                state,
                HanFeiPublicStandardAction.DeclareQuotaStandard),
            "The quota declaration must be preparable before saving.");

        PhilosophyRunState restoredRun = PhilosophyRunStateCodec.Decode(
            PhilosophyRunStateCodec.Encode(new PhilosophyRunState
            {
                HanFeiPublicStandard = state,
            }));
        HanFeiPublicStandardState? restored = restoredRun.HanFeiPublicStandard;
        Assert(restored is
               {
                   Stage: HanFeiPublicStandardStage.RuleDeclaration,
                   PendingAction: HanFeiPublicStandardAction.DeclareQuotaStandard,
               }
               && HanFeiPublicStandardFlow.TryCommitPending(restored)
               && restored.Stage == HanFeiPublicStandardStage.QuotaCase,
            "Save restore must resume exactly the prepared public declaration once.");
    }

    private static void ClosingIsIdempotentAndCannotReopenTheRoute()
    {
        HanFeiPublicStandardState state = HanFeiPublicStandardState.Create("CLOSE");
        Commit(state, HanFeiPublicStandardAction.StayWithXunzi);
        Commit(state, HanFeiPublicStandardAction.KeepRoleBoundary);
        Assert(HanFeiPublicStandardFlow.TryClose(state)
               && state.Closed
               && state.ConfirmedActions.Count(action =>
                   action == HanFeiPublicStandardAction.Leave) == 1
               && !HanFeiPublicStandardFlow.TryClose(state),
            "Leaving a result must close exactly once and prevent a second route entry.");
    }

    private static void TriggerRequiresCurrentInkLineAndThirdActCombat()
    {
        Assert(LaterActRelicEligibilityCatalog.IsEligible(
                LaterActCandidateIds.HanFei,
                LaterActRelicEligibilityCatalog.ActThreeIndex,
                [RouteRelicIds.XunziShengMo]),
            "Ink Line must register Han Fei through the shared third-act eligibility catalog.");
        Assert(!LaterActRelicEligibilityCatalog.IsEligible(
                LaterActCandidateIds.HanFei,
                LaterActRelicEligibilityCatalog.ActThreeIndex,
                [RouteRelicIds.KongziMuduo]),
            "The replaced Muduo must not qualify for Han Fei.");

        HanFeiPublicStandardTriggerContext eligible = new(
            true, true, true, 2, 1, true,
            false, false, false, true);
        Assert(HanFeiPublicStandardTriggerPolicy.ShouldInsert(eligible),
            "The current Ink Line route must offer Han Fei after one third-act combat.");
        Assert(!HanFeiPublicStandardTriggerPolicy.ShouldInsert(
                   eligible with { CurrentActIndex = 1 })
               && !HanFeiPublicStandardTriggerPolicy.ShouldInsert(
                   eligible with { CompletedCombatCount = 0 })
               && !HanFeiPublicStandardTriggerPolicy.ShouldInsert(
                   eligible with { HasRequiredRelic = false }),
            "A new instance must require act three, one combat, and the current Ink Line relic.");
        Assert(HanFeiPublicStandardTriggerPolicy.ShouldInsert(eligible with
               {
                   CompletedCombatCount = 0,
                   HasRequiredRelic = false,
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
               })
               && !HanFeiPublicStandardTriggerPolicy.ShouldInsert(eligible with
               {
                   HasSavedInstance = true,
                   SavedInstanceUsable = true,
                   SavedInstanceClosed = true,
               }),
            "An open saved event may resume, but a closed event must never reopen.");
    }

    private static void LocalizationCoversEveryPageAndOption()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "content",
                    "STS2Philosophers",
                    "localization",
                    language,
                    "events.json")));
            foreach (HanFeiPublicStandardStage stage in
                     Enum.GetValues<HanFeiPublicStandardStage>())
            {
                string page =
                    $"HAN_FEI_PUBLIC_STANDARD.pages.{HanFeiPublicStandardFlow.StageKey(stage)}";
                Assert(document.RootElement.TryGetProperty($"{page}.description", out _),
                    $"Missing {language} page localization for {stage}.");
                foreach (HanFeiPublicStandardAction action in
                         HanFeiPublicStandardFlow.Options(stage))
                {
                    string option =
                        $"{page}.options.{HanFeiPublicStandardFlow.ActionKey(action)}";
                    Assert(document.RootElement.TryGetProperty($"{option}.title", out _)
                           && document.RootElement.TryGetProperty(
                               $"{option}.description",
                               out _),
                        $"Missing {language} option localization for {stage}/{action}.");
                }
            }
        }
    }

    private static void EventDoesNotInventRewardsOrReplaceTheRelic()
    {
        string source = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(),
            "src",
            "Events",
            "HanFeiPublicStandard.cs"));
        Assert(!source.Contains("RelicCmd", StringComparison.Ordinal)
               && !source.Contains("Gold", StringComparison.Ordinal)
               && !source.Contains("Heal", StringComparison.Ordinal)
               && !source.Contains("RecordCurrentDoctrine", StringComparison.Ordinal),
            "The event must preserve the current relic and express the route through committed choices, not rewards.");
    }

    private static void Commit(
        HanFeiPublicStandardState state,
        HanFeiPublicStandardAction action)
    {
        Assert(HanFeiPublicStandardFlow.TryPrepare(state, action),
            $"Action {action} should be legal from {state.Stage}.");
        Assert(HanFeiPublicStandardFlow.TryCommitPending(state),
            $"Action {action} should commit from {state.Stage}.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
