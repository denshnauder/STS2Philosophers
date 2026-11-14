using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class MillHarm : EventModel
{
    private const string LocalizationRoot = "MILL_HARM";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.APPLY_HARM_TEST"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.{KeepKey(TryRestoreState())}"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("MillHarm cannot restore its saved event state.");
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
                Block("MillHarm run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("MillHarm saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("MillHarm entry persistence is unknown."); return;
            }
            if (state.PendingAction != MillHarmAction.None
                && (!CommitPending(shared, state) || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved MillHarm choice remains blocked for recovery."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"MillHarm entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(MillHarmState expectedState, string expectedInstanceId,
        MillHarmStage expectedStage, int expectedActionCount, MillHarmAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("MillHarm run state is unavailable."); return;
            }
            if (shared.MillHarm is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != MillHarmAction.None || state.Closed
                || !MillHarmFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"MillHarm prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"MillHarm choice could not commit: {action}."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"MillHarm committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"MillHarm choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, MillHarmState state) =>
        ReferenceEquals(shared.MillHarm, state) && shared.WesternJourney is { } journey
        && MillHarmFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run)
    {
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.MillHarm is not { } state) return false;
        return RouteEventMapBoundary.IsActiveEvent(run, this, state.ActIndex - 1);
    }

    private void ShowConfirmedState(MillHarmState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(MillHarmState state)
    {
        string instanceId = state.InstanceId;
        MillHarmStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return MillHarmFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{MillHarmFlow.StageKey(stage)}.options.{OptionKey(state, action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private MillHarmState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.MillHarm is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:{state.ActIndex}"
            && MillHarmFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(MillHarmState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{PageKey(state)}.description");

    private static bool IsBenthamFixed(MillHarmState? state) =>
        state is { FixedMode: true, RoomSourceNodeId: MillHarmState.BenthamSourceNodeId };

    private static bool IsSmithFixed(MillHarmState? state) =>
        state is { FixedMode: true, RoomSourceNodeId: MillHarmState.SmithSourceNodeId };

    private static bool IsMillFixed(MillHarmState? state) =>
        state is { FixedMode: true, RoomSourceNodeId: "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE" };

    private static string KeepKey(MillHarmState? state) =>
        state is { FixedMode: true, ActIndex: 2 }
            ? IsBenthamFixed(state) ? "KEEP_BENTHAM_EARLY" : "KEEP_SMITH_EARLY"
        : IsBenthamFixed(state) ? "KEEP_BENTHAM"
        : IsSmithFixed(state) ? "KEEP_SMITH"
        : IsMillFixed(state) ? "KEEP_MILL"
        : "KEEP_WOLLSTONECRAFT";

    private static string OptionKey(MillHarmState state, MillHarmAction action) =>
        action == MillHarmAction.KeepWollstonecraft
            ? KeepKey(state)
            : MillHarmFlow.ActionKey(action);

    private static string PageKey(MillHarmState state)
    {
        if (!state.FixedMode) return MillHarmFlow.DescriptionKey(state);
        if (state.ActIndex == 2) return state.Stage switch
        {
            MillHarmStage.Invitation => IsBenthamFixed(state) ? "EARLY_BENTHAM" : "EARLY_SMITH",
            MillHarmStage.Test => "EARLY_TEST",
            MillHarmStage.ResultConduct => "EARLY_RESULT_CONDUCT",
            MillHarmStage.ResultOffense => "EARLY_RESULT_OFFENSE",
            MillHarmStage.ResultKept => "EARLY_RESULT_KEPT",
            _ => "EARLY_SMITH",
        };
        return state.Stage switch
        {
            MillHarmStage.Invitation => IsMillFixed(state) ? "FIXED_MILL"
                : IsBenthamFixed(state) ? "FIXED_BENTHAM" : "FIXED_SMITH",
            MillHarmStage.Test => "FIXED_TEST",
            MillHarmStage.ResultConduct => "FIXED_RESULT_CONDUCT",
            MillHarmStage.ResultOffense => "FIXED_RESULT_OFFENSE",
            MillHarmStage.ResultKept => "FIXED_RESULT_KEPT",
            _ => "FIXED_SMITH",
        };
    }
}
