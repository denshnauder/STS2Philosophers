using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// Butler's act-three fixed room. A waypoint ledger decides who may claim, sign
// and speak for others; a newcomer does not fit the two titles it keeps reusing.
// The player can reprint the table, let the person propose a workable procedure,
// or keep the old table and see who it stops. Nothing about the person is stored
// beyond what they choose to say in public.
public sealed class ButlerAddress : EventModel
{
    private const string LocalizationRoot = "BUTLER_ADDRESS";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.OBSERVE_LEDGER"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_LEDGER"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("ButlerAddress cannot restore its saved event state.");
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
                Block("ButlerAddress run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("ButlerAddress saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("ButlerAddress entry persistence is unknown."); return;
            }
            if (state.PendingAction != ButlerAddressAction.None
                && (!CommitPending(shared, state) || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved ButlerAddress choice remains blocked for recovery."); return;
            }
            if (!await ApplyReprintCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"ButlerAddress entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(ButlerAddressState expectedState, string expectedInstanceId,
        ButlerAddressStage expectedStage, int expectedActionCount, ButlerAddressAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("ButlerAddress run state is unavailable."); return;
            }
            if (shared.ButlerAddress is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != ButlerAddressAction.None || state.Closed
                || !ButlerAddressFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"ButlerAddress prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"ButlerAddress choice could not commit: {action}."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"ButlerAddress committed choice persistence is unknown: {action}."); return;
            }
            if (!await ApplyReprintCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"ButlerAddress choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, ButlerAddressState state) =>
        ReferenceEquals(shared.ButlerAddress, state) && shared.WesternJourney is { } journey
        && ButlerAddressFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    // The record says which act it belongs to, so the same body can serve the
    // room the boundary opens in the act it names.
    private static int ActiveActIndex(RunState run) =>
        PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) && shared?.ButlerAddress is { } state
            ? Math.Clamp(state.ActIndex, 1, 3) - 1 : 2;

    private bool IsActiveRoom(RunState run) =>
        RouteEventMapBoundary.IsActiveEvent(run, this, ActiveActIndex(run));

    private async Task<bool> ApplyReprintCostAsync(RunState run, PhilosophyRunState shared, ButlerAddressState state)
    {
        if (state.Stage != ButlerAddressStage.ResultAmended) return true;
        EventGoldCostOutcome outcome = await EventGoldCostProtocol.ApplyAsync(
            state,
            () => Owner is null ? null : (decimal?)Owner.Gold,
            async () => await PlayerCmd.LoseGold(state.Cost, Owner!, GoldLossType.Spent),
            () => RouteEventMapBoundary.PersistEventAsync(run, shared));
        switch (outcome)
        {
            case EventGoldCostOutcome.OwnerUnavailable:
                Block("The ButlerAddress reprint has no owner to charge.");
                return false;
            case EventGoldCostOutcome.PersistenceUnknown:
                Block("The ButlerAddress reprint cost persistence is unknown.");
                return false;
            default:
                return true;
        }
    }

    private void ShowConfirmedState(ButlerAddressState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(ButlerAddressState state)
    {
        string instanceId = state.InstanceId;
        ButlerAddressStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        // Reprinting the table is only offered when the player can actually pay
        // for it; the ledger is never changed by taking gold the run does not have.
        bool canReprint = stage != ButlerAddressStage.Ledger || Owner is null || Owner.Gold >= ButlerAddressState.ReprintGoldCost;
        return ButlerAddressFlow.Options(stage)
            .Where(action => action != ButlerAddressAction.AmendRegistry || canReprint)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{ButlerAddressFlow.StageKey(stage)}.options.{ButlerAddressFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private ButlerAddressState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.ButlerAddress is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:{state.ActIndex}"
            && ButlerAddressFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(ButlerAddressState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{ButlerAddressFlow.DescriptionKey(state)}.description");
}
