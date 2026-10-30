using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class KuhnParadigm : EventModel
{
    private const string LocalizationRoot = "KUHN_PARADIGM";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.MEASUREMENT.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.MEASUREMENT.options.CHECK_INSTRUMENTS"),
            L10NLookup($"{LocalizationRoot}.pages.MEASUREMENT.options.STUDY_NEW_CLASSIFICATION"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("KuhnParadigm cannot restore its saved event state.");
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
                Block("KuhnParadigm run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("KuhnParadigm saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("KuhnParadigm entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != KuhnParadigmAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved KuhnParadigm choice remains blocked for recovery.");
                return;
            }
            if (!await ApplyTrainingCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"KuhnParadigm entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(KuhnParadigmState expectedState, string expectedInstanceId,
        KuhnParadigmStage expectedStage, int expectedActionCount, KuhnParadigmAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("KuhnParadigm run state is unavailable.");
                return;
            }
            if (action == KuhnParadigmAction.TrainTeam && !CanAffordTraining()) return;
            if (shared.KuhnParadigm is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != KuhnParadigmAction.None || state.Closed
                || !KuhnParadigmFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"KuhnParadigm prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"KuhnParadigm choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"KuhnParadigm committed choice persistence is unknown: {action}.");
                return;
            }
            if (!await ApplyTrainingCostAsync(run, shared, state)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"KuhnParadigm choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, KuhnParadigmState state) =>
        ReferenceEquals(shared.KuhnParadigm, state) && shared.WesternJourney is { } journey
        && KuhnParadigmFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private bool CanAffordTraining() =>
        Owner is { } owner && owner.Gold >= KuhnParadigmState.TrainTeamGoldCost;

    private async Task<bool> ApplyTrainingCostAsync(RunState run, PhilosophyRunState shared,
        KuhnParadigmState state)
    {
        if (state.Standard != KuhnParadigmStandard.Trained) return true;
        EventGoldCostOutcome outcome = await EventGoldCostProtocol.ApplyAsync(
            state,
            () => Owner is null ? null : (decimal?)Owner.Gold,
            async () => await PlayerCmd.LoseGold(state.Cost, Owner!, GoldLossType.Spent),
            () => RouteEventMapBoundary.PersistEventAsync(run, shared));
        switch (outcome)
        {
            case EventGoldCostOutcome.OwnerUnavailable:
                Block("The Kuhn training has no owner to charge.");
                return false;
            case EventGoldCostOutcome.PersistenceUnknown:
                Block("The Kuhn training cost persistence is unknown.");
                return false;
            default:
                return true;
        }
    }

    private void ShowConfirmedState(KuhnParadigmState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(KuhnParadigmState state)
    {
        string instanceId = state.InstanceId;
        KuhnParadigmStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return KuhnParadigmFlow.Options(stage)
            .Where(action => action != KuhnParadigmAction.TrainTeam || CanAffordTraining())
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{KuhnParadigmFlow.StageKey(stage)}.options.{KuhnParadigmFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private KuhnParadigmState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.KuhnParadigm is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:3"
            && KuhnParadigmFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(KuhnParadigmState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{KuhnParadigmFlow.DescriptionKey(state)}.description");
}
