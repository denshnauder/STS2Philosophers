using STS2Philosophers;

internal static class ZenoAssentPageFlowChecks
{
    private const string SourceKind = "DIOGENES_PUBLIC_ACCOUNT";
    private const string ActionFact = "FACT_RETIRED_ASSENT";
    private const string PriorStatement = "STATEMENT_PRIOR";
    private const string CurrentStatement = "STATEMENT_CURRENT";
    private const string NarrowStatement = "STATEMENT_NARROW";
    private const string LaterOutcome = "OUTCOME_PUBLIC";

    private static readonly ZenoRouteValidationCatalog Catalog = new(
        [new KeyValuePair<string, int>(SourceKind, 1)],
        [ActionFact],
        [PriorStatement, CurrentStatement, NarrowStatement],
        [LaterOutcome]);

    public static void Run()
    {
        RestoresOnlyZ0OrTheCommittedResult();
        MaterialOptionsFollowTheFrozenSnapshot();
        NavigationIsReversibleUntilConfirmation();
        ConfirmationProducesOneOutcomeIntent();
        LocalizationKeysMatchTheLockedContract();
        Console.WriteLine("Zeno assent page checks passed: frozen material options, reversible pages and committed result recovery.");
    }

    private static void RestoresOnlyZ0OrTheCommittedResult()
    {
        ZenoRouteFeatureState active = CreateActive(CreateMaterial());
        Assert(ZenoAssentPageFlow.TryRestore(active, Catalog, out ZenoAssentPageView? initial),
            "An active event must restore from its frozen material.");
        Assert(initial!.Page == ZenoAssentPage.MaterialReview,
            "An uncommitted event must always recover at Z0, not at a draft page.");

        ZenoRouteFeatureState committed = Commit(ZenoRouteStateService.PrepareOutcome(
            active,
            3,
            ZenoAssentOutcome.Narrow,
            NarrowStatement,
            Catalog).State);
        Assert(ZenoAssentPageFlow.TryRestore(committed, Catalog, out ZenoAssentPageView? result),
            "A committed outcome must restore its result page.");
        Assert(result!.Page == ZenoAssentPage.ResultNarrow &&
               result.Options.SequenceEqual([ZenoAssentPageOption.Leave]),
            "A committed Narrow outcome must recover only RESULT_NARROW and its leave action.");

        ZenoRouteFeatureState pending = ZenoRouteStateService.PrepareOutcome(
            active,
            3,
            ZenoAssentOutcome.Keep,
            CurrentStatement,
            Catalog).State;
        Assert(!ZenoAssentPageFlow.TryRestore(pending, Catalog, out _),
            "A prepared transaction must be recovered by the persistence boundary before any page is shown.");
    }

    private static void MaterialOptionsFollowTheFrozenSnapshot()
    {
        AssertScopeOptions(
            CreateMaterial(),
            ZenoAssentMaterialVariant.Material,
            [
                ZenoAssentPageOption.BackToMaterial,
                ZenoAssentPageOption.Keep,
                ZenoAssentPageOption.Narrow,
                ZenoAssentPageOption.Withdraw,
            ]);
        AssertScopeOptions(
            CreateActionWithoutCurrentStatement(),
            ZenoAssentMaterialVariant.Material,
            [ZenoAssentPageOption.BackToMaterial, ZenoAssentPageOption.NoReassent]);
        AssertScopeOptions(
            CreateNoMaterialWithStatement(),
            ZenoAssentMaterialVariant.NoMaterialWithStatement,
            [
                ZenoAssentPageOption.BackToMaterial,
                ZenoAssentPageOption.Keep,
                ZenoAssentPageOption.Narrow,
                ZenoAssentPageOption.Withdraw,
            ]);
        AssertScopeOptions(
            CreateNoMaterialEmpty(),
            ZenoAssentMaterialVariant.NoMaterialEmpty,
            [ZenoAssentPageOption.BackToMaterial, ZenoAssentPageOption.NoAssent]);
    }

