using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class SpinozaCauseNetwork : EventModel
{
    private const string LocalizationRoot = "SPINOZA_CAUSE_NETWORK";
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
            Block("SpinozaCauseNetwork cannot restore its saved event state.");
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
                Block("SpinozaCauseNetwork run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("SpinozaCauseNetwork saved state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("SpinozaCauseNetwork entry persistence is unknown.");
                return;
            }
            if (!HasValidCarrier(run))
            {
                Block("SpinozaCauseNetwork requires an owned DescartesThinkingLens carrier.");
                return;
            }
            if (state.PendingAction != SpinozaCauseNetworkAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved SpinozaCauseNetwork choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"SpinozaCauseNetwork entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(SpinozaCauseNetworkState expectedState, string expectedInstanceId,
        SpinozaCauseNetworkStage expectedStage, int expectedActionCount, SpinozaCauseNetworkAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("SpinozaCauseNetwork run state is unavailable.");
                return;
            }
            if (!HasValidCarrier(run))
            {
                Block("SpinozaCauseNetwork requires an owned DescartesThinkingLens carrier.");
                return;
            }
            if (shared.SpinozaCauseNetwork is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != SpinozaCauseNetworkAction.None || state.Closed
                || !SpinozaCauseNetworkFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"SpinozaCauseNetwork prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"SpinozaCauseNetwork choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"SpinozaCauseNetwork committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"SpinozaCauseNetwork choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, SpinozaCauseNetworkState state) =>
        ReferenceEquals(shared.SpinozaCauseNetwork, state)
        && shared.WesternJourney is { } journey
        && SpinozaCauseNetworkFlow.TryCommitPending(state, journey,
            WesternRouteGraph.LoadEmbedded());

    private bool HasValidCarrier(RunState run) =>
        run.Players.Count == 1
        && ReferenceEquals(run.Players[0], Owner)
        && Owner is { } owner
        && owner.Relics.OfType<DescartesThinkingLens>().FirstOrDefault() is { } cup
        && cup.CanOfferNativeQuestion
        && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
        && shared is not null
        && shared.CurrentDoctrine is { ThinkerId: "DESCARTES", DoctrineId: "DESCARTES_THINKING_LENS" };

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private void ShowConfirmedState(SpinozaCauseNetworkState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(SpinozaCauseNetworkState state)
    {
        string instanceId = state.InstanceId;
        SpinozaCauseNetworkStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return SpinozaCauseNetworkFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{SpinozaCauseNetworkFlow.StageKey(stage)}.options.{SpinozaCauseNetworkFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private SpinozaCauseNetworkState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.SpinozaCauseNetwork is { } state
            && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2"
            && HasValidCarrier(run)
            && SpinozaCauseNetworkFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(SpinozaCauseNetworkState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{SpinozaCauseNetworkFlow.DescriptionKey(state)}.description");
}
