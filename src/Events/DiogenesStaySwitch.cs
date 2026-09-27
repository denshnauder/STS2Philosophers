using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace STS2Philosophers;

public sealed class DiogenesStaySwitch : EventModel
{
    private const string LocalizationRoot = "DIOGENES_STAY_SWITCH";

    private IDiogenesStaySwitchStateHost? _stateHost;
    private ZenoRouteValidationCatalog? _catalog;
    private DiogenesStaySwitchPageView? _currentView;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.ROUTE_CHOICE.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.ROUTE_CHOICE.options.REVIEW_STATEMENT"),
            L10NLookup($"{LocalizationRoot}.pages.ROUTE_CHOICE.options.STAY"),
            L10NLookup($"{LocalizationRoot}.pages.ROUTE_CHOICE.options.LEARN_ABOUT_ZENO"),
        };

    internal void Configure(
        IDiogenesStaySwitchStateHost stateHost,
        ZenoRouteValidationCatalog catalog)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(stateHost);
        ArgumentNullException.ThrowIfNull(catalog);

        _stateHost = stateHost;
        _catalog = catalog;
        _currentView = null;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        DiogenesStaySwitchPageView view = RestoreViewOrThrow();
        _currentView = view;
        return GenerateOptions(view);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            throw new InvalidOperationException("DiogenesStaySwitch cannot start as a pre-finished event.");
        }

        SetPage(RestoreViewOrThrow());
    }

    private async Task Choose(DiogenesStaySwitchPageOption option)
    {
        if (_currentView is not { } view ||
            _stateHost?.CurrentState.Route is not { } route)
        {
            return;
        }

        DiogenesStaySwitchPageAction action =
            DiogenesStaySwitchPageFlow.Choose(route, view, option);
        if (action.Kind == DiogenesStaySwitchPageActionKind.Navigate &&
            action.NextPage is { } next &&
            DiogenesStaySwitchPageFlow.TryCreateView(
                route,
                next,
                out DiogenesStaySwitchPageView nextView))
        {
            SetPage(nextView);
            return;
        }

        bool committed = action.Kind switch
        {
            DiogenesStaySwitchPageActionKind.CommitStay =>
                await _stateHost.CommitStayAsync(),
            DiogenesStaySwitchPageActionKind.CommitSwitch =>
                await _stateHost.CommitSwitchAsync(),
            _ => false,
        };
        if (committed && TryRestoreView(out DiogenesStaySwitchPageView resultView))
        {
            SetPage(resultView);
            return;
        }

        if (action.Kind == DiogenesStaySwitchPageActionKind.RequestClose)
        {
            await _stateHost.CloseAndResumeAsync();
        }
    }

    private void SetPage(DiogenesStaySwitchPageView view)
    {
        _currentView = view;
        SetEventState(PageDescription(view.Page), GenerateOptions(view));
    }

    private IReadOnlyList<EventOption> GenerateOptions(DiogenesStaySwitchPageView view) =>
        view.Options
            .Select(option => new EventOption(
                this,
                () => Choose(option),
                OptionLocalizationKey(view.Page, option),
                Array.Empty<IHoverTip>()))
            .ToArray();

    private bool TryRestoreView(out DiogenesStaySwitchPageView view)
    {
        view = null!;
        return _stateHost is not null &&
            _catalog is not null &&
            DiogenesStaySwitchPageFlow.TryRestore(_stateHost.CurrentState, _catalog, out view);
    }

    private DiogenesStaySwitchPageView RestoreViewOrThrow()
    {
        if (!TryRestoreView(out DiogenesStaySwitchPageView view))
        {
            throw new InvalidOperationException(
                "DiogenesStaySwitch requires one unresolved or committed route without a pending transaction.");
        }

        return view;
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(
        DiogenesStaySwitchPage page) =>
        L10NLookup($"{LocalizationRoot}.pages.{DiogenesStaySwitchPageFlow.PageKey(page)}.description");

    private static string OptionLocalizationKey(
        DiogenesStaySwitchPage page,
        DiogenesStaySwitchPageOption option) =>
        $"{LocalizationRoot}.pages.{DiogenesStaySwitchPageFlow.PageKey(page)}.options.{DiogenesStaySwitchPageFlow.OptionKey(option)}";
}
