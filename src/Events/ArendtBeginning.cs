using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// Arendt's act-three fixed room. After the disaster every traveller repairs their
// own vehicle and nobody takes up the question of the next road. Finishing the
// tools alone earns supplies; inviting others can only begin something if they
// answer on their own; signing the standing order keeps the old arrangement and
// its exclusions. A silent invitation is recorded as an attempt, never as a
// victory and never as a reroll.
public sealed class ArendtBeginning : EventModel
{
    private const string LocalizationRoot = "ARENDT_BEGINNING";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[] { L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.OBSERVE_WAYPOINT") };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("ArendtBeginning cannot restore its saved event state.");
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
                Block("ArendtBeginning run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("ArendtBeginning saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("ArendtBeginning entry persistence is unknown."); return;
            }
            if (state.PendingAction != ArendtBeginningAction.None
                && (!CommitPending(shared, state) || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved ArendtBeginning choice remains blocked for recovery."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"ArendtBeginning entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(ArendtBeginningState expectedState, string expectedInstanceId,
        ArendtBeginningStage expectedStage, int expectedActionCount, ArendtBeginningAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("ArendtBeginning run state is unavailable."); return;
            }
            if (shared.ArendtBeginning is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != ArendtBeginningAction.None || state.Closed
                || !ArendtBeginningFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"ArendtBeginning prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"ArendtBeginning choice could not commit: {action}."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"ArendtBeginning committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"ArendtBeginning choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, ArendtBeginningState state) =>
        ReferenceEquals(shared.ArendtBeginning, state) && shared.WesternJourney is { } journey
        && ArendtBeginningFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private static int ActiveActIndex(RunState run) =>
        PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) && shared?.ArendtBeginning is { } state
            ? Math.Clamp(state.ActIndex, 1, 3) - 1 : 2;

    private bool IsActiveRoom(RunState run) =>
        RouteEventMapBoundary.IsActiveEvent(run, this, ActiveActIndex(run));

    private void ShowConfirmedState(ArendtBeginningState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(ArendtBeginningState state)
    {
        string instanceId = state.InstanceId;
        ArendtBeginningStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return ArendtBeginningFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{ArendtBeginningFlow.StageKey(stage)}.options.{ArendtBeginningFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private ArendtBeginningState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.ArendtBeginning is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:{state.ActIndex}"
            && ArendtBeginningFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(ArendtBeginningState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{ArendtBeginningFlow.DescriptionKey(state)}.description");
}
