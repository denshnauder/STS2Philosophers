using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2Philosophers;

public sealed class AristotleCivicInquiry : EventModel
{
    private const string LocalizationRoot = "ARISTOTLE_CIVIC_INQUIRY";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(AristotleCivicInquiryStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    // A disabled option keeps the native event open while its awaited start hook
    // confirms the saved room and any pending choice before exposing actions.
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("AristotleCivicInquiry cannot restore its saved event state.");
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
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("AristotleCivicInquiry run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("AristotleCivicInquiry saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("AristotleCivicInquiry entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != AristotleCivicInquiryAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved AristotleCivicInquiry choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"AristotleCivicInquiry entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(AristotleCivicInquiryState expectedState, string expectedInstanceId,
        AristotleCivicInquiryStage expectedStage, int expectedActionCount, AristotleCivicInquiryAction action)
    {
        // Reject an obsolete button before acquiring a gate or touching native run state.
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("AristotleCivicInquiry run state is unavailable.");
                return;
            }
            if (shared.AristotleCivicInquiry is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != AristotleCivicInquiryAction.None || state.Closed
                || !AristotleCivicInquiryFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state.Stage), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"AristotleCivicInquiry prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"AristotleCivicInquiry choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"AristotleCivicInquiry committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"AristotleCivicInquiry choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, AristotleCivicInquiryState state) =>
        ReferenceEquals(shared.AristotleCivicInquiry, state) && shared.WesternJourney is { } journey
        && AristotleCivicInquiryFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private void ShowConfirmedState(AristotleCivicInquiryState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state.Stage));
        else SetEventState(PageDescription(state.Stage), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(AristotleCivicInquiryState state)
    {
        string instanceId = state.InstanceId;
        AristotleCivicInquiryStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return AristotleCivicInquiryFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{AristotleCivicInquiryFlow.StageKey(stage)}.options.{AristotleCivicInquiryFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private AristotleCivicInquiryState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.AristotleCivicInquiry is { } state && state.IsUsable()) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(AristotleCivicInquiryStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{AristotleCivicInquiryFlow.StageKey(stage)}.description");
}

