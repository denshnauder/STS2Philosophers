using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace STS2Philosophers;

public sealed class CopernicusShiftedCenter : EventModel
{
    private const string LocalizationRoot = "COPERNICUS_SHIFTED_CENTER";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        FormatPage(CopernicusShiftedCenterStage.Invitation, null);

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
            Block("CopernicusShiftedCenter cannot restore its saved event state.");
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
                Block("CopernicusShiftedCenter run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("CopernicusShiftedCenter saved event state is unavailable.");
                return;
            }
            if (!CopernicusNativeTravel.TryBindQuestion(run, state)
                || !CopernicusNativeTravel.MatchesSource(run, state.Fork))
            {
                Block("CopernicusShiftedCenter source fork changed before entry recovery.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("CopernicusShiftedCenter entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != CopernicusShiftedCenterAction.None
                && (!CopernicusNativeTravel.MatchesSource(run, state.Fork) || !CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved CopernicusShiftedCenter choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowOrScheduleTravel(run, state);
        }
        catch (Exception exception)
        {
            Block($"CopernicusShiftedCenter entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(CopernicusShiftedCenterState expectedState, string expectedInstanceId,
        CopernicusShiftedCenterStage expectedStage, int expectedActionCount, CopernicusShiftedCenterAction action)
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
                Block("CopernicusShiftedCenter run state is unavailable.");
                return;
            }
            if (shared.CopernicusShiftedCenter is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != CopernicusShiftedCenterAction.None || state.Closed) return;
            if (!CopernicusNativeTravel.MatchesSource(run, state.Fork))
            {
                Block("CopernicusShiftedCenter source fork changed before a prepared choice.");
                return;
            }
            if (!CopernicusShiftedCenterFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"CopernicusShiftedCenter prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CopernicusNativeTravel.MatchesSource(run, state.Fork) || !CommitPending(shared, state))
            {
                Block($"CopernicusShiftedCenter choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"CopernicusShiftedCenter committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowOrScheduleTravel(run, state);
        }
        catch (Exception exception)
        {
            Block($"CopernicusShiftedCenter choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, CopernicusShiftedCenterState state) =>
        ReferenceEquals(shared.CopernicusShiftedCenter, state) && shared.WesternJourney is { } journey
        && shared.DemocritusArrangement is { } democritus && shared.EmpedoclesRootsInquiry is { } empedocles
        && CopernicusShiftedCenterFlow.TryCommitPending(state, journey, democritus, empedocles, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 1);

    private void ShowOrScheduleTravel(RunState run, CopernicusShiftedCenterState state)
    {
        ShowConfirmedState(state);
        if (state.TravelPending) CopernicusNativeTravel.QueueTravel(run, this, state, Block);
    }

    private void ShowConfirmedState(CopernicusShiftedCenterState state)
    {
        _ready = !state.Closed;
        // A finished native event always exposes an unguarded Proceed button.
        // Keep the travel intention locked until native travel exits this room.
        if (state.TravelPending) SetEventState(PageDescription(state), LockedOptions());
        else if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(CopernicusShiftedCenterState state)
    {
        string instanceId = state.InstanceId;
        CopernicusShiftedCenterStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return CopernicusShiftedCenterFlow.Options(stage).Select(action =>
        {
            EventOption option = new(this, () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{CopernicusShiftedCenterFlow.StageKey(stage)}.options.{CopernicusShiftedCenterFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>());
            ApplyLabels(option.Title, state);
            ApplyLabels(option.Description, state);
            return option;
        }).ToArray();
    }

    private CopernicusShiftedCenterState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.CopernicusShiftedCenter is { } state
            && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:2"
            && shared.DemocritusArrangement is { } democritus && shared.EmpedoclesRootsInquiry is { } empedocles
            && CopernicusShiftedCenterFlow.IsRouteConsistent(state, journey, democritus, empedocles, WesternRouteGraph.LoadEmbedded())) return state;
        return null;
    }

    private void Block(string reason)
    {
        _ready = false;
        _blocked = true;
        if (Owner?.RunState is RunState run && IsActiveRoom(run))
        {
            if (!IsFinished) SetEventState(Description ?? InitialDescription, LockedOptions());
        }
        if (Owner?.RunState is RunState current) RouteEventMapBoundary.Freeze(current, reason);
        else Log.Error($"[STS2Philosophers] {reason}");
    }

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(CopernicusShiftedCenterState state) => FormatPage(state.Stage, state);

    private MegaCrit.Sts2.Core.Localization.LocString FormatPage(CopernicusShiftedCenterStage stage, CopernicusShiftedCenterState? state)
    {
        var description = L10NLookup($"{LocalizationRoot}.pages.{CopernicusShiftedCenterFlow.StageKey(stage)}.description");
        ApplyLabels(description, state);
        return description;
    }
    private void ApplyLabels(MegaCrit.Sts2.Core.Localization.LocString text, CopernicusShiftedCenterState? state)
    {
        string Label(string key) => L10NLookup($"{LocalizationRoot}.{key}").GetFormattedText();
        string Exit(int index)
        {
            string name = Label(index == 0 ? "labels.EXIT_A" : "labels.EXIT_B");
            if (state?.Fork?.Exits is not { Count: 2 }) return name;
            string type = state.Fork.Exits[index].PointType switch
            {
                "RestSite" => "REST_SITE", "Unknown" => "UNKNOWN", "Shop" => "SHOP", "Treasure" => "TREASURE",
                "Monster" => "MONSTER", "Elite" => "ELITE", "Boss" => "BOSS", "Ancient" => "ANCIENT", _ => "UNASSIGNED",
            };
            return $"{name} ({Label($"map_points.{type}")})";
        }
        text.Add("ExitA", Exit(0)); text.Add("ExitB", Exit(1));
        text.Add("Order", state?.Order is { Count: > 0 } order
            ? string.Join(" → ", order.Select(o => Label($"observations.{o.ToString().ToUpperInvariant()}")))
            : Label("observations.EMPTY"));
    }
}
