namespace STS2Philosophers;

internal enum DiogenesStaySwitchPage
{
    RouteChoice,
    StatementReview,
    ConfirmStay,
    ZenoRelationReview,
    ConfirmSwitch,
    ResultStay,
    ResultSwitch,
}

internal enum DiogenesStaySwitchPageOption
{
    ReviewStatement,
    Stay,
    LearnAboutZeno,
    BackToRouteChoice,
    ConfirmStay,
    ContinueToSwitch,
    ConfirmSwitch,
    Leave,
}

internal enum DiogenesStaySwitchPageActionKind
{
    NoChange,
    Navigate,
    CommitStay,
    CommitSwitch,
    RequestClose,
}

internal sealed record DiogenesStaySwitchPageView(
    DiogenesStaySwitchPage Page,
    IReadOnlyList<DiogenesStaySwitchPageOption> Options);

internal sealed record DiogenesStaySwitchPageAction(
    DiogenesStaySwitchPageActionKind Kind,
    DiogenesStaySwitchPage? NextPage = null);

internal static class DiogenesStaySwitchPageFlow
{
    private static readonly DiogenesStaySwitchPageAction NoChange =
        new(DiogenesStaySwitchPageActionKind.NoChange);

    public static bool TryRestore(
        ZenoRouteFeatureState state,
        ZenoRouteValidationCatalog catalog,
        out DiogenesStaySwitchPageView view)
    {
        view = null!;
        if (state.PendingOperation is not null ||
            state.Route is not { } route ||
            !ZenoRouteStateCodec.IsValidFeature(state, catalog))
        {
            return false;
        }

        DiogenesStaySwitchPage page = route.Stage switch
        {
            ZenoRouteStage.Unresolved => DiogenesStaySwitchPage.RouteChoice,
            ZenoRouteStage.DiogenesClosed => DiogenesStaySwitchPage.ResultStay,
            ZenoRouteStage.WaitingInterval => DiogenesStaySwitchPage.ResultSwitch,
            _ => default,
        };
        if (route.Stage is not (
                ZenoRouteStage.Unresolved or
                ZenoRouteStage.DiogenesClosed or
                ZenoRouteStage.WaitingInterval))
        {
            return false;
        }

        return TryCreateView(route, page, out view);
    }

    public static bool TryCreateView(
        ZenoRouteState route,
        DiogenesStaySwitchPage page,
        out DiogenesStaySwitchPageView view)
    {
        view = null!;
        if (!PageMatchesStage(route.Stage, page))
        {
            return false;
        }

        IReadOnlyList<DiogenesStaySwitchPageOption> options = page switch
        {
            DiogenesStaySwitchPage.RouteChoice =>
                [
                    DiogenesStaySwitchPageOption.ReviewStatement,
                    DiogenesStaySwitchPageOption.Stay,
                    DiogenesStaySwitchPageOption.LearnAboutZeno,
                ],
            DiogenesStaySwitchPage.StatementReview =>
                [DiogenesStaySwitchPageOption.BackToRouteChoice],
            DiogenesStaySwitchPage.ConfirmStay =>
                [
                    DiogenesStaySwitchPageOption.BackToRouteChoice,
                    DiogenesStaySwitchPageOption.ConfirmStay,
                ],
            DiogenesStaySwitchPage.ZenoRelationReview =>
                [
                    DiogenesStaySwitchPageOption.BackToRouteChoice,
                    DiogenesStaySwitchPageOption.ContinueToSwitch,
                ],
            DiogenesStaySwitchPage.ConfirmSwitch =>
                [
                    DiogenesStaySwitchPageOption.BackToRouteChoice,
                    DiogenesStaySwitchPageOption.ConfirmSwitch,
                ],
            DiogenesStaySwitchPage.ResultStay or
            DiogenesStaySwitchPage.ResultSwitch =>
                [DiogenesStaySwitchPageOption.Leave],
            _ => [],
        };
        if (options.Count == 0)
        {
            return false;
        }

        view = new DiogenesStaySwitchPageView(page, options);
        return true;
    }

