namespace STS2Philosophers;

internal enum ZenoAssentPage
{
    MaterialReview,
    OriginReview,
    AssentScope,
    ConfirmKeep,
    ConfirmNarrow,
    ConfirmWithdraw,
    ConfirmNoReassent,
    ConfirmNoAssent,
    ResultKeep,
    ResultNarrow,
    ResultWithdraw,
    ResultNoReassent,
    ResultNoAssent,
}

internal enum ZenoAssentPageOption
{
    ReviewOrigin,
    Continue,
    BackToMaterial,
    Keep,
    Narrow,
    Withdraw,
    NoReassent,
    NoAssent,
    BackToScope,
    Confirm,
    Leave,
}

internal enum ZenoAssentMaterialVariant
{
    Material,
    NoMaterialWithStatement,
    NoMaterialEmpty,
}

internal enum ZenoAssentPageActionKind
{
    Rejected,
    Navigate,
    CommitOutcome,
    RequestClose,
}

internal sealed record ZenoAssentPageView(
    ZenoAssentPage Page,
    ZenoAssentMaterialVariant MaterialVariant,
    IReadOnlyList<ZenoAssentPageOption> Options);

internal sealed record ZenoAssentPageAction(
    ZenoAssentPageActionKind Kind,
    ZenoAssentPage? NextPage = null,
    ZenoAssentOutcome? Outcome = null);

internal static class ZenoAssentPageFlow
{
    private static readonly ZenoAssentPageAction Rejected =
        new(ZenoAssentPageActionKind.Rejected);

    public static bool TryRestore(
        ZenoRouteFeatureState state,
        ZenoRouteValidationCatalog catalog,
        out ZenoAssentPageView view)
    {
        view = null!;
        if (state.PendingOperation is not null ||
            state.Route is not { } route ||
            !ZenoRouteStateCodec.IsValidFeature(state, catalog))
        {
            return false;
        }

        ZenoAssentPage page = route.Stage switch
        {
            ZenoRouteStage.EventActive => ZenoAssentPage.MaterialReview,
            ZenoRouteStage.OutcomeCommitted when route.Outcome is { } outcome => ResultPage(outcome),
            _ => default,
        };
        if (route.Stage is not (ZenoRouteStage.EventActive or ZenoRouteStage.OutcomeCommitted))
        {
            return false;
        }

        return TryCreateView(route, page, out view);
    }

    public static bool TryCreateView(
        ZenoRouteState route,
        ZenoAssentPage page,
        out ZenoAssentPageView view)
    {
        view = null!;
        if (route.Material is not { } material ||
            string.IsNullOrWhiteSpace(route.EventInstanceId))
        {
            return false;
        }

        bool resultPage = IsResultPage(page);
        if (route.Stage == ZenoRouteStage.EventActive && resultPage ||
            route.Stage == ZenoRouteStage.OutcomeCommitted &&
            (!resultPage || route.Outcome is null || page != ResultPage(route.Outcome.Value)))
        {
            return false;
        }

        if (route.Stage is not (ZenoRouteStage.EventActive or ZenoRouteStage.OutcomeCommitted))
        {
            return false;
        }

        ZenoAssentMaterialVariant variant = MaterialVariant(material);
        IReadOnlyList<ZenoAssentPageOption> options = page switch
        {
            ZenoAssentPage.MaterialReview =>
                [ZenoAssentPageOption.ReviewOrigin, ZenoAssentPageOption.Continue],
            ZenoAssentPage.OriginReview =>
                [ZenoAssentPageOption.BackToMaterial],
            ZenoAssentPage.AssentScope => AssentOptions(material),
            ZenoAssentPage.ConfirmKeep or
            ZenoAssentPage.ConfirmNarrow or
            ZenoAssentPage.ConfirmWithdraw or
            ZenoAssentPage.ConfirmNoReassent or
            ZenoAssentPage.ConfirmNoAssent =>
                [ZenoAssentPageOption.BackToScope, ZenoAssentPageOption.Confirm],
            ZenoAssentPage.ResultKeep or
            ZenoAssentPage.ResultNarrow or
            ZenoAssentPage.ResultWithdraw or
            ZenoAssentPage.ResultNoReassent or
            ZenoAssentPage.ResultNoAssent =>
                [ZenoAssentPageOption.Leave],
            _ => [],
        };

        if (options.Count == 0 ||
            IsConfirmationPage(page) && !IsOutcomeAllowed(material, ConfirmationOutcome(page)))
        {
            return false;
        }

        view = new ZenoAssentPageView(page, variant, options);
        return true;
    }

