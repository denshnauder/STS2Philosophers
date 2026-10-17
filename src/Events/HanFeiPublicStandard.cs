using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2Philosophers;

public sealed class HanFeiPublicStandard : EventModel
{
    private const string LocalizationRoot = "HAN_FEI_PUBLIC_STANDARD";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(HanFeiPublicStandardStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        HanFeiPublicStandardFlow.Options(HanFeiPublicStandardStage.Invitation)
            .Select(action => L10NLookup(
                $"{LocalizationRoot}.pages.INVITATION.options.{HanFeiPublicStandardFlow.ActionKey(action)}"));

    // A disabled option keeps the native event open while its awaited start hook
    // confirms the saved room and any pending choice before exposing actions.
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("HanFeiPublicStandard cannot restore its saved event state.");
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
                Block("HanFeiPublicStandard run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("HanFeiPublicStandard saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("HanFeiPublicStandard entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != HanFeiPublicStandardAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved HanFeiPublicStandard choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"HanFeiPublicStandard entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(HanFeiPublicStandardState expectedState, string expectedInstanceId,
        HanFeiPublicStandardStage expectedStage, int expectedActionCount, HanFeiPublicStandardAction action)
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
                Block("HanFeiPublicStandard run state is unavailable.");
                return;
            }
            if (shared.HanFeiPublicStandard is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != HanFeiPublicStandardAction.None || state.Closed
                || !HanFeiPublicStandardFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state.Stage), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"HanFeiPublicStandard prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"HanFeiPublicStandard choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"HanFeiPublicStandard committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"HanFeiPublicStandard choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, HanFeiPublicStandardState state) =>
        ReferenceEquals(shared.HanFeiPublicStandard, state) && HanFeiPublicStandardFlow.TryCommitPending(state);

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(HanFeiPublicStandardState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state.Stage));
        else SetEventState(PageDescription(state.Stage), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(HanFeiPublicStandardState state)
    {
        string instanceId = state.InstanceId;
        HanFeiPublicStandardStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return HanFeiPublicStandardFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{HanFeiPublicStandardFlow.StageKey(stage)}.options.{HanFeiPublicStandardFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private HanFeiPublicStandardState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.HanFeiPublicStandard is { } state && state.IsUsable()) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(
        HanFeiPublicStandardStage stage) =>
        L10NLookup(
            $"{LocalizationRoot}.pages.{HanFeiPublicStandardFlow.StageKey(stage)}.description");
}
