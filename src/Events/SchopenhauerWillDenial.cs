using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class SchopenhauerWillDenial : EventModel
{
    private const string LocalizationRoot = "SCHOPENHAUER_WILL_DENIAL";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.BUY_EXTRA"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ASK_PORTER"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("SchopenhauerWillDenial cannot restore its saved event state.");
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
                Block("SchopenhauerWillDenial run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("SchopenhauerWillDenial saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("SchopenhauerWillDenial entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != SchopenhauerWillDenialAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved SchopenhauerWillDenial choice remains blocked for recovery.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"SchopenhauerWillDenial entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(SchopenhauerWillDenialState expectedState, string expectedInstanceId,
        SchopenhauerWillDenialStage expectedStage, int expectedActionCount,
        SchopenhauerWillDenialAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("SchopenhauerWillDenial run state is unavailable.");
                return;
            }
            if (shared.SchopenhauerWillDenial is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != SchopenhauerWillDenialAction.None || state.Closed
                || !SchopenhauerWillDenialFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"SchopenhauerWillDenial prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"SchopenhauerWillDenial choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"SchopenhauerWillDenial committed choice persistence is unknown: {action}.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"SchopenhauerWillDenial choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task<bool> ApplyCostAsync(RunState run, PhilosophyRunState shared,
        SchopenhauerWillDenialState state)
    {
        if (state.Outcome == SchopenhauerWillDenialOutcome.None) return true;
        EventGoldCostOutcome outcome = await EventGoldCostProtocol.ApplyAsync(
            state,
            () => Owner is null ? null : (decimal?)Owner.Gold,
            async () => await PlayerCmd.LoseGold(state.Cost, Owner!, GoldLossType.Spent),
            () => RouteEventMapBoundary.PersistEventAsync(run, shared));
        switch (outcome)
        {
            case EventGoldCostOutcome.OwnerUnavailable:
                Block("SchopenhauerWillDenial has no owner to charge.");
                return false;
            case EventGoldCostOutcome.PersistenceUnknown:
                Block("SchopenhauerWillDenial cost persistence is unknown.");
                return false;
            default:
                return true;
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, SchopenhauerWillDenialState state) =>
        ReferenceEquals(shared.SchopenhauerWillDenial, state)
        && shared.WesternJourney is { } journey && shared.KantApperception is { } kant
        && SchopenhauerWillDenialFlow.TryCommitPending(state, journey, kant, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(SchopenhauerWillDenialState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(SchopenhauerWillDenialState state)
    {
        string instanceId = state.InstanceId;
        SchopenhauerWillDenialStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return SchopenhauerWillDenialFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{SchopenhauerWillDenialFlow.StageKey(stage)}.options.{SchopenhauerWillDenialFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private SchopenhauerWillDenialState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.SchopenhauerWillDenial is { } state
            && shared.WesternJourney is { } journey && shared.KantApperception is { } kant
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:3"
            && SchopenhauerWillDenialFlow.IsRouteConsistent(state, journey, kant)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(SchopenhauerWillDenialState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{SchopenhauerWillDenialFlow.DescriptionKey(state)}.description");
}