    public static ZenoAssentPageAction Choose(
        ZenoRouteState route,
        ZenoAssentPageView view,
        ZenoAssentPageOption option)
    {
        if (!TryCreateView(route, view.Page, out ZenoAssentPageView canonicalView) ||
            canonicalView.MaterialVariant != view.MaterialVariant ||
            !canonicalView.Options.SequenceEqual(view.Options) ||
            !canonicalView.Options.Contains(option) ||
            route.Material is not { } material)
        {
            return Rejected;
        }

        return (view.Page, option) switch
        {
            (ZenoAssentPage.MaterialReview, ZenoAssentPageOption.ReviewOrigin) =>
                Navigate(ZenoAssentPage.OriginReview),
            (ZenoAssentPage.MaterialReview, ZenoAssentPageOption.Continue) =>
                Navigate(ZenoAssentPage.AssentScope),
            (ZenoAssentPage.OriginReview, ZenoAssentPageOption.BackToMaterial) =>
                Navigate(ZenoAssentPage.MaterialReview),
            (ZenoAssentPage.AssentScope, ZenoAssentPageOption.BackToMaterial) =>
                Navigate(ZenoAssentPage.MaterialReview),
            (ZenoAssentPage.AssentScope, ZenoAssentPageOption.Keep) =>
                Navigate(ZenoAssentPage.ConfirmKeep),
            (ZenoAssentPage.AssentScope, ZenoAssentPageOption.Narrow) =>
                Navigate(ZenoAssentPage.ConfirmNarrow),
            (ZenoAssentPage.AssentScope, ZenoAssentPageOption.Withdraw) =>
                Navigate(ZenoAssentPage.ConfirmWithdraw),
            (ZenoAssentPage.AssentScope, ZenoAssentPageOption.NoReassent) =>
                Navigate(ZenoAssentPage.ConfirmNoReassent),
            (ZenoAssentPage.AssentScope, ZenoAssentPageOption.NoAssent) =>
                Navigate(ZenoAssentPage.ConfirmNoAssent),
            (_, ZenoAssentPageOption.BackToScope) when IsConfirmationPage(view.Page) =>
                Navigate(ZenoAssentPage.AssentScope),
            (_, ZenoAssentPageOption.Confirm) when IsConfirmationPage(view.Page) &&
                IsOutcomeAllowed(material, ConfirmationOutcome(view.Page)) =>
                new ZenoAssentPageAction(
                    ZenoAssentPageActionKind.CommitOutcome,
                    Outcome: ConfirmationOutcome(view.Page)),
            (_, ZenoAssentPageOption.Leave) when IsResultPage(view.Page) =>
                new ZenoAssentPageAction(ZenoAssentPageActionKind.RequestClose),
            _ => Rejected,
        };
    }

    public static string PageKey(ZenoAssentPage page) => page switch
    {
        ZenoAssentPage.MaterialReview => "MATERIAL_REVIEW",
        ZenoAssentPage.OriginReview => "ORIGIN_REVIEW",
        ZenoAssentPage.AssentScope => "ASSENT_SCOPE",
        ZenoAssentPage.ConfirmKeep => "CONFIRM_KEEP",
        ZenoAssentPage.ConfirmNarrow => "CONFIRM_NARROW",
        ZenoAssentPage.ConfirmWithdraw => "CONFIRM_WITHDRAW",
        ZenoAssentPage.ConfirmNoReassent => "CONFIRM_NO_REASSENT",
        ZenoAssentPage.ConfirmNoAssent => "CONFIRM_NO_ASSENT",
        ZenoAssentPage.ResultKeep => "RESULT_KEEP",
        ZenoAssentPage.ResultNarrow => "RESULT_NARROW",
        ZenoAssentPage.ResultWithdraw => "RESULT_WITHDRAW",
        ZenoAssentPage.ResultNoReassent => "RESULT_NO_REASSENT",
        ZenoAssentPage.ResultNoAssent => "RESULT_NO_ASSENT",
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };

