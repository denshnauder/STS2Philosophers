using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace STS2Philosophers;

public sealed class DiogenesStaySwitch : EventModel, IZenoRouteEventSceneIdentity
{
    private const string LocalizationRoot = "DIOGENES_STAY_SWITCH";

    private IDiogenesStaySwitchStateHost? _stateHost;
    private ZenoRouteValidationCatalog? _catalog;
    private DiogenesStaySwitchPageView? _currentView;
    private DiogenesStaySwitchEntryRecord? _entry;

    ZenoRouteObservedEventKind IZenoRouteEventSceneIdentity.RouteEventKind => ZenoRouteObservedEventKind.Diogenes;
    string? IZenoRouteEventSceneIdentity.RouteEventInstanceId => _entry?.RunId;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(DiogenesStaySwitchPage.RouteChoice);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.ROUTE_CHOICE.options.REVIEW_STATEMENT"),
            L10NLookup($"{LocalizationRoot}.pages.ROUTE_CHOICE.options.REVIEW_PRACTICE"),
            L10NLookup($"{LocalizationRoot}.pages.ROUTE_CHOICE.options.STAY"),
            L10NLookup($"{LocalizationRoot}.pages.ROUTE_CHOICE.options.LEARN_ABOUT_ZENO"),
        };

    internal void Configure(
        IDiogenesStaySwitchStateHost stateHost,
        ZenoRouteValidationCatalog catalog,
        DiogenesStaySwitchEntryRecord? entry = null)
    {
        AssertMutable();
        ArgumentNullException.ThrowIfNull(stateHost);
        ArgumentNullException.ThrowIfNull(catalog);

        _stateHost = stateHost;
        _catalog = catalog;
        _entry = entry;
        _currentView = null;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (_entry?.Closed == true) return [];
        DiogenesStaySwitchPageView view = RestoreViewOrThrow();
        _currentView = view;
        return GenerateOptions(view);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (_entry?.Closed == true)
        {
            FinishResolvedEntry();
            return;
        }
        if (isPreFinished) throw new InvalidOperationException("An unresolved Diogenes record cannot be pre-finished.");

        SetPage(RestoreViewOrThrow());
    }

    internal void FinishResolvedEntry()
    {
        if (IsFinished) return;
        DiogenesStaySwitchPage page = _stateHost?.CurrentState.Route?.Stage == ZenoRouteStage.DiogenesClosed
            ? DiogenesStaySwitchPage.ResultStay : DiogenesStaySwitchPage.ResultSwitch;
        SetEventFinished(PageDescription(page));
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(DiogenesStaySwitchPage page)
    {
        string suffix = page == DiogenesStaySwitchPage.PracticeReview
            ? _entry?.Practice switch
            {
                { Foregone: >= 2 } => ".REPEATED",
                { Foregone: 1 } => ".ONCE",
                { Used: > 0 } => ".USED_SUPPORT",
                _ => ".NO_OBSERVATION",
            }
            : _entry is not null && page is
            DiogenesStaySwitchPage.RouteChoice or DiogenesStaySwitchPage.StatementReview or
            DiogenesStaySwitchPage.ResultStay or DiogenesStaySwitchPage.ResultSwitch
            ? _entry.Facts?.Action switch
            {
                SocratesVirtueAction.Continued => ".CONTINUED",
                SocratesVirtueAction.Retreated => ".RETREATED",
                _ => ".NO_MATERIAL",
            }
            : string.Empty;
        var description = L10NLookup($"{LocalizationRoot}.pages.{DiogenesStaySwitchPageFlow.PageKey(page)}.description{suffix}");
        if (_entry?.Facts is { } facts)
        {
            description.AddObj("HitPointLoss", facts.RetainedHitPointLoss);
            description.AddObj("PotionCount", facts.ConsumedPotionCount);
        }
        if (_entry?.Practice is { } practice)
        {
            description.AddObj("EligibleCount", practice.Eligible);
            description.AddObj("ForegoneCount", practice.Foregone);
            description.AddObj("UsedCount", practice.Used);
        }
        return description;
    }

    private static string OptionLocalizationKey(
        DiogenesStaySwitchPage page,
        DiogenesStaySwitchPageOption option) =>
        $"{LocalizationRoot}.pages.{DiogenesStaySwitchPageFlow.PageKey(page)}.options.{DiogenesStaySwitchPageFlow.OptionKey(option)}";
}
