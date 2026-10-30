using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2Philosophers;

public sealed class BaconSampleLedger : EventModel
{
    private const string LocalizationRoot = "BACON_SAMPLE_LEDGER";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

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
            Block("BaconSampleLedger cannot restore its saved event state.");
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
                Block("BaconSampleLedger run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("BaconSampleLedger saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("BaconSampleLedger entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != BaconSampleLedgerAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved BaconSampleLedger choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"BaconSampleLedger entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(BaconSampleLedgerState expectedState, string expectedInstanceId,
        BaconSampleLedgerStage expectedStage, int expectedActionCount, BaconSampleLedgerAction action)
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
                Block("BaconSampleLedger run state is unavailable.");
                return;
            }
            if (shared.BaconSampleLedger is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != BaconSampleLedgerAction.None || state.Closed
                || !BaconSampleLedgerFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"BaconSampleLedger prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"BaconSampleLedger choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"BaconSampleLedger committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"BaconSampleLedger choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, BaconSampleLedgerState state) =>
        ReferenceEquals(shared.BaconSampleLedger, state) && shared.WesternJourney is { } journey
        && shared.OckhamNamesInquiry is { } ockham && shared.GorgiasSpeechInquiry is { } gorgias
        && BaconSampleLedgerFlow.TryCommitPending(state, journey, ockham, gorgias, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(BaconSampleLedgerState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(BaconSampleLedgerState state)
    {
        string instanceId = state.InstanceId;
        if (state.Inspecting && state.Diderot is { } segment)
        {
            DiderotPracticeStage segmentStage = segment.Stage;
            int segmentActions = segment.ConfirmedActions.Count;
            return DiderotPracticeFlow.Options(segmentStage)
                .Select(action => new EventOption(this,
                    () => ChooseDiderot(state, instanceId, segmentStage, segmentActions, action),
                    $"DIDEROT_PRACTICE.pages.{DiderotPracticeFlow.StageKey(segmentStage)}.options.{DiderotPracticeFlow.ActionKey(action)}",
                    Array.Empty<IHoverTip>()))
                .ToArray();
        }
        BaconSampleLedgerStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        var options = BaconSampleLedgerFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{BaconSampleLedgerFlow.StageKey(stage)}.options.{BaconSampleLedgerFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToList();
        if (state.Diderot is { Closed: false } practice && practice.IsUsable()
            && stage is BaconSampleLedgerStage.ResultLimited or BaconSampleLedgerStage.ResultControl
                or BaconSampleLedgerStage.ResultKept or BaconSampleLedgerStage.ResultDeclined)
        {
            options.Insert(0, new EventOption(this,
                () => AskDiderot(state, instanceId),
                $"{LocalizationRoot}.pages.{BaconSampleLedgerFlow.StageKey(stage)}.options.ASK_DIDEROT",
                Array.Empty<IHoverTip>()));
        }
        return options;
    }

    private BaconSampleLedgerState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.BaconSampleLedger is { } state
            && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:3"
            && shared.OckhamNamesInquiry is { } ockham && shared.GorgiasSpeechInquiry is { } gorgias
            && BaconSampleLedgerFlow.IsRouteConsistent(state, journey, ockham, gorgias)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(BaconSampleLedgerState state) =>
        state.Inspecting
            ? L10NLookup($"DIDEROT_PRACTICE.pages.{DiderotPracticeFlow.DescriptionKey(state.Diderot!)}.description")
            : L10NLookup($"{LocalizationRoot}.pages.{BaconSampleLedgerFlow.DescriptionKey(state)}.description");

    private async Task ChooseDiderot(BaconSampleLedgerState expectedState, string expectedInstanceId,
        DiderotPracticeStage expectedStage, int expectedActionCount, DiderotPracticeAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Diderot is not { } segment
            || segment.Stage != expectedStage || segment.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("BaconSampleLedger run state is unavailable."); return;
            }
            if (shared.BaconSampleLedger is not { } state || !ReferenceEquals(state, expectedState)
                || state.Diderot is not { } current || !ReferenceEquals(current, segment)
                || segment.InstanceId != $"{expectedInstanceId}:DIDEROT"
                || segment.Stage != expectedStage || segment.ConfirmedActions.Count != expectedActionCount
                || segment.PendingAction != DiderotPracticeAction.None || segment.Closed
                || !DiderotPracticeFlow.TryPrepare(segment, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"Diderot prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run)) return;
            DiderotPracticeFlow.Apply(segment, action);
            if (segment.Closed) state.Inspecting = false;
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"Diderot committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"Diderot choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task AskDiderot(BaconSampleLedgerState expectedState, string expectedInstanceId)
    {
        if (expectedState.InstanceId != expectedInstanceId || !_ready || _blocked
            || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("BaconSampleLedger run state is unavailable."); return;
            }
            if (shared.BaconSampleLedger is not { } state || !ReferenceEquals(state, expectedState)
                || state.Diderot is not { Closed: false } || !state.Diderot.IsUsable()) return;
            state.Inspecting = true;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("Diderot entry persistence is unknown."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"Diderot entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }
}
