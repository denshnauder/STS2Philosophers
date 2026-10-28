using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class FichteSelfPositing : EventModel
{
    private const string LocalizationRoot = "FICHTE_SELF_POSITING";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.REVISE_AS_SELF_POSITED"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.REQUIRE_CHECK"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_KANT"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("FichteSelfPositing cannot restore its saved event state.");
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
                Block("FichteSelfPositing run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("FichteSelfPositing saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("FichteSelfPositing entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != FichteSelfPositingAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved FichteSelfPositing choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"FichteSelfPositing entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(FichteSelfPositingState expectedState, string expectedInstanceId,
        FichteSelfPositingStage expectedStage, int expectedActionCount, FichteSelfPositingAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("FichteSelfPositing run state is unavailable.");
                return;
            }
            if (shared.FichteSelfPositing is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != FichteSelfPositingAction.None || state.Closed
                || !FichteSelfPositingFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"FichteSelfPositing prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"FichteSelfPositing choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"FichteSelfPositing committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"FichteSelfPositing choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, FichteSelfPositingState state) =>
        ReferenceEquals(shared.FichteSelfPositing, state) && shared.WesternJourney is { } journey
        && FichteSelfPositingFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(FichteSelfPositingState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(FichteSelfPositingState state)
    {
        string instanceId = state.InstanceId;
        FichteSelfPositingStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return FichteSelfPositingFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{FichteSelfPositingFlow.StageKey(stage)}.options.{FichteSelfPositingFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private FichteSelfPositingState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.FichteSelfPositing is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:3"
            && FichteSelfPositingFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(FichteSelfPositingState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{FichteSelfPositingFlow.DescriptionKey(state)}.description");
}
