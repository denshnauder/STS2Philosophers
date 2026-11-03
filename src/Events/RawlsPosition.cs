using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// Rawls's act-three fixed room. The player drafts a travel-subsidy rule before
// learning which standing they hold at this waypoint; the standing was fixed
// when the room opened, so a reload cannot redraw it. Reopening the scheme for
// everyone costs gold and is never a private re-roll.
public sealed class RawlsPosition : EventModel
{
    private const string LocalizationRoot = "RAWLS_POSITION";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DRAFT_MINIMUM"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DRAFT_TOTAL_DISCOUNT"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DRAFT_LOTTERY"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("RawlsPosition cannot restore its saved event state.");
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
                Block("RawlsPosition run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("RawlsPosition saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("RawlsPosition entry persistence is unknown."); return;
            }
            if (state.PendingAction != RawlsPositionAction.None
                && (!CommitPending(shared, state) || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved RawlsPosition choice remains blocked for recovery."); return;
            }
            if (!await ApplyAmendCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"RawlsPosition entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(RawlsPositionState expectedState, string expectedInstanceId,
        RawlsPositionStage expectedStage, int expectedActionCount, RawlsPositionAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("RawlsPosition run state is unavailable."); return;
            }
            if (shared.RawlsPosition is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != RawlsPositionAction.None || state.Closed
                || !RawlsPositionFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"RawlsPosition prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"RawlsPosition choice could not commit: {action}."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"RawlsPosition committed choice persistence is unknown: {action}."); return;
            }
            if (!await ApplyAmendCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"RawlsPosition choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, RawlsPositionState state) =>
        ReferenceEquals(shared.RawlsPosition, state) && shared.WesternJourney is { } journey
        && RawlsPositionFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private static int ActiveActIndex(RunState run) =>
        PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) && shared?.RawlsPosition is { } state
            ? Math.Clamp(state.ActIndex, 1, 3) - 1 : 2;

    private bool IsActiveRoom(RunState run) =>
        RouteEventMapBoundary.IsActiveEvent(run, this, ActiveActIndex(run));

    private async Task<bool> ApplyAmendCostAsync(RunState run, PhilosophyRunState shared, RawlsPositionState state)
    {
        if (state.Stage != RawlsPositionStage.ResultAmended) return true;
        EventGoldCostOutcome outcome = await EventGoldCostProtocol.ApplyAsync(
            state,
            () => Owner is null ? null : (decimal?)Owner.Gold,
            async () => await PlayerCmd.LoseGold(state.Cost, Owner!, GoldLossType.Spent),
            () => RouteEventMapBoundary.PersistEventAsync(run, shared));
        switch (outcome)
        {
            case EventGoldCostOutcome.OwnerUnavailable:
                Block("The RawlsPosition amendment has no owner to charge.");
                return false;
            case EventGoldCostOutcome.PersistenceUnknown:
                Block("The RawlsPosition amendment cost persistence is unknown.");
                return false;
            default:
                return true;
        }
    }

    private void ShowConfirmedState(RawlsPositionState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(RawlsPositionState state)
    {
        string instanceId = state.InstanceId;
        RawlsPositionStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        // Reopening the scheme for everyone is only offered when the player can
        // pay for it; it is never a private re-roll of the published rule.
        bool canAmend = stage != RawlsPositionStage.RuleFrozen
            || Owner is null || Owner.Gold >= RawlsPositionState.AmendGoldCost;
        return RawlsPositionFlow.Options(stage)
            .Where(action => action != RawlsPositionAction.AmendUniversally || canAmend)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{RawlsPositionFlow.StageKey(stage)}.options.{RawlsPositionFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private RawlsPositionState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.RawlsPosition is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:{state.ActIndex}"
            && RawlsPositionFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(RawlsPositionState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{RawlsPositionFlow.DescriptionKey(state)}.description");
}
