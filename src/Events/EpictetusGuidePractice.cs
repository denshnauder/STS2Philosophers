using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2Philosophers;

public sealed class EpictetusGuidePractice : EventModel
{
    private const string LocalizationRoot = "EPICTETUS_GUIDE_PRACTICE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(EpictetusGuidePracticeStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.GUIDE_WITH_RESERVATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.PROMISE_PASSAGE"),
        };

    // A disabled option keeps the native event open while its awaited start hook
    // confirms the saved room and any pending choice before exposing actions.
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("EpictetusGuidePractice cannot restore its saved event state.");
            return;
        }
        SetEventState(PageDescription(state.Stage), LockedOptions());
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
                Block("EpictetusGuidePractice run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("EpictetusGuidePractice saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("EpictetusGuidePractice entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != EpictetusGuidePracticeAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved EpictetusGuidePractice choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"EpictetusGuidePractice entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(EpictetusGuidePracticeState expectedState, string expectedInstanceId,
        EpictetusGuidePracticeStage expectedStage, int expectedActionCount, EpictetusGuidePracticeAction action)
    {
        // Reject an obsolete button before acquiring a gate or touching native run state.
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("EpictetusGuidePractice run state is unavailable.");
                return;
            }
            if (shared.EpictetusGuidePractice is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != EpictetusGuidePracticeAction.None || state.Closed
                || !EpictetusGuidePracticeFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state.Stage), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"EpictetusGuidePractice prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"EpictetusGuidePractice choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"EpictetusGuidePractice committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"EpictetusGuidePractice choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, EpictetusGuidePracticeState state) =>
        ReferenceEquals(shared.EpictetusGuidePractice, state) && shared.ZenoRoutePayload.State?.Route is { } route && shared.SenecaDailyReview is { } seneca
        && EpictetusGuidePracticeFlow.TryCommitPending(state, route, seneca);

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(EpictetusGuidePracticeState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state.Stage));
        else SetEventState(PageDescription(state.Stage), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(EpictetusGuidePracticeState state)
    {
        string instanceId = state.InstanceId;
        EpictetusGuidePracticeStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return EpictetusGuidePracticeFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{EpictetusGuidePracticeFlow.StageKey(stage)}.options.{EpictetusGuidePracticeFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private EpictetusGuidePracticeState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.EpictetusGuidePractice is { } state && shared.ZenoRoutePayload.State?.Route is { } route && shared.SenecaDailyReview is { } seneca
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:1"
            && EpictetusGuidePracticeFlow.IsRouteConsistent(state, route, seneca)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(EpictetusGuidePracticeStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{EpictetusGuidePracticeFlow.StageKey(stage)}.description");
}