    public static DiogenesStaySwitchPageAction Choose(
        ZenoRouteState route,
        DiogenesStaySwitchPageView view,
        DiogenesStaySwitchPageOption option)
    {
        if (!TryCreateView(route, view.Page, out DiogenesStaySwitchPageView canonicalView) ||
            !canonicalView.Options.SequenceEqual(view.Options) ||
            !canonicalView.Options.Contains(option))
        {
            return NoChange;
        }

        return (view.Page, option) switch
        {
            (DiogenesStaySwitchPage.RouteChoice, DiogenesStaySwitchPageOption.ReviewStatement) =>
                Navigate(DiogenesStaySwitchPage.StatementReview),
            (DiogenesStaySwitchPage.RouteChoice, DiogenesStaySwitchPageOption.Stay) =>
                Navigate(DiogenesStaySwitchPage.ConfirmStay),
            (DiogenesStaySwitchPage.RouteChoice, DiogenesStaySwitchPageOption.LearnAboutZeno) =>
                Navigate(DiogenesStaySwitchPage.ZenoRelationReview),
            (DiogenesStaySwitchPage.StatementReview, DiogenesStaySwitchPageOption.BackToRouteChoice) or
            (DiogenesStaySwitchPage.ConfirmStay, DiogenesStaySwitchPageOption.BackToRouteChoice) or
            (DiogenesStaySwitchPage.ZenoRelationReview, DiogenesStaySwitchPageOption.BackToRouteChoice) or
            (DiogenesStaySwitchPage.ConfirmSwitch, DiogenesStaySwitchPageOption.BackToRouteChoice) =>
                Navigate(DiogenesStaySwitchPage.RouteChoice),
            (DiogenesStaySwitchPage.ConfirmStay, DiogenesStaySwitchPageOption.ConfirmStay) =>
                new DiogenesStaySwitchPageAction(DiogenesStaySwitchPageActionKind.CommitStay),
            (DiogenesStaySwitchPage.ZenoRelationReview, DiogenesStaySwitchPageOption.ContinueToSwitch) =>
                Navigate(DiogenesStaySwitchPage.ConfirmSwitch),
            (DiogenesStaySwitchPage.ConfirmSwitch, DiogenesStaySwitchPageOption.ConfirmSwitch) =>
                new DiogenesStaySwitchPageAction(DiogenesStaySwitchPageActionKind.CommitSwitch),
            (DiogenesStaySwitchPage.ResultStay, DiogenesStaySwitchPageOption.Leave) or
            (DiogenesStaySwitchPage.ResultSwitch, DiogenesStaySwitchPageOption.Leave) =>
                new DiogenesStaySwitchPageAction(DiogenesStaySwitchPageActionKind.RequestClose),
            _ => NoChange,
        };
    }

    public static DiogenesStaySwitchPageAction Cancel() => NoChange;

    public static string PageKey(DiogenesStaySwitchPage page) => page switch
    {
        DiogenesStaySwitchPage.RouteChoice => "ROUTE_CHOICE",
        DiogenesStaySwitchPage.StatementReview => "STATEMENT_REVIEW",
        DiogenesStaySwitchPage.ConfirmStay => "CONFIRM_STAY",
        DiogenesStaySwitchPage.ZenoRelationReview => "ZENO_RELATION_REVIEW",
        DiogenesStaySwitchPage.ConfirmSwitch => "CONFIRM_SWITCH",
        DiogenesStaySwitchPage.ResultStay => "RESULT_STAY",
        DiogenesStaySwitchPage.ResultSwitch => "RESULT_SWITCH",
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };

    public static string OptionKey(DiogenesStaySwitchPageOption option) => option switch
    {
        DiogenesStaySwitchPageOption.ReviewStatement => "REVIEW_STATEMENT",
        DiogenesStaySwitchPageOption.Stay => "STAY",
        DiogenesStaySwitchPageOption.LearnAboutZeno => "LEARN_ABOUT_ZENO",
        DiogenesStaySwitchPageOption.BackToRouteChoice => "BACK_TO_ROUTE_CHOICE",
        DiogenesStaySwitchPageOption.ConfirmStay => "CONFIRM_STAY",
        DiogenesStaySwitchPageOption.ContinueToSwitch => "CONTINUE_TO_SWITCH",
        DiogenesStaySwitchPageOption.ConfirmSwitch => "CONFIRM_SWITCH",
        DiogenesStaySwitchPageOption.Leave => "LEAVE",
        _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };

    private static bool PageMatchesStage(
        ZenoRouteStage stage,
        DiogenesStaySwitchPage page) => stage switch
    {
        ZenoRouteStage.Unresolved => page is
            DiogenesStaySwitchPage.RouteChoice or
            DiogenesStaySwitchPage.StatementReview or
            DiogenesStaySwitchPage.ConfirmStay or
            DiogenesStaySwitchPage.ZenoRelationReview or
            DiogenesStaySwitchPage.ConfirmSwitch,
        ZenoRouteStage.DiogenesClosed => page == DiogenesStaySwitchPage.ResultStay,
        ZenoRouteStage.WaitingInterval => page == DiogenesStaySwitchPage.ResultSwitch,
        _ => false,
    };

    private static DiogenesStaySwitchPageAction Navigate(DiogenesStaySwitchPage page) =>
        new(DiogenesStaySwitchPageActionKind.Navigate, page);
}
