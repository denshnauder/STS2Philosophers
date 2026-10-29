using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class JamesForcedBridge : EventModel
{
    private const string LocalizationRoot = "JAMES_FORCED_BRIDGE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.ASSESSMENT.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.ASSESSMENT.options.CHECK_CONDITIONS"),
            L10NLookup($"{LocalizationRoot}.pages.ASSESSMENT.options.ASK_KEEPER"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("JamesForcedBridge cannot restore its saved event state.");
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
                Block("JamesForcedBridge run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("JamesForcedBridge saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("JamesForcedBridge entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != JamesForcedBridgeAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved JamesForcedBridge choice remains blocked for recovery.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"JamesForcedBridge entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(JamesForcedBridgeState expectedState, string expectedInstanceId,
        JamesForcedBridgeStage expectedStage, int expectedActionCount, JamesForcedBridgeAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("JamesForcedBridge run state is unavailable.");
                return;
            }
            if (shared.JamesForcedBridge is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != JamesForcedBridgeAction.None || state.Closed
                || !JamesForcedBridgeFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"JamesForcedBridge prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"JamesForcedBridge choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"JamesForcedBridge committed choice persistence is unknown: {action}.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"JamesForcedBridge choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task<bool> ApplyCostAsync(RunState run, PhilosophyRunState shared,
        JamesForcedBridgeState state)
    {
        if (state.Outcome == JamesForcedBridgeOutcome.None) return true;
        EventGoldCostOutcome outcome = await EventGoldCostProtocol.ApplyAsync(
            state,
            () => Owner is null ? null : (decimal?)Owner.Gold,
            async () => await PlayerCmd.LoseGold(state.Cost, Owner!, GoldLossType.Spent),
            () => RouteEventMapBoundary.PersistEventAsync(run, shared));
        switch (outcome)
        {
            case EventGoldCostOutcome.OwnerUnavailable:
                Block("JamesForcedBridge has no owner to charge.");
                return false;
            case EventGoldCostOutcome.PersistenceUnknown:
                Block("JamesForcedBridge cost persistence is unknown.");
                return false;
            default:
                return true;
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, JamesForcedBridgeState state) =>
        ReferenceEquals(shared.JamesForcedBridge, state)
        && shared.WesternJourney is { } journey
        && shared.ZenoRoutePayload.State?.Route is { } route
        && JamesForcedBridgeFlow.TryCommitPending(state, journey, route);

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private void ShowConfirmedState(JamesForcedBridgeState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(JamesForcedBridgeState state)
    {
        string instanceId = state.InstanceId;
        JamesForcedBridgeStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return JamesForcedBridgeFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{JamesForcedBridgeFlow.StageKey(stage)}.options.{JamesForcedBridgeFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private JamesForcedBridgeState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.JamesForcedBridge is { } state
            && shared.WesternJourney is { } journey
            && shared.ZenoRoutePayload.State?.Route is { } route
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2"
            && JamesForcedBridgeFlow.IsRouteConsistent(state, journey, route)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(JamesForcedBridgeState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{JamesForcedBridgeFlow.DescriptionKey(state)}.description");
}
