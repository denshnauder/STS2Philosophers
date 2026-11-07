using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

/// <summary>
/// Berkeley's independent second-act question after Locke's fixed room. The
/// earlier in-batch segment remains for old saves and for Locke's own page; this
/// room gives the observation practice a real question-node entry of its own.
/// </summary>
public sealed class BerkeleyPerceivedMark : EventModel
{
    private const string LocalizationRoot = "BERKELEY_PERCEIVED_MARK";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.CHECK_TOUCH"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.CHECK_SOUND"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.CHECK_RECEIPT"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("BerkeleyPerceivedMark cannot restore its saved event state.");
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
                Block("BerkeleyPerceivedMark run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("BerkeleyPerceivedMark saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("BerkeleyPerceivedMark entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != BerkeleyMarkAction.None
                && (!BerkeleyPerceivedMarkFlow.TryCommitPending(state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved BerkeleyPerceivedMark choice remains blocked for recovery.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"BerkeleyPerceivedMark entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(BerkeleyPerceivedMarkState expectedState, string expectedInstanceId,
        BerkeleyMarkStage expectedStage, int expectedActionCount, BerkeleyMarkAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("BerkeleyPerceivedMark run state is unavailable.");
                return;
            }
            if (shared.BerkeleyPerceivedMark is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != BerkeleyMarkAction.None || state.Closed
                || !BerkeleyPerceivedMarkFlow.TryPrepare(state, action)) return;
            _ready = false;
            if (action == BerkeleyMarkAction.RequestSecondObserver && !CanAffordSecondObserver())
            {
                Block("The second observer costs gold the player does not have.");
                return;
            }
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"BerkeleyPerceivedMark prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !BerkeleyPerceivedMarkFlow.TryCommitPending(state))
            {
                Block($"BerkeleyPerceivedMark choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"BerkeleyPerceivedMark committed choice persistence is unknown: {action}.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"BerkeleyPerceivedMark choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private bool IsActiveRoom(RunState run)
    {
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.BerkeleyPerceivedMark is not { } state) return false;
        return RouteEventMapBoundary.IsActiveEvent(run, this, state.ActIndex - 1);
    }

    private void ShowConfirmedState(BerkeleyPerceivedMarkState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(BerkeleyPerceivedMarkState state)
    {
        string instanceId = state.InstanceId;
        BerkeleyMarkStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return BerkeleyPerceivedMarkFlow.Options(stage)
            .Where(action => action != BerkeleyMarkAction.RequestSecondObserver
                || CanAffordSecondObserver())
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{BerkeleyPerceivedMarkFlow.DescriptionKey(state)}.options.{BerkeleyActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private bool CanAffordSecondObserver() =>
        Owner is { } owner && owner.Gold >= BerkeleyPerceivedMarkState.SecondObserverGoldCost;

    private async Task<bool> ApplyCostAsync(RunState run, PhilosophyRunState shared,
        BerkeleyPerceivedMarkState state)
    {
        if (state.Conclusion != BerkeleyConclusion.SecondObserver) return true;
        EventGoldCostOutcome outcome = await EventGoldCostProtocol.ApplyAsync(
            state,
            () => Owner is null ? null : (decimal?)Owner.Gold,
            async () => await PlayerCmd.LoseGold(state.Cost, Owner!, GoldLossType.Spent),
            () => RouteEventMapBoundary.PersistEventAsync(run, shared));
        switch (outcome)
        {
            case EventGoldCostOutcome.OwnerUnavailable:
                Block("The second observer has no owner to charge.");
                return false;
            case EventGoldCostOutcome.PersistenceUnknown:
                Block("The second observer cost persistence is unknown.");
                return false;
            default:
                return true;
        }
    }

    private BerkeleyPerceivedMarkState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.BerkeleyPerceivedMark is { } state
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(BerkeleyPerceivedMarkState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{BerkeleyPerceivedMarkFlow.DescriptionKey(state)}.description");

    private static string BerkeleyActionKey(BerkeleyMarkAction action) => string.Concat(
        action.ToString().SelectMany((c, i) => i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
