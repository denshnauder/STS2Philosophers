using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using System.Runtime.CompilerServices;

namespace STS2Philosophers;

public sealed class MachiavelliEmergencyMandate : EventModel
{
    private const string LocalizationRoot = "MACHIAVELLI_EMERGENCY_MANDATE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(MachiavelliEmergencyMandateStage.Invitation);

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.ACCEPT_INVITATION"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.DECLINE_INVITATION"),
        };

    // Empty native options finish the event. Locked options keep it open while
    // the native awaited AfterEventStarted hook confirms the saved transaction.
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        MachiavelliEmergencyMandateState state = RestoreStateOrThrow();
        if (isPreFinished && !state.Closed)
        {
            throw new InvalidDataException("An open Machiavelli arrangement cannot be pre-finished.");
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
            PhilosophyRunState shared = PhilosophyRunStateService.GetOrCreate(run);
            MachiavelliEmergencyMandateState state = RestoreStateOrThrow();
            if (!IsJourneyConsistent(shared))
            {
                Block("The saved Machiavelli arrangement conflicts with its route.");
                return;
            }

            if (!await PersistBoundaryAsync(run, shared))
            {
                Block("Machiavelli entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != MachiavelliEmergencyMandateAction.None)
            {
                if (!CommitPending(shared, state) || !await PersistBoundaryAsync(run, shared))
                {
                    Block("The saved Machiavelli action remains blocked for recovery.");
                    return;
                }
            }
            if (!IsActiveRoom(run)) return;
            ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"Machiavelli entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(
        MachiavelliEmergencyMandateState expectedState,
        string expectedInstanceId,
        MachiavelliEmergencyMandateStage expectedStage,
        int expectedActionCount,
        MachiavelliEmergencyMandateAction action)
    {
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            PhilosophyRunState shared = PhilosophyRunStateService.GetOrCreate(run);
            if (shared.MachiavelliEmergencyMandate is not { } state
                || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != MachiavelliEmergencyMandateAction.None || state.Closed) return;
            if (!IsJourneyConsistent(shared))
            {
                Block("The Machiavelli choice no longer matches its saved route.");
                return;
            }
            if (!MachiavelliEmergencyMandateFlow.TryPrepare(state, action)) return;

            _ready = false;
            SetEventState(PageDescription(state.Stage), LockedOptions());
            if (!await PersistBoundaryAsync(run, shared))
            {
                Block($"Machiavelli prepared action persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"Machiavelli action could not commit: {action}.");
                return;
            }
            if (!await PersistBoundaryAsync(run, shared))
            {
                Block($"Machiavelli committed action persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"Machiavelli action failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, MachiavelliEmergencyMandateState state) =>
        shared.WesternJourney is { } journey
        && ReferenceEquals(shared.MachiavelliEmergencyMandate, state)
        && MachiavelliEmergencyMandateFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    internal static bool IsJourneyConsistent(PhilosophyRunState shared) =>
        shared.MachiavelliEmergencyMandate is { } state && shared.WesternJourney is { } journey
        && MachiavelliEmergencyMandateFlow.IsJourneyConsistent(state, journey, WesternRouteGraph.LoadEmbedded());

    internal static Task<bool> PersistBoundaryAsync(
        RunState run, PhilosophyRunState shared, EventRoom? expectedRoom = null) =>
        RouteEventMapBoundary.PersistEventAsync(run, shared, expectedRoom);

    private bool IsActiveRoom(RunState run) =>
        RunManager.Instance.IsInProgress && !run.IsGameOver && run.Players.Count == 1
        && run.CurrentActIndex == 1
        && ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run)
        && run.CurrentRoom is EventRoom room && ReferenceEquals(room.LocalMutableEvent, this)
        && (run.CurrentRoomCount == 2 && run.BaseRoom is not null
            || run.CurrentRoomCount == 1 && ReferenceEquals(run.BaseRoom, room));

    private void ShowConfirmedState(MachiavelliEmergencyMandateState state)
    {
        if (state.Closed)
        {
            _ready = false;
            SetEventFinished(PageDescription(state.Stage));
            return;
        }
        _ready = true;
        SetEventState(PageDescription(state.Stage), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, $"{LocalizationRoot}.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(MachiavelliEmergencyMandateState state)
    {
        string instanceId = state.InstanceId;
        MachiavelliEmergencyMandateStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return MachiavelliEmergencyMandateFlow.Options(stage)
            .Select(action => new EventOption(
                this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{MachiavelliEmergencyMandateFlow.StageKey(stage)}.options.{MachiavelliEmergencyMandateFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private MachiavelliEmergencyMandateState RestoreStateOrThrow()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.GetOrCreate(run).MachiavelliEmergencyMandate is { } state
            && state.IsUsable() && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2") return state;
        throw new InvalidDataException("MachiavelliEmergencyMandate requires its valid saved instance.");
    }

    private void Block(string message)
    {
        _ready = false;
        _blocked = true;
        if (!IsFinished && Owner?.RunState is RunState run && IsActiveRoom(run))
        {
            SetEventState(Description ?? InitialDescription, LockedOptions());
        }
        Log.Error($"[STS2Philosophers] {message} Choices remain frozen until reload.");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(MachiavelliEmergencyMandateStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{MachiavelliEmergencyMandateFlow.StageKey(stage)}.description");

}
