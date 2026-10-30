using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class PeirceInquiry : EventModel
{
    private const string LocalizationRoot = "PEIRCE_INQUIRY";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.TEST_BY_CONSEQUENCES"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.FALLIBLE_COMMUNITY"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_HUME"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("PeirceInquiry cannot restore its saved event state.");
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
                Block("PeirceInquiry run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("PeirceInquiry saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("PeirceInquiry entry persistence is unknown."); return;
            }
            if (state.PendingAction != PeirceInquiryAction.None
                && (!CommitPending(shared, state) || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved PeirceInquiry choice remains blocked for recovery."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"PeirceInquiry entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(PeirceInquiryState expectedState, string expectedInstanceId,
        PeirceInquiryStage expectedStage, int expectedActionCount, PeirceInquiryAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("PeirceInquiry run state is unavailable."); return;
            }
            if (shared.PeirceInquiry is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != PeirceInquiryAction.None || state.Closed
                || !PeirceInquiryFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"PeirceInquiry prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"PeirceInquiry choice could not commit: {action}."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"PeirceInquiry committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"PeirceInquiry choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, PeirceInquiryState state) =>
        ReferenceEquals(shared.PeirceInquiry, state) && shared.WesternJourney is { } journey
        && PeirceInquiryFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private void ShowConfirmedState(PeirceInquiryState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(PeirceInquiryState state)
    {
        if (state.InspectingJames && state.James is { } james)
            return JamesPragmatismFlow.Options(james.Stage)
                .Select(action => new EventOption(this,
                    () => ChooseJames(state, james.InstanceId, james.Stage, james.ConfirmedActions.Count, action),
                    $"JAMES_PRAGMATISM.pages.{JamesPragmatismFlow.StageKey(james.Stage)}.options.{JamesPragmatismFlow.ActionKey(action)}",
                    Array.Empty<IHoverTip>()))
                .ToArray();
        string instanceId = state.InstanceId;
        PeirceInquiryStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        bool jamesAlreadyMet = Owner?.RunState is RunState ownerRun
            && PhilosophyRunStateService.TryGet(ownerRun, out PhilosophyRunState? ownerShared)
            && ownerShared?.JamesForcedBridge is not null;
        return PeirceInquiryFlow.Options(stage)
            .Where(action => !(action == PeirceInquiryAction.AskJames && jamesAlreadyMet))
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{PeirceInquiryFlow.StageKey(stage)}.options.{PeirceInquiryFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private async Task ChooseJames(PeirceInquiryState expectedState, string expectedInstanceId,
        JamesPragmatismStage expectedStage, int expectedActionCount, JamesPragmatismAction action)
    {
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("PeirceInquiry run state is unavailable."); return;
            }
            if (shared.PeirceInquiry is not { } state || !ReferenceEquals(state, expectedState)
                || !state.InspectingJames || state.James is not { } james
                || james.InstanceId != expectedInstanceId || james.Stage != expectedStage
                || james.ConfirmedActions.Count != expectedActionCount
                || james.PendingAction != JamesPragmatismAction.None || james.Closed
                || !JamesPragmatismFlow.TryPrepare(james, action)) return;
            JamesPragmatismFlow.Apply(james, action);
            if (james.Closed) state.InspectingJames = false;
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"PeirceInquiry James persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"PeirceInquiry James choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private PeirceInquiryState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.PeirceInquiry is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2"
            && PeirceInquiryFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(PeirceInquiryState state) =>
        state.InspectingJames && state.James is { } james
            ? L10NLookup($"JAMES_PRAGMATISM.pages.{JamesPragmatismFlow.StageKey(james.Stage)}.description")
            : L10NLookup($"{LocalizationRoot}.pages.{PeirceInquiryFlow.DescriptionKey(state)}.description");

}
