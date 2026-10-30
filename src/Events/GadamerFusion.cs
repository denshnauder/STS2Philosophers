using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class GadamerFusion : EventModel
{
    private const string LocalizationRoot = "GADAMER_FUSION";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.HORIZON_FUSION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.EFFECTIVE_HISTORY"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_HEIDEGGER"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("GadamerFusion cannot restore its saved event state.");
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
                Block("GadamerFusion run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("GadamerFusion saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("GadamerFusion entry persistence is unknown."); return;
            }
            if (state.PendingAction != GadamerFusionAction.None
                && (!CommitPending(shared, state) || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved GadamerFusion choice remains blocked for recovery."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"GadamerFusion entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(GadamerFusionState expectedState, string expectedInstanceId,
        GadamerFusionStage expectedStage, int expectedActionCount, GadamerFusionAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("GadamerFusion run state is unavailable."); return;
            }
            if (shared.GadamerFusion is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != GadamerFusionAction.None || state.Closed
                || !GadamerFusionFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"GadamerFusion prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"GadamerFusion choice could not commit: {action}."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"GadamerFusion committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"GadamerFusion choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, GadamerFusionState state) =>
        ReferenceEquals(shared.GadamerFusion, state) && shared.WesternJourney is { } journey
        && GadamerFusionFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private void ShowConfirmedState(GadamerFusionState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(GadamerFusionState state)
    {
        string instanceId = state.InstanceId;
        GadamerFusionStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return GadamerFusionFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{GadamerFusionFlow.StageKey(stage)}.options.{GadamerFusionFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private GadamerFusionState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.GadamerFusion is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:3"
            && GadamerFusionFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(GadamerFusionState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{GadamerFusionFlow.DescriptionKey(state)}.description");
}