    private static void NavigationIsReversibleUntilConfirmation()
    {
        ZenoRouteFeatureState active = CreateActive(CreateMaterial());
        ZenoRouteState route = active.Route!;
        ZenoAssentPageFlow.TryRestore(active, Catalog, out ZenoAssentPageView? z0);

        ZenoAssentPageAction origin = ZenoAssentPageFlow.Choose(
            route,
            z0!,
            ZenoAssentPageOption.ReviewOrigin);
        Assert(origin == new ZenoAssentPageAction(
                ZenoAssentPageActionKind.Navigate,
                ZenoAssentPage.OriginReview),
            "Z0 must navigate to the origin review without writing an outcome.");
        Assert(ZenoAssentPageFlow.TryCreateView(route, origin.NextPage!.Value, out ZenoAssentPageView? zp),
            "The origin review must be renderable from the same frozen route.");
        Assert(ZenoAssentPageFlow.Choose(route, zp!, ZenoAssentPageOption.BackToMaterial).NextPage ==
               ZenoAssentPage.MaterialReview,
            "The origin review must return to Z0 without mutation.");

        ZenoAssentPageAction scope = ZenoAssentPageFlow.Choose(
            route,
            z0!,
            ZenoAssentPageOption.Continue);
        Assert(ZenoAssentPageFlow.TryCreateView(route, scope.NextPage!.Value, out ZenoAssentPageView? z1),
            "Z0 must navigate to the assent scope.");
        ZenoAssentPageAction confirm = ZenoAssentPageFlow.Choose(
            route,
            z1!,
            ZenoAssentPageOption.Withdraw);
        Assert(confirm.NextPage == ZenoAssentPage.ConfirmWithdraw,
            "Withdraw must first open its confirmation page.");
        ZenoAssentPage confirmationPage = confirm.NextPage ??
            throw new InvalidOperationException("Expected a confirmation page.");
        Assert(ZenoAssentPageFlow.TryCreateView(route, confirmationPage, out ZenoAssentPageView? zx) &&
               ZenoAssentPageFlow.Choose(route, zx!, ZenoAssentPageOption.BackToScope).NextPage ==
               ZenoAssentPage.AssentScope,
            "Every confirmation page must put the safe return action first.");
    }

