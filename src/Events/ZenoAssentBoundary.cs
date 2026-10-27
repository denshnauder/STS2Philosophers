using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace STS2Philosophers;

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
        if (_stateHost?.CurrentState.Route?.Stage == ZenoRouteStage.ZenoClosed) return [];
        if (_stateHost?.CurrentState.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed) return LockedOptions();
        ZenoAssentPageView view = RestoreViewOrFallback();
        _currentView = view;
        return GenerateOptions(view);
    }

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (_stateHost?.CurrentState.Route?.Stage == ZenoRouteStage.ZenoClosed)
        {
            FinishResolvedRoute();
            return;
        }
        if (_stateHost?.CurrentState.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed)
        {
            SetEventState(InitialDescription, LockedOptions());
            return;
        }
        if (isPreFinished && _stateHost?.CurrentState.Route?.Stage != ZenoRouteStage.OutcomeCommitted)
            throw new InvalidOperationException("An unresolved Zeno record cannot be pre-finished.");

        SetPage(RestoreViewOrFallback());
    }

    public override async Task AfterEventStarted()
    {
        if (_stateHost?.CurrentState.PendingOperation?.Kind == ZenoRouteOperationKind.EventClosed)
            await _stateHost.CloseAndResumeAsync();
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    internal void FinishResolvedRoute()
    {
        if (_stateHost?.CurrentState.Route is not { Stage: ZenoRouteStage.ZenoClosed, Outcome: { } outcome })
            throw new InvalidOperationException("Zeno completion has not been committed.");
        if (IsFinished) return;
        SetEventFinished(L10NLookup($"{LocalizationRoot}.pages.{ZenoAssentPageFlow.PageKey(ZenoAssentPageFlow.ResultPage(outcome))}.description"));
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
            return;
        }

        if (action.Kind == ZenoAssentPageActionKind.RequestClose)
        {
            await _stateHost.CloseAndResumeAsync();
        }
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
                () => Choose(option),
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
        if (view.Page == ZenoAssentPage.MaterialReview &&
            _stateHost?.CurrentState.Route?.Material is { } material &&
            material.SourceKind == DiogenesStaySwitchEntryPolicy.SourceKind)
        {
            variant = material.ActionFactId switch
            {
                DiogenesStaySwitchEntryPolicy.ContinuedFactId => ".CONTINUED_NO_STATEMENT",
                DiogenesStaySwitchEntryPolicy.RetreatedFactId => ".RETREATED_NO_STATEMENT",
                _ => variant,
            };
        }
        return L10NLookup($"{LocalizationRoot}.pages.{pageKey}{variant}.description");
    }

    private static string OptionLocalizationKey(
        ZenoAssentPage page,
        ZenoAssentPageOption option) =>
        $"{LocalizationRoot}.pages.{ZenoAssentPageFlow.PageKey(page)}.options.{ZenoAssentPageFlow.OptionKey(option)}";
}
