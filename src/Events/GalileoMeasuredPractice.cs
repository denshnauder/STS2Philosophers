using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2Philosophers;

public sealed class GalileoMeasuredPractice : EventModel
{
    private const string LocalizationRoot = "GALILEO_MEASURED_PRACTICE";
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
            Block("GalileoMeasuredPractice cannot restore its saved event state.");
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
            if (!HasCarrier())
            {
                Block("The actual owned Galileo lyre is unavailable.");
                return;
            }
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("GalileoMeasuredPractice run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("GalileoMeasuredPractice saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("GalileoMeasuredPractice entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != GalileoMeasuredPracticeAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved GalileoMeasuredPractice choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"GalileoMeasuredPractice entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(GalileoMeasuredPracticeState expectedState, string expectedInstanceId,
        GalileoMeasuredPracticeStage expectedStage, int expectedActionCount, GalileoMeasuredPracticeAction action)
    {
        // Reject an obsolete button before acquiring a gate or touching native run state.
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!HasCarrier())
            {
                Block("The actual owned Galileo lyre is unavailable.");
                return;
            }
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("GalileoMeasuredPractice run state is unavailable.");
                return;
            }
            if (shared.GalileoMeasuredPractice is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != GalileoMeasuredPracticeAction.None || state.Closed
                || !GalileoMeasuredPracticeFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"GalileoMeasuredPractice prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"GalileoMeasuredPractice choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"GalileoMeasuredPractice committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"GalileoMeasuredPractice choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, GalileoMeasuredPracticeState state) =>
        ReferenceEquals(shared.GalileoMeasuredPractice, state) && shared.WesternJourney is { } journey
        && shared.DemocritusArrangement is { } democritus && shared.EmpedoclesRootsInquiry is { } empedocles
        && GalileoMeasuredPracticeFlow.TryCommitPending(state, journey, democritus, empedocles);

    private bool HasCarrier() => Owner?.GetRelicById(ModelDb.GetId<HeraclitusLyre>())
        is HeraclitusLyre { CanOfferGalileo: true };

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 2);

    private void ShowConfirmedState(GalileoMeasuredPracticeState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(GalileoMeasuredPracticeState state)
    {
        string instanceId = state.InstanceId;
        GalileoMeasuredPracticeStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return GalileoMeasuredPracticeFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{GalileoMeasuredPracticeFlow.StageKey(stage)}.options.{GalileoMeasuredPracticeFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private GalileoMeasuredPracticeState? TryRestoreState()
    {
        if (HasCarrier() && Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.GalileoMeasuredPractice is { } state
            && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:3"
            && shared.DemocritusArrangement is { } democritus && shared.EmpedoclesRootsInquiry is { } empedocles
            && GalileoMeasuredPracticeFlow.IsRouteConsistent(state, journey, democritus, empedocles)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(GalileoMeasuredPracticeState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{GalileoMeasuredPracticeFlow.DescriptionKey(state)}.description");
}
