using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class MontesquieuBalance : EventModel
{
    private const string LocalizationRoot = "MONTESQUIEU_BALANCE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.SEPARATE_POWERS"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.BALANCE_FACTIONS"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.PRESERVE_PRIVILEGE"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("MontesquieuBalance cannot restore its saved event state.");
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
                Block("MontesquieuBalance run state is unavailable."); return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("MontesquieuBalance saved event state is unavailable."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("MontesquieuBalance entry persistence is unknown."); return;
            }
            if (state.PendingAction != MontesquieuBalanceAction.None
                && (!CommitPending(shared, state) || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved MontesquieuBalance choice remains blocked for recovery."); return;
            }
            if (state.Voltaire is { PendingAction: not VoltaireToleranceAction.None } segment)
            {
                if (!VoltaireToleranceFlow.Options(segment.Stage).Contains(segment.PendingAction))
                {
                    Block("The saved Voltaire choice is not valid for its page."); return;
                }
                VoltaireToleranceFlow.Apply(segment, segment.PendingAction);
                if (segment.Closed) state.Inspecting = false;
                if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
                {
                    Block("The saved Voltaire choice persistence is unknown."); return;
                }
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"MontesquieuBalance entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task Choose(MontesquieuBalanceState expectedState, string expectedInstanceId,
        MontesquieuBalanceStage expectedStage, int expectedActionCount, MontesquieuBalanceAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("MontesquieuBalance run state is unavailable."); return;
            }
            if (shared.MontesquieuBalance is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != MontesquieuBalanceAction.None || state.Closed
                || !MontesquieuBalanceFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"MontesquieuBalance prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"MontesquieuBalance choice could not commit: {action}."); return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"MontesquieuBalance committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"MontesquieuBalance choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private static bool CommitPending(PhilosophyRunState shared, MontesquieuBalanceState state) =>
        ReferenceEquals(shared.MontesquieuBalance, state) && shared.WesternJourney is { } journey
        && MontesquieuBalanceFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 0);

    private void ShowConfirmedState(MontesquieuBalanceState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(MontesquieuBalanceState state)
    {
        string instanceId = state.InstanceId;
        if (state.Inspecting && state.Voltaire is { } segment)
        {
            VoltaireToleranceStage segmentStage = segment.Stage;
            int segmentActions = segment.ConfirmedActions.Count;
            return VoltaireToleranceFlow.Options(segmentStage)
                .Select(action => new EventOption(this,
                    () => ChooseVoltaire(state, instanceId, segmentStage, segmentActions, action),
                    $"VOLTAIRE_TOLERANCE.pages.{VoltaireToleranceFlow.StageKey(segmentStage)}.options.{VoltaireToleranceFlow.ActionKey(action)}",
                    Array.Empty<IHoverTip>()))
                .ToArray();
        }
        MontesquieuBalanceStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        var options = MontesquieuBalanceFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{MontesquieuBalanceFlow.StageKey(stage)}.options.{MontesquieuBalanceFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToList();
        if (state.Voltaire is { Closed: false } tolerant && tolerant.IsUsable()
            && stage is MontesquieuBalanceStage.ResultSeparation or MontesquieuBalanceStage.ResultBalance
                or MontesquieuBalanceStage.ResultPrivilege)
        {
            options.Insert(0, new EventOption(this,
                () => AskVoltaire(state, instanceId),
                $"{LocalizationRoot}.pages.{MontesquieuBalanceFlow.StageKey(stage)}.options.ASK_VOLTAIRE",
                Array.Empty<IHoverTip>()));
        }
        return options;
    }

    private MontesquieuBalanceState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.MontesquieuBalance is { } state && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:1"
            && MontesquieuBalanceFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(MontesquieuBalanceState state) =>
        state.Inspecting
            ? L10NLookup($"VOLTAIRE_TOLERANCE.pages.{VoltaireToleranceFlow.DescriptionKey(state.Voltaire!)}.description")
            : L10NLookup($"{LocalizationRoot}.pages.{MontesquieuBalanceFlow.DescriptionKey(state)}.description");

    private async Task ChooseVoltaire(MontesquieuBalanceState expectedState, string expectedInstanceId,
        VoltaireToleranceStage expectedStage, int expectedActionCount, VoltaireToleranceAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Voltaire is not { } segment
            || segment.Stage != expectedStage || segment.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("Montesquieu run state is unavailable."); return;
            }
            if (shared.MontesquieuBalance is not { } state || !ReferenceEquals(state, expectedState)
                || state.Voltaire is not { } current || !ReferenceEquals(current, segment)
                || segment.InstanceId != $"{expectedInstanceId}:VOLTAIRE"
                || segment.Stage != expectedStage || segment.ConfirmedActions.Count != expectedActionCount
                || segment.PendingAction != VoltaireToleranceAction.None || segment.Closed
                || !VoltaireToleranceFlow.TryPrepare(segment, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"Voltaire prepared choice persistence is unknown: {action}."); return;
            }
            if (!IsActiveRoom(run)) return;
            VoltaireToleranceFlow.Apply(segment, action);
            if (segment.Closed) state.Inspecting = false;
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"Voltaire committed choice persistence is unknown: {action}."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"Voltaire choice failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }

    private async Task AskVoltaire(MontesquieuBalanceState expectedState, string expectedInstanceId)
    {
        if (expectedState.InstanceId != expectedInstanceId || !_ready || _blocked
            || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("Montesquieu run state is unavailable."); return;
            }
            if (shared.MontesquieuBalance is not { } state || !ReferenceEquals(state, expectedState)
                || state.Voltaire is not { Closed: false } || !state.Voltaire.IsUsable()) return;
            state.Inspecting = true;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("Voltaire entry persistence is unknown."); return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception) { Block($"Voltaire entry failed: {exception}"); }
        finally { Volatile.Write(ref _resolving, 0); }
    }
}
