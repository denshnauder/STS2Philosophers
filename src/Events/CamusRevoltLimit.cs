using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class CamusRevoltLimit : EventModel
{
    private const string LocalizationRoot = "CAMUS_REVOLT_LIMIT";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.SITUATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.SITUATION.options.ACKNOWLEDGE_LEVY"),
            L10NLookup($"{LocalizationRoot}.pages.SITUATION.options.QUESTION_LEVY"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("CamusRevoltLimit cannot restore its saved event state.");
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
                Block("CamusRevoltLimit run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("CamusRevoltLimit saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("CamusRevoltLimit entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != CamusRevoltLimitAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved CamusRevoltLimit choice remains blocked for recovery.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"CamusRevoltLimit entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(CamusRevoltLimitState expectedState, string expectedInstanceId,
        CamusRevoltLimitStage expectedStage, int expectedActionCount, CamusRevoltLimitAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("CamusRevoltLimit run state is unavailable.");
                return;
            }
            if (shared.CamusRevoltLimit is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != CamusRevoltLimitAction.None || state.Closed
                || !CamusRevoltLimitFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"CamusRevoltLimit prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"CamusRevoltLimit choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"CamusRevoltLimit committed choice persistence is unknown: {action}.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"CamusRevoltLimit choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task<bool> ApplyCostAsync(RunState run, PhilosophyRunState shared,
        CamusRevoltLimitState state)
    {
        if (state.Outcome == CamusRevoltLimitOutcome.None || state.CostPaid) return true;
        if (state.CostAttempted)
        {
            Block("CamusRevoltLimit cost was attempted but its outcome is unknown.");
            return false;
        }
        state.CostAttempted = true;
        if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
        {
            Block("CamusRevoltLimit cost attempt persistence is unknown.");
            return false;
        }
        if (state.Cost > 0)
        {
            if (Owner is null)
            {
                Block("CamusRevoltLimit has no owner to charge.");
                return false;
            }
            await PlayerCmd.LoseGold(state.Cost, Owner, GoldLossType.Spent);
        }
        state.CostPaid = true;
        if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
        {
            Block("CamusRevoltLimit paid cost persistence is unknown.");
            return false;
        }
        return true;
    }

    private static bool CommitPending(PhilosophyRunState shared, CamusRevoltLimitState state) =>
        ReferenceEquals(shared.CamusRevoltLimit, state)
        && shared.WesternJourney is { } journey
        && CamusRevoltLimitFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(CamusRevoltLimitState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(CamusRevoltLimitState state)
    {
        string instanceId = state.InstanceId;
        CamusRevoltLimitStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return CamusRevoltLimitFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{CamusRevoltLimitFlow.StageKey(stage)}.options.{CamusRevoltLimitFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private CamusRevoltLimitState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.CamusRevoltLimit is { } state
            && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:3"
            && CamusRevoltLimitFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(CamusRevoltLimitState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{CamusRevoltLimitFlow.DescriptionKey(state)}.description");
}
