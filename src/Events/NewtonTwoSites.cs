using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class NewtonTwoSites : EventModel
{
    private const string LocalizationRoot = "NEWTON_TWO_SITES";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
    [
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT"),
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE"),
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("NewtonTwoSites cannot restore its saved event state.");
            return;
        }
        SetEventState(PageDescription(state), LockedOptions());
    }

    public override Task AfterEventStarted() => ResumeAfterEntryAsync();

    private async Task ResumeAfterEntryAsync()
    {
        if (_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("NewtonTwoSites run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("NewtonTwoSites saved state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("NewtonTwoSites entry persistence is unknown.");
                return;
            }
            if (!HasValidCarrier(run))
            {
                Block("NewtonTwoSites requires an owned SocratesQuestionCup carrier.");
                return;
            }
            if (state.PendingAction != NewtonTwoSitesAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved NewtonTwoSites choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"NewtonTwoSites entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(NewtonTwoSitesState expectedState, string expectedInstanceId,
        NewtonTwoSitesStage expectedStage, int expectedActionCount, NewtonTwoSitesAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("NewtonTwoSites run state is unavailable.");
                return;
            }
            if (!HasValidCarrier(run))
            {
                Block("NewtonTwoSites requires an owned SocratesQuestionCup carrier.");
                return;
            }
            if (shared.NewtonTwoSites is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != NewtonTwoSitesAction.None || state.Closed
                || !NewtonTwoSitesFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"NewtonTwoSites prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"NewtonTwoSites choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"NewtonTwoSites committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"NewtonTwoSites choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, NewtonTwoSitesState state) =>
        ReferenceEquals(shared.NewtonTwoSites, state)
        && shared.WesternJourney is { } journey
        && shared.SextusOpposedAppearances is { } sextus
        && shared.DescartesMethodicReconstruction is { } descartes
        && NewtonTwoSitesFlow.TryCommitPending(state, journey, sextus, descartes,
            WesternRouteGraph.LoadEmbedded());

    private bool HasValidCarrier(RunState run) =>
        run.Players.Count == 1
        && ReferenceEquals(run.Players[0], Owner)
        && Owner is { } owner
        && owner.Relics.OfType<SocratesQuestionCup>().FirstOrDefault() is { } cup
        && cup.CanOfferNativeQuestion;

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(NewtonTwoSitesState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(NewtonTwoSitesState state)
    {
        string instanceId = state.InstanceId;
        NewtonTwoSitesStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return NewtonTwoSitesFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{NewtonTwoSitesFlow.StageKey(stage)}.options.{NewtonTwoSitesFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private NewtonTwoSitesState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.NewtonTwoSites is { } state
            && shared.WesternJourney is { } journey
            && shared.SextusOpposedAppearances is { } sextus
            && shared.DescartesMethodicReconstruction is { } descartes
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:3"
            && HasValidCarrier(run)
            && NewtonTwoSitesFlow.IsRouteConsistent(state, journey, sextus, descartes)) return state;
        return null;
    }

    private void Block(string reason)
    {
        _ready = false;
        _blocked = true;
        if (Owner?.RunState is RunState run && IsActiveRoom(run))
        {
            if (!IsFinished) SetEventState(Description ?? InitialDescription, LockedOptions());
            RouteEventMapBoundary.Freeze(run, reason);
        }
        else Log.Error($"[STS2Philosophers] {reason}");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(NewtonTwoSitesState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{NewtonTwoSitesFlow.DescriptionKey(state)}.description");
}
