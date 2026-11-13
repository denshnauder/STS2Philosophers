using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// Voltaire's act-two question node on the checks line. The same tolerance case
// that Montesquieu's room may offer as an in-batch segment also stands on its own
// at a real map question: it owns no edge, never moves the fixed position, and a
// run that never draws it loses nothing. An old save keeps its nested segment.
public sealed class VoltairePublicCritique : EventModel
{
    private const string LocalizationRoot = "VOLTAIRE_PUBLIC_CRITIQUE";
    private const string PageRoot = "VOLTAIRE_TOLERANCE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{PageRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{PageRoot}.pages.INVITATION.options.PROTECT_SPEECH"),
            L10NLookup($"{PageRoot}.pages.INVITATION.options.RESTRICT_SPEECH"),
            L10NLookup($"{PageRoot}.pages.INVITATION.options.KEEP_BALANCE"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("VoltairePublicCritique cannot restore its saved event state.");
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
                Block("VoltairePublicCritique run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("VoltairePublicCritique saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("VoltairePublicCritique entry persistence is unknown."); return;
            }
            // A choice written before the crash is completed here, never dropped:
            // the page it belongs to still accepts it.
            if (state.PendingAction != VoltaireToleranceAction.None)
            {
                if (!VoltaireToleranceFlow.Options(state.Stage).Contains(state.PendingAction))
                {
                    Block("The saved VoltairePublicCritique choice no longer fits its page."); return;
                }
                VoltaireToleranceFlow.Apply(state, state.PendingAction);
                state.PendingAction = VoltaireToleranceAction.None;
                if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
                {
                    Block("VoltairePublicCritique recovered choice persistence is unknown."); return;
                }
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"VoltairePublicCritique entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(VoltaireToleranceState expectedState, string expectedInstanceId,
        VoltaireToleranceStage expectedStage, int expectedActionCount, VoltaireToleranceAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("VoltairePublicCritique run state is unavailable."); return;
            }
            if (shared.VoltairePublicCritique is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != VoltaireToleranceAction.None || state.Closed
                || !VoltaireToleranceFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"VoltairePublicCritique prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !ReferenceEquals(shared.VoltairePublicCritique, state))
            {
                Block($"VoltairePublicCritique choice could not commit: {action}."); return;
            }
            VoltaireToleranceFlow.Apply(state, action);
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"VoltairePublicCritique committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"VoltairePublicCritique choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static int ActiveActIndex() => 1;

    private bool IsActiveRoom(RunState run) =>
        RouteEventMapBoundary.IsActiveEvent(run, this, ActiveActIndex());

    private void ShowConfirmedState(VoltaireToleranceState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(VoltaireToleranceState state)
    {
        string instanceId = state.InstanceId;
        VoltaireToleranceStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return VoltaireToleranceFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{PageRoot}.pages.{VoltaireToleranceFlow.StageKey(stage)}.options.{VoltaireToleranceFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private VoltaireToleranceState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.VoltairePublicCritique is { } state
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2") return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(VoltaireToleranceState state) =>
        L10NLookup($"{PageRoot}.pages.{VoltaireToleranceFlow.StageKey(state.Stage)}.description");
}