    private static void ConfirmationProducesOneOutcomeIntent()
    {
        AssertConfirmation(CreateMaterial(), ZenoAssentPageOption.Keep, ZenoAssentOutcome.Keep);
        AssertConfirmation(CreateMaterial(), ZenoAssentPageOption.Narrow, ZenoAssentOutcome.Narrow);
        AssertConfirmation(CreateMaterial(), ZenoAssentPageOption.Withdraw, ZenoAssentOutcome.Withdraw);
        AssertConfirmation(
            CreateActionWithoutCurrentStatement(),
            ZenoAssentPageOption.NoReassent,
            ZenoAssentOutcome.NoReassent);
        AssertConfirmation(
            CreateNoMaterialEmpty(),
            ZenoAssentPageOption.NoAssent,
            ZenoAssentOutcome.NoAssent);

        ZenoRouteFeatureState noNarrow = CreateActive(ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [PriorStatement, CurrentStatement],
            CurrentStatement,
            null,
            LaterOutcome));
        ZenoAssentPageFlow.TryCreateView(
            noNarrow.Route!,
            ZenoAssentPage.AssentScope,
            out ZenoAssentPageView? scope);
        Assert(ZenoAssentPageFlow.Choose(
                noNarrow.Route!,
                scope!,
                ZenoAssentPageOption.Narrow).Kind == ZenoAssentPageActionKind.Rejected,
            "Narrow must disappear and remain non-callable when no reviewed narrow statement exists.");
    }

    private static void LocalizationKeysMatchTheLockedContract()
    {
        Assert(ZenoAssentPageFlow.PageKey(ZenoAssentPage.MaterialReview) == "MATERIAL_REVIEW" &&
               ZenoAssentPageFlow.PageKey(ZenoAssentPage.ResultNoAssent) == "RESULT_NO_ASSENT" &&
               ZenoAssentPageFlow.OptionKey(ZenoAssentPageOption.BackToScope) == "BACK_TO_SCOPE" &&
               ZenoAssentPageFlow.OptionKey(ZenoAssentPageOption.NoReassent) == "NO_REASSENT",
            "Page and option keys must remain on the approved ZENO_ASSENT_BOUNDARY matrix.");
    }

    private static void AssertScopeOptions(
        ZenoAssentMaterialSnapshot material,
        ZenoAssentMaterialVariant variant,
        IReadOnlyList<ZenoAssentPageOption> expected)
    {
        ZenoRouteFeatureState active = CreateActive(material);
        Assert(ZenoAssentPageFlow.TryCreateView(
                active.Route!,
                ZenoAssentPage.AssentScope,
                out ZenoAssentPageView? view),
            "The assent scope must be renderable for every approved material shape.");
        Assert(view!.MaterialVariant == variant && view.Options.SequenceEqual(expected),
            "The assent scope must expose only the ordered options allowed by the frozen material.");
    }

    private static void AssertConfirmation(
        ZenoAssentMaterialSnapshot material,
        ZenoAssentPageOption option,
        ZenoAssentOutcome outcome)
    {
        ZenoRouteFeatureState active = CreateActive(material);
        ZenoRouteState route = active.Route!;
        ZenoAssentPageFlow.TryCreateView(route, ZenoAssentPage.AssentScope, out ZenoAssentPageView? scope);
        ZenoAssentPageAction navigation = ZenoAssentPageFlow.Choose(route, scope!, option);
        Assert(navigation.Kind == ZenoAssentPageActionKind.Navigate && navigation.NextPage.HasValue,
            $"{option} must open a confirmation page.");
        ZenoAssentPage confirmationPage = navigation.NextPage ??
            throw new InvalidOperationException("Expected a confirmation page.");
        ZenoAssentPageFlow.TryCreateView(route, confirmationPage, out ZenoAssentPageView? confirmation);
        ZenoAssentPageAction commit = ZenoAssentPageFlow.Choose(
            route,
            confirmation!,
            ZenoAssentPageOption.Confirm);
        Assert(commit.Kind == ZenoAssentPageActionKind.CommitOutcome && commit.Outcome == outcome,
            $"Confirming {option} must emit exactly the {outcome} persistence intent.");
    }

    private static ZenoRouteFeatureState CreateActive(ZenoAssentMaterialSnapshot material)
    {
        ZenoRouteFeatureState waiting = Commit(
            ZenoRouteStateService.PrepareSwitch(CreateUnresolved(), 0, material, Catalog).State);
        ZenoRouteFeatureState opening = Commit(ZenoRouteStateService.PrepareOpeningClaim(
            waiting,
            1,
            ZenoOpeningTrigger.BeforeBoss,
            ZenoResumeDestination.Boss,
            "ACT_THREE_BOSS_001",
            "ZENO_EVENT_001",
            Catalog).State);
        return Commit(ZenoRouteStateService.PrepareEventEstablished(opening, 2, Catalog).State);
    }

    private static ZenoRouteFeatureState Commit(ZenoRouteFeatureState prepared)
    {
        ZenoRoutePendingOperation pending = prepared.PendingOperation ??
            throw new InvalidOperationException("Expected pending operation.");
        return ZenoRouteStateService.Commit(
            prepared,
            pending.OperationId,
            pending.CandidateDigest,
            Catalog).State;
    }

    private static ZenoRouteFeatureState CreateUnresolved() =>
        new(
            ZenoRouteFeatureGeneration.Current,
            new ZenoRouteState(
                ZenoRouteState.CurrentVersion,
                ZenoRouteStage.Unresolved,
                0,
                0,
                "RUN_20260925_PAGE_001",
                2,
                ZenoRouteIds.RouteEdge,
                ZenoRouteIds.TerminalNode,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));

    private static ZenoAssentMaterialSnapshot CreateMaterial() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [PriorStatement, CurrentStatement],
            CurrentStatement,
            NarrowStatement,
            LaterOutcome);

    private static ZenoAssentMaterialSnapshot CreateActionWithoutCurrentStatement() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            ActionFact,
            false,
            [PriorStatement],
            null,
            null,
            LaterOutcome);

    private static ZenoAssentMaterialSnapshot CreateNoMaterialWithStatement() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            null,
            true,
            [PriorStatement, CurrentStatement],
            CurrentStatement,
            NarrowStatement,
            null);

    private static ZenoAssentMaterialSnapshot CreateNoMaterialEmpty() =>
        ZenoRouteStateCodec.CreateMaterialSnapshot(
            1,
            SourceKind,
            null,
            true,
            [],
            null,
            null,
            null);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
