using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2Philosophers;

public sealed class MarcusAureliusCampPractice : EventModel
{
    private const string LocalizationRoot = "MARCUS_AURELIUS_CAMP_PRACTICE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(MarcusAureliusCampPracticeStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ASK_NEED"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.STATE_PLAN"),
        };

    // A disabled option keeps the native event open while its awaited start hook
    // confirms the saved room and any pending choice before exposing actions.
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("MarcusAureliusCampPractice cannot restore its saved event state.");
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
                Block("MarcusAureliusCampPractice run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("MarcusAureliusCampPractice saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("MarcusAureliusCampPractice entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != MarcusAureliusCampPracticeAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved MarcusAureliusCampPractice choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"MarcusAureliusCampPractice entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(MarcusAureliusCampPracticeState expectedState, string expectedInstanceId,
        MarcusAureliusCampPracticeStage expectedStage, int expectedActionCount, MarcusAureliusCampPracticeAction action)
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
                Block("MarcusAureliusCampPractice run state is unavailable.");
                return;
            }
            if (shared.MarcusAureliusCampPractice is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != MarcusAureliusCampPracticeAction.None || state.Closed
                || !MarcusAureliusCampPracticeFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state.Stage), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"MarcusAureliusCampPractice prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"MarcusAureliusCampPractice choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"MarcusAureliusCampPractice committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"MarcusAureliusCampPractice choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, MarcusAureliusCampPracticeState state) =>
        ReferenceEquals(shared.MarcusAureliusCampPractice, state) && shared.ZenoRoutePayload.State?.Route is { } route && shared.EpictetusGuidePractice is { } epictetus
        && MarcusAureliusCampPracticeFlow.TryCommitPending(state, route, epictetus);

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(MarcusAureliusCampPracticeState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state.Stage));
        else SetEventState(PageDescription(state.Stage), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(MarcusAureliusCampPracticeState state)
    {
        string instanceId = state.InstanceId;
        MarcusAureliusCampPracticeStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return MarcusAureliusCampPracticeFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{MarcusAureliusCampPracticeFlow.StageKey(stage)}.options.{MarcusAureliusCampPracticeFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private MarcusAureliusCampPracticeState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.MarcusAureliusCampPractice is { } state && shared.ZenoRoutePayload.State?.Route is { } route && shared.EpictetusGuidePractice is { } epictetus
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:1"
            && MarcusAureliusCampPracticeFlow.IsRouteConsistent(state, route, epictetus)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(MarcusAureliusCampPracticeStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{MarcusAureliusCampPracticeFlow.StageKey(stage)}.description");
}
