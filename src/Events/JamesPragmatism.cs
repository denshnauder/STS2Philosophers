using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

/// <summary>
/// James's independent second-act question after Peirce's fixed room. The
/// earlier in-batch segment remains for old saves and for Peirce's own page;
/// this room gives the same practice a real question-node entry of its own.
/// </summary>
public sealed class JamesPragmatism : EventModel
{
    private const string LocalizationRoot = "JAMES_PRAGMATISM";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.TEST_PUBLICLY"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_PRIVATE"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("JamesPragmatism cannot restore its saved event state.");
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
                Block("JamesPragmatism run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("JamesPragmatism saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("JamesPragmatism entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != JamesPragmatismAction.None
                && (!JamesPragmatismFlow.TryCommitPending(state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved JamesPragmatism choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"JamesPragmatism entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(JamesPragmatismState expectedState, string expectedInstanceId,
        JamesPragmatismStage expectedStage, int expectedActionCount, JamesPragmatismAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("JamesPragmatism run state is unavailable.");
                return;
            }
            if (shared.JamesPragmatism is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != JamesPragmatismAction.None || state.Closed
                || !JamesPragmatismFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"JamesPragmatism prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !JamesPragmatismFlow.TryCommitPending(state))
            {
                Block($"JamesPragmatism choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"JamesPragmatism committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"JamesPragmatism choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private bool IsActiveRoom(RunState run)
    {
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.JamesPragmatism is not { } state) return false;
        return RouteEventMapBoundary.IsActiveEvent(run, this, state.ActIndex - 1);
    }

    private void ShowConfirmedState(JamesPragmatismState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(JamesPragmatismState state)
    {
        string instanceId = state.InstanceId;
        JamesPragmatismStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return JamesPragmatismFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{JamesPragmatismFlow.StageKey(stage)}.options.{JamesPragmatismFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private JamesPragmatismState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.JamesPragmatism is { } state
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:{state.ActIndex}"
            && state.IsUsable()) return state;
        return null;
    }

    private void Block(string reason)
    {
        _ready = false; _blocked = true;
        if (Owner?.RunState is RunState run && IsActiveRoom(run))
        {
            if (!IsFinished) SetEventState(Description ?? InitialDescription, LockedOptions());
            RouteEventMapBoundary.Freeze(run, reason);
        }
        else Log.Error($"[STS2Philosophers] {reason}");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(JamesPragmatismState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{JamesPragmatismFlow.StageKey(state.Stage)}.description");
}
