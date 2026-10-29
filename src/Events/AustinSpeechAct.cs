using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class AustinSpeechAct : EventModel
{
    private const string LocalizationRoot = "AUSTIN_SPEECH_ACT";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.STATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.STATION.options.CHECK_AUTHORIZATION"),
            L10NLookup($"{LocalizationRoot}.pages.STATION.options.CHECK_PRESENCE"),
            L10NLookup($"{LocalizationRoot}.pages.STATION.options.CHECK_PROCEDURE"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("AustinSpeechAct cannot restore its saved event state.");
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
                Block("AustinSpeechAct run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("AustinSpeechAct saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("AustinSpeechAct entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != AustinSpeechActAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved AustinSpeechAct choice remains blocked for recovery.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"AustinSpeechAct entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(AustinSpeechActState expectedState, string expectedInstanceId,
        AustinSpeechActStage expectedStage, int expectedActionCount, AustinSpeechActAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("AustinSpeechAct run state is unavailable.");
                return;
            }
            if (shared.AustinSpeechAct is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != AustinSpeechActAction.None || state.Closed
                || !AustinSpeechActFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"AustinSpeechAct prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"AustinSpeechAct choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"AustinSpeechAct committed choice persistence is unknown: {action}.");
                return;
            }
            if (!await ApplyCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"AustinSpeechAct choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task<bool> ApplyCostAsync(RunState run, PhilosophyRunState shared,
        AustinSpeechActState state)
    {
        if (state.Outcome == AustinSpeechActOutcome.None) return true;
        EventGoldCostOutcome outcome = await EventGoldCostProtocol.ApplyAsync(
            state,
            () => Owner is null ? null : (decimal?)Owner.Gold,
            async () => await PlayerCmd.LoseGold(state.Cost, Owner!, GoldLossType.Spent),
            () => RouteEventMapBoundary.PersistEventAsync(run, shared));
        switch (outcome)
        {
            case EventGoldCostOutcome.OwnerUnavailable:
                Block("AustinSpeechAct has no owner to charge.");
                return false;
            case EventGoldCostOutcome.PersistenceUnknown:
                Block("AustinSpeechAct cost persistence is unknown.");
                return false;
            default:
                return true;
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, AustinSpeechActState state) =>
        ReferenceEquals(shared.AustinSpeechAct, state)
        && shared.WesternJourney is { } journey
        && AustinSpeechActFlow.TryCommitPending(state, journey);

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private void ShowConfirmedState(AustinSpeechActState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(AustinSpeechActState state)
    {
        string instanceId = state.InstanceId;
        AustinSpeechActStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return AustinSpeechActState.Available(stage, state.CheckCount, state.AuthorizationChecked,
                state.PresenceChecked, state.ProcedureChecked)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{AustinSpeechActFlow.StageKey(stage)}.options.{AustinSpeechActFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private AustinSpeechActState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.AustinSpeechAct is { } state
            && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2"
            && AustinSpeechActFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(AustinSpeechActState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{AustinSpeechActFlow.DescriptionKey(state)}.description");
}
