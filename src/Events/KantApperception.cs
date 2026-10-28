using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class KantApperception : EventModel
{
    private const string LocalizationRoot = "KANT_APPERCEPTION";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.INSPECT_LEDGER"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_HUME"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("KantApperception cannot restore its saved event state.");
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
                Block("KantApperception run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("KantApperception saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("KantApperception entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != KantApperceptionAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved KantApperception choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"KantApperception entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(KantApperceptionState expectedState, string expectedInstanceId,
        KantApperceptionStage expectedStage, int expectedActionCount, KantApperceptionAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("KantApperception run state is unavailable.");
                return;
            }
            if (shared.KantApperception is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != KantApperceptionAction.None || state.Closed
                || !KantApperceptionFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"KantApperception prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"KantApperception choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"KantApperception committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"KantApperception choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, KantApperceptionState state) =>
        ReferenceEquals(shared.KantApperception, state) && shared.WesternJourney is { } journey
        && KantApperceptionFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(KantApperceptionState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(KantApperceptionState state)
    {
        string instanceId = state.InstanceId;
        KantApperceptionStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return KantApperceptionFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{KantApperceptionFlow.StageKey(stage)}.options.{KantApperceptionFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private KantApperceptionState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.KantApperception is { } state
            && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:3"
            && KantApperceptionFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(KantApperceptionState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{KantApperceptionFlow.DescriptionKey(state)}.description");
}
