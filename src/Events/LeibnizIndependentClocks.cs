using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class LeibnizIndependentClocks : EventModel
{
    private const string LocalizationRoot = LeibnizHarmonyState.LocalizationRoot;
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
    [
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT"),
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE"),
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("LeibnizIndependentClocks cannot restore its saved event state.");
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
                Block("LeibnizIndependentClocks run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("LeibnizIndependentClocks saved state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("LeibnizIndependentClocks entry persistence is unknown.");
                return;
            }
            if (!HasValidCarrier(run))
            {
                Block("LeibnizIndependentClocks requires an owned DescartesThinkingLens carrier.");
                return;
            }
            if (state.PendingAction != LeibnizHarmonyAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved LeibnizIndependentClocks choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"LeibnizIndependentClocks entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(LeibnizHarmonyState expectedState, string expectedInstanceId,
        LeibnizHarmonyStage expectedStage, int expectedActionCount, LeibnizHarmonyAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("LeibnizIndependentClocks run state is unavailable.");
                return;
            }
            if (!HasValidCarrier(run))
            {
                Block("LeibnizIndependentClocks requires an owned DescartesThinkingLens carrier.");
                return;
            }
            if (shared.LeibnizHarmony is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != LeibnizHarmonyAction.None || state.Closed
                || !LeibnizHarmonyFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"LeibnizIndependentClocks prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"LeibnizIndependentClocks choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"LeibnizIndependentClocks committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"LeibnizIndependentClocks choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, LeibnizHarmonyState state) =>
        ReferenceEquals(shared.LeibnizHarmony, state)
        && shared.WesternJourney is { } journey
        && shared.SpinozaCauseNetwork is { } spinoza
        && LeibnizHarmonyFlow.TryCommitPending(state, journey, spinoza,
            WesternRouteGraph.LoadEmbedded());

    private bool HasValidCarrier(RunState run) =>
        run.Players.Count == 1
        && ReferenceEquals(run.Players[0], Owner)
        && Owner is { } owner
        && owner.Relics.OfType<DescartesThinkingLens>().FirstOrDefault() is { } lens
        && ReferenceEquals(lens.Owner, owner)
        && lens.CanOfferNativeQuestion
        && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
        && shared is not null
        && shared.CurrentDoctrine is { ThinkerId: "DESCARTES", DoctrineId: "DESCARTES_THINKING_LENS" };

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(LeibnizHarmonyState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(LeibnizHarmonyState state)
    {
        string instanceId = state.InstanceId;
        LeibnizHarmonyStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return LeibnizHarmonyFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{LeibnizHarmonyFlow.StageKey(stage)}.options.{LeibnizHarmonyFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private LeibnizHarmonyState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.LeibnizHarmony is { } state
            && shared.WesternJourney is { } journey
            && shared.SpinozaCauseNetwork is { } spinoza
            && state.InstanceId == LeibnizHarmonyState.BuildInstanceId(run.Rng.Seed)
            && state.SpinozaInstanceId == $"SPINOZA_CAUSE_NETWORK:{run.Rng.Seed}:2"
            && HasValidCarrier(run)
            && LeibnizHarmonyFlow.IsRouteConsistent(state, journey, spinoza)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(LeibnizHarmonyState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{LeibnizHarmonyFlow.DescriptionKey(state)}.description");
}
