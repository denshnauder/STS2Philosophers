using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class OckhamNamesInquiry : EventModel
{
    private const string LocalizationRoot = "OCKHAM_NAMES_INQUIRY";
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
            Block("OckhamNamesInquiry cannot restore its saved event state.");
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
                Block("OckhamNamesInquiry run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("OckhamNamesInquiry saved state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("OckhamNamesInquiry entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != OckhamNamesAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved OckhamNamesInquiry choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"OckhamNamesInquiry entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(OckhamNamesInquiryState expectedState, string expectedInstanceId,
        OckhamNamesStage expectedStage, int expectedActionCount, OckhamNamesAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("OckhamNamesInquiry run state is unavailable.");
                return;
            }
            if (shared.OckhamNamesInquiry is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != OckhamNamesAction.None || state.Closed
                || !OckhamNamesInquiryFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"OckhamNamesInquiry prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"OckhamNamesInquiry choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"OckhamNamesInquiry committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"OckhamNamesInquiry choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, OckhamNamesInquiryState state) =>
        ReferenceEquals(shared.OckhamNamesInquiry, state)
        && shared.WesternJourney is { } journey
        && shared.GorgiasSpeechInquiry is { } gorgias
        && OckhamNamesInquiryFlow.TryCommitPending(state, journey, gorgias,
            WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private void ShowConfirmedState(OckhamNamesInquiryState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(OckhamNamesInquiryState state)
    {
        string instanceId = state.InstanceId;
        OckhamNamesStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return OckhamNamesInquiryFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{OckhamNamesInquiryFlow.StageKey(stage)}.options.{OckhamNamesInquiryFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private OckhamNamesInquiryState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.OckhamNamesInquiry is { } state
            && shared.WesternJourney is { } journey
            && shared.GorgiasSpeechInquiry is { } gorgias
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:{OckhamNamesInquiryState.QuestionAct}"
            && OckhamNamesInquiryFlow.IsRouteConsistent(state, journey, gorgias)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(OckhamNamesInquiryState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{OckhamNamesInquiryFlow.DescriptionKey(state)}.description");
}
