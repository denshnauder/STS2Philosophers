using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class AnaximenesBreathInvitation : EventModel
{
    private const string LocalizationRoot = "ANAXIMENES_BREATH_INVITATION";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(AnaximenesBreathInvitationStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        [L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE")];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("AnaximenesBreathInvitation cannot restore its saved event state.");
            return;
        }
        SetEventState(PageDescription(state.Stage), LockedOptions());
    }

    public override Task AfterEventStarted() => ResumeAfterEntryAsync();

    private async Task ResumeAfterEntryAsync()
    {
        if (_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null
                || TryRestoreState() is not { } state)
            {
                Block("AnaximenesBreathInvitation saved state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("AnaximenesBreathInvitation entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != AnaximenesBreathInvitationAction.None
                && (!await CommitPendingAsync(run, shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved AnaximenesBreathInvitation choice remains blocked for recovery.");
                return;
            }
            if (!MatchesGrantedPractice(run, state))
            {
                Block("AnaximenesBreathInvitation result does not match the saved relic.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"AnaximenesBreathInvitation entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(AnaximenesBreathInvitationState expectedState, string expectedInstanceId,
        AnaximenesBreathInvitationStage expectedStage, int expectedActionCount,
        AnaximenesBreathInvitationAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("AnaximenesBreathInvitation run state is unavailable.");
                return;
            }
            if (shared.AnaximenesBreathInvitation is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != AnaximenesBreathInvitationAction.None || state.Closed
                || !AnaximenesBreathInvitationFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state.Stage), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"AnaximenesBreathInvitation prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !await CommitPendingAsync(run, shared, state))
            {
                Block($"AnaximenesBreathInvitation choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"AnaximenesBreathInvitation committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"AnaximenesBreathInvitation choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static async Task<bool> CommitPendingAsync(
        RunState run, PhilosophyRunState shared, AnaximenesBreathInvitationState state)
    {
        if (!ReferenceEquals(shared.AnaximenesBreathInvitation, state)
            || shared.WesternJourney?.CurrentNodeId != AnaximenesBreathInvitationState.RequiredNodeId
            || run.Players.Count != 1) return false;
        var player = run.Players[0];
        if (player.GetRelicById(ModelDb.GetId<HeraclitusLyre>()) is null) return false;
        bool hasPractice = player.GetRelicById(ModelDb.GetId<AnaximenesBreath>()) is not null;
        if (state.PendingAction == AnaximenesBreathInvitationAction.Accept && !hasPractice)
        {
            await RelicCmd.Obtain<AnaximenesBreath>(player);
            hasPractice = player.GetRelicById(ModelDb.GetId<AnaximenesBreath>()) is not null;
        }
        return AnaximenesBreathInvitationFlow.TryCommitPending(state, hasPractice);
    }

    private static bool MatchesGrantedPractice(RunState run, AnaximenesBreathInvitationState state)
    {
        if (run.Players.Count != 1) return false;
        bool hasPractice = run.Players[0].GetRelicById(ModelDb.GetId<AnaximenesBreath>()) is not null;
        return state.Stage switch
        {
            AnaximenesBreathInvitationStage.Invitation => !hasPractice,
            AnaximenesBreathInvitationStage.Accepted => hasPractice,
            AnaximenesBreathInvitationStage.Declined => !hasPractice,
            _ => false,
        };
    }

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(AnaximenesBreathInvitationState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state.Stage));
        else SetEventState(PageDescription(state.Stage), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(AnaximenesBreathInvitationState state)
    {
        string instanceId = state.InstanceId;
        AnaximenesBreathInvitationStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return AnaximenesBreathInvitationFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{AnaximenesBreathInvitationFlow.StageKey(stage)}.options.{AnaximenesBreathInvitationFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private AnaximenesBreathInvitationState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.AnaximenesBreathInvitation is { } state && state.IsUsable()) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(AnaximenesBreathInvitationStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{AnaximenesBreathInvitationFlow.StageKey(stage)}.description");
}
