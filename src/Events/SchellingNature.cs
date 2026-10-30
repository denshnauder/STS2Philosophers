using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class SchellingNature : EventModel
{
    private const string LocalizationRoot = "SCHELLING_NATURE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.NATURE_AS_SPIRIT"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.NATURE_AS_MECHANISM"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_SPINOZA"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("SchellingNature cannot restore its saved event state.");
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
                Block("SchellingNature run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("SchellingNature saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("SchellingNature entry persistence is unknown."); return;
            }
            if (state.PendingAction != SchellingNatureAction.None
                && (!CommitPending(shared, state) || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved SchellingNature choice remains blocked for recovery."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"SchellingNature entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(SchellingNatureState expectedState, string expectedInstanceId,
        SchellingNatureStage expectedStage, int expectedActionCount, SchellingNatureAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("SchellingNature run state is unavailable."); return;
            }
            if (shared.SchellingNature is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != SchellingNatureAction.None || state.Closed
                || !SchellingNatureFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"SchellingNature prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"SchellingNature choice could not commit: {action}."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"SchellingNature committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"SchellingNature choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, SchellingNatureState state) =>
        ReferenceEquals(shared.SchellingNature, state) && shared.WesternJourney is { } journey
        && SchellingNatureFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private void ShowConfirmedState(SchellingNatureState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(SchellingNatureState state)
    {
        string instanceId = state.InstanceId;
        SchellingNatureStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return SchellingNatureFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{SchellingNatureFlow.StageKey(stage)}.options.{SchellingNatureFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private SchellingNatureState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.SchellingNature is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2"
            && SchellingNatureFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(SchellingNatureState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{SchellingNatureFlow.DescriptionKey(state)}.description");

}