    public static string OptionKey(ZenoAssentPageOption option) => option switch
    {
        ZenoAssentPageOption.ReviewOrigin => "REVIEW_ORIGIN",
        ZenoAssentPageOption.Continue => "CONTINUE",
        ZenoAssentPageOption.BackToMaterial => "BACK_TO_MATERIAL",
        ZenoAssentPageOption.Keep => "KEEP",
        ZenoAssentPageOption.Narrow => "NARROW",
        ZenoAssentPageOption.Withdraw => "WITHDRAW",
        ZenoAssentPageOption.NoReassent => "NO_REASSENT",
        ZenoAssentPageOption.NoAssent => "NO_ASSENT",
        ZenoAssentPageOption.BackToScope => "BACK_TO_SCOPE",
        ZenoAssentPageOption.Confirm => "CONFIRM",
        ZenoAssentPageOption.Leave => "LEAVE",
        _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };

    public static ZenoAssentPage ResultPage(ZenoAssentOutcome outcome) => outcome switch
    {
        ZenoAssentOutcome.Keep => ZenoAssentPage.ResultKeep,
        ZenoAssentOutcome.Narrow => ZenoAssentPage.ResultNarrow,
        ZenoAssentOutcome.Withdraw => ZenoAssentPage.ResultWithdraw,
        ZenoAssentOutcome.NoReassent => ZenoAssentPage.ResultNoReassent,
        ZenoAssentOutcome.NoAssent => ZenoAssentPage.ResultNoAssent,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static IReadOnlyList<ZenoAssentPageOption> AssentOptions(
        ZenoAssentMaterialSnapshot material)
    {
        List<ZenoAssentPageOption> options = [ZenoAssentPageOption.BackToMaterial];
        if (material.CurrentStatementId is not null)
        {
            options.Add(ZenoAssentPageOption.Keep);
            if (material.NarrowStatementId is not null)
            {
                options.Add(ZenoAssentPageOption.Narrow);
            }

            options.Add(ZenoAssentPageOption.Withdraw);
        }
        else
        {
            options.Add(material.IsNoMaterial
                ? ZenoAssentPageOption.NoAssent
                : ZenoAssentPageOption.NoReassent);
        }

        return options;
    }

    private static bool IsOutcomeAllowed(
        ZenoAssentMaterialSnapshot material,
        ZenoAssentOutcome outcome) => outcome switch
    {
        ZenoAssentOutcome.Keep => material.CurrentStatementId is not null,
        ZenoAssentOutcome.Narrow =>
            material.CurrentStatementId is not null && material.NarrowStatementId is not null,
        ZenoAssentOutcome.Withdraw => material.CurrentStatementId is not null,
        ZenoAssentOutcome.NoReassent =>
            !material.IsNoMaterial && material.ActionFactId is not null && material.CurrentStatementId is null,
        ZenoAssentOutcome.NoAssent =>
            material.IsNoMaterial && material.ActionFactId is null && material.CurrentStatementId is null,
        _ => false,
    };

    private static ZenoAssentMaterialVariant MaterialVariant(
        ZenoAssentMaterialSnapshot material) =>
        !material.IsNoMaterial
            ? ZenoAssentMaterialVariant.Material
            : material.CurrentStatementId is not null
                ? ZenoAssentMaterialVariant.NoMaterialWithStatement
                : ZenoAssentMaterialVariant.NoMaterialEmpty;

    private static bool IsConfirmationPage(ZenoAssentPage page) => page is
        ZenoAssentPage.ConfirmKeep or
        ZenoAssentPage.ConfirmNarrow or
        ZenoAssentPage.ConfirmWithdraw or
        ZenoAssentPage.ConfirmNoReassent or
        ZenoAssentPage.ConfirmNoAssent;

    private static bool IsResultPage(ZenoAssentPage page) => page is
        ZenoAssentPage.ResultKeep or
        ZenoAssentPage.ResultNarrow or
        ZenoAssentPage.ResultWithdraw or
        ZenoAssentPage.ResultNoReassent or
        ZenoAssentPage.ResultNoAssent;

    private static ZenoAssentOutcome ConfirmationOutcome(ZenoAssentPage page) => page switch
    {
        ZenoAssentPage.ConfirmKeep => ZenoAssentOutcome.Keep,
        ZenoAssentPage.ConfirmNarrow => ZenoAssentOutcome.Narrow,
        ZenoAssentPage.ConfirmWithdraw => ZenoAssentOutcome.Withdraw,
        ZenoAssentPage.ConfirmNoReassent => ZenoAssentOutcome.NoReassent,
        ZenoAssentPage.ConfirmNoAssent => ZenoAssentOutcome.NoAssent,
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };

    private static ZenoAssentPageAction Navigate(ZenoAssentPage page) =>
        new(ZenoAssentPageActionKind.Navigate, page);
}
