using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace STS2Philosophers;

internal interface IZenoAssentBoundaryStateHost
{
    ZenoRouteFeatureState CurrentState { get; }

    Task<bool> CommitOutcomeAsync(ZenoAssentOutcome outcome);
}

public sealed class ZenoAssentBoundary : EventModel, IZenoRouteEventSceneIdentity
{
    private const string LocalizationRoot = "ZENO_ASSENT_BOUNDARY";

    private string? _eventInstanceId;
    private IZenoAssentBoundaryStateHost? _stateHost;
    private ZenoRouteValidationCatalog? _catalog;
    private ZenoAssentPageView? _currentView;

    ZenoRouteObservedEventKind IZenoRouteEventSceneIdentity.RouteEventKind =>
        ZenoRouteObservedEventKind.Zeno;

    string? IZenoRouteEventSceneIdentity.RouteEventInstanceId => _eventInstanceId;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.MATERIAL_REVIEW.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.MATERIAL_REVIEW.options.REVIEW_ORIGIN"),
            L10NLookup($"{LocalizationRoot}.pages.MATERIAL_REVIEW.options.CONTINUE"),
        };

    internal void Configure(
        string eventInstanceId,
        IZenoAssentBoundaryStateHost stateHost,
        ZenoRouteValidationCatalog catalog)
    {
        AssertMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventInstanceId);
        ArgumentNullException.ThrowIfNull(stateHost);
        ArgumentNullException.ThrowIfNull(catalog);

        _eventInstanceId = eventInstanceId;
        _stateHost = stateHost;
        _catalog = catalog;
        _currentView = null;
    }

    internal bool RestoreFromCurrentState(ZenoRouteRecoveryPlan plan)
    {
        if (_eventInstanceId is null ||
            !string.Equals(_eventInstanceId, plan.EventInstanceId, StringComparison.Ordinal) ||
            plan.Action is not (
                ZenoRouteRecoveryAction.RestoreMaterialReview or
                ZenoRouteRecoveryAction.RestoreCommittedOutcome) ||
            !TryRestoreView(out ZenoAssentPageView view) ||
            plan.Action == ZenoRouteRecoveryAction.RestoreMaterialReview &&
            view.Page != ZenoAssentPage.MaterialReview ||
            plan.Action == ZenoRouteRecoveryAction.RestoreCommittedOutcome &&
            (!plan.Outcome.HasValue || view.Page != ZenoAssentPageFlow.ResultPage(plan.Outcome.Value)))
        {
            return false;
        }

        SetPage(view);
        return true;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        ZenoAssentPageView view = RestoreViewOrFallback();
        _currentView = view;
        return GenerateOptions(view);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (isPreFinished)
        {
            throw new InvalidOperationException("ZenoAssentBoundary cannot start as a pre-finished event.");
        }

        SetPage(RestoreViewOrFallback());
    }

    private async Task Choose(ZenoAssentPageOption option)
    {
        if (_currentView is not { } view ||
            _stateHost?.CurrentState.Route is not { } route)
        {
            return;
        }

        ZenoAssentPageAction action = ZenoAssentPageFlow.Choose(route, view, option);
        if (action.Kind == ZenoAssentPageActionKind.Navigate &&
            action.NextPage is { } next &&
            ZenoAssentPageFlow.TryCreateView(route, next, out ZenoAssentPageView nextView))
        {
            SetPage(nextView);
            return;
        }

        if (action.Kind == ZenoAssentPageActionKind.CommitOutcome &&
            action.Outcome is { } outcome &&
            await _stateHost.CommitOutcomeAsync(outcome) &&
            TryRestoreView(out ZenoAssentPageView resultView))
        {
            SetPage(resultView);
        }

        // RequestClose is deliberately left for the later close-and-resume stage.
    }

    private void SetPage(ZenoAssentPageView view)
    {
        _currentView = view;
        SetEventState(PageDescription(view), GenerateOptions(view));
    }

    private IReadOnlyList<EventOption> GenerateOptions(ZenoAssentPageView view) =>
        view.Options
            .Select(option => new EventOption(
                this,
                option == ZenoAssentPageOption.Leave ? null : () => Choose(option),
                OptionLocalizationKey(view.Page, option),
                Array.Empty<IHoverTip>()))
            .ToArray();

    private bool TryRestoreView(out ZenoAssentPageView view)
    {
        view = null!;
        return _stateHost is not null &&
            _catalog is not null &&
            ZenoAssentPageFlow.TryRestore(_stateHost.CurrentState, _catalog, out view) &&
            string.Equals(
                _stateHost.CurrentState.Route?.EventInstanceId,
                _eventInstanceId,
                StringComparison.Ordinal);
    }

    private ZenoAssentPageView RestoreViewOrFallback()
    {
        if (!TryRestoreView(out ZenoAssentPageView view))
        {
            throw new InvalidOperationException(
                "ZenoAssentBoundary must be configured with one valid active or committed route instance before it starts.");
        }

        return view;
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(ZenoAssentPageView view)
    {
        string pageKey = ZenoAssentPageFlow.PageKey(view.Page);
        string variant = view.Page == ZenoAssentPage.MaterialReview
            ? view.MaterialVariant switch
            {
                ZenoAssentMaterialVariant.NoMaterialWithStatement => ".NO_MATERIAL_WITH_STATEMENT",
                ZenoAssentMaterialVariant.NoMaterialEmpty => ".NO_MATERIAL_EMPTY",
                _ => string.Empty,
            }
            : string.Empty;
        return L10NLookup($"{LocalizationRoot}.pages.{pageKey}{variant}.description");
    }

    private static string OptionLocalizationKey(
        ZenoAssentPage page,
        ZenoAssentPageOption option) =>
        $"{LocalizationRoot}.pages.{ZenoAssentPageFlow.PageKey(page)}.options.{ZenoAssentPageFlow.OptionKey(option)}";
}
