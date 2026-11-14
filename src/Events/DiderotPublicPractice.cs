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
public sealed class DiderotPublicPractice : EventModel
{
    private const string LocalizationRoot = "DIDEROT_PUBLIC_PRACTICE";
    private const string PageRoot = "DIDEROT_PRACTICE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{PageRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{PageRoot}.pages.INVITATION.options.COLLECT_INTO_SYSTEM"),
            L10NLookup($"{PageRoot}.pages.INVITATION.options.KEEP_SAMPLE"),
            L10NLookup($"{PageRoot}.pages.INVITATION.options.REFUSE_SUMMARY"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("DiderotPublicPractice cannot restore its saved event state.");
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
                Block("DiderotPublicPractice run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("DiderotPublicPractice saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("DiderotPublicPractice entry persistence is unknown."); return;
            }
            // A choice written before the crash is completed here, never dropped:
            // the page it belongs to still accepts it.
            if (state.PendingAction != DiderotPracticeAction.None)
            {
                if (!DiderotPracticeFlow.Options(state.Stage).Contains(state.PendingAction))
                {
                    Block("The saved DiderotPublicPractice choice no longer fits its page."); return;
                }
                DiderotPracticeFlow.Apply(state, state.PendingAction);
                state.PendingAction = DiderotPracticeAction.None;
                if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
                {
                    Block("DiderotPublicPractice recovered choice persistence is unknown."); return;
                }
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"DiderotPublicPractice entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(DiderotPracticeState expectedState, string expectedInstanceId,
        DiderotPracticeStage expectedStage, int expectedActionCount, DiderotPracticeAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("DiderotPublicPractice run state is unavailable."); return;
            }
            if (shared.DiderotPublicPractice is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != DiderotPracticeAction.None || state.Closed
                || !DiderotPracticeFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"DiderotPublicPractice prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !ReferenceEquals(shared.DiderotPublicPractice, state))
            {
                Block($"DiderotPublicPractice choice could not commit: {action}."); return;
            }
            DiderotPracticeFlow.Apply(state, action);
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"DiderotPublicPractice committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"DiderotPublicPractice choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static int ActiveActIndex() => 1;

    private bool IsActiveRoom(RunState run) =>
        RouteEventMapBoundary.IsActiveEvent(run, this, ActiveActIndex());

    private void ShowConfirmedState(DiderotPracticeState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(DiderotPracticeState state)
    {
        string instanceId = state.InstanceId;
        DiderotPracticeStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return DiderotPracticeFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{PageRoot}.pages.{DiderotPracticeFlow.StageKey(stage)}.options.{DiderotPracticeFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private DiderotPracticeState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.DiderotPublicPractice is { } state
            && (state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2"
                // Keep the Voltaire-prefixed instance written by 0.5.32/0.5.33;
                // the room identity is Diderot's even though the old prefix was wrong.
                || state.InstanceId == $"VOLTAIRE_PUBLIC_CRITIQUE:{run.Rng.Seed}:2")) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(DiderotPracticeState state) =>
        L10NLookup($"{PageRoot}.pages.{DiderotPracticeFlow.StageKey(state.Stage)}.description");
}
