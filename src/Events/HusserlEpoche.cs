using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class HusserlEpoche : EventModel
{
    private const string LocalizationRoot = "HUSSERL_EPOCHE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DESCRIBE_APPEARANCE"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.SUSPEND_THESIS"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.TURN_EMBODIED"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_DESCARTES"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("HusserlEpoche cannot restore its saved event state.");
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
                Block("HusserlEpoche run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("HusserlEpoche saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("HusserlEpoche entry persistence is unknown."); return;
            }
            if (state.PendingAction != HusserlEpocheAction.None
                && (!CommitPending(shared, state) || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved HusserlEpoche choice remains blocked for recovery."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"HusserlEpoche entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(HusserlEpocheState expectedState, string expectedInstanceId,
        HusserlEpocheStage expectedStage, int expectedActionCount, HusserlEpocheAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("HusserlEpoche run state is unavailable."); return;
            }
            if (shared.HusserlEpoche is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != HusserlEpocheAction.None || state.Closed
                || !HusserlEpocheFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"HusserlEpoche prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"HusserlEpoche choice could not commit: {action}."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"HusserlEpoche committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"HusserlEpoche choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, HusserlEpocheState state) =>
        ReferenceEquals(shared.HusserlEpoche, state) && shared.WesternJourney is { } journey
        && HusserlEpocheFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 0);

    private void ShowConfirmedState(HusserlEpocheState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(HusserlEpocheState state)
    {
        string instanceId = state.InstanceId;
        HusserlEpocheStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return HusserlEpocheFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{HusserlEpocheFlow.StageKey(stage)}.options.{HusserlEpocheFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private HusserlEpocheState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.HusserlEpoche is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:1"
            && HusserlEpocheFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(HusserlEpocheState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{HusserlEpocheFlow.DescriptionKey(state)}.description");

}
