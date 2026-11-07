using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

/// <summary>
/// Benjamin's independent second-act question after Foucault's fixed room.
/// It presents the existing Benjamin perspective page without advancing a route
/// or spending the act's main question.
/// </summary>
public sealed class BenjaminHistory : EventModel
{
    private const string LocalizationRoot = "FOUCAULT_POWER";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.BENJAMIN.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[] { L10NLookup($"{LocalizationRoot}.pages.BENJAMIN.options.LEAVE") };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("BenjaminHistory cannot restore its saved event state.");
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
                Block("BenjaminHistory run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("BenjaminHistory saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("BenjaminHistory entry persistence is unknown.");
                return;
            }
            ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"BenjaminHistory entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(BenjaminHistoryState expectedState, string expectedInstanceId)
    {
        if (expectedState.InstanceId != expectedInstanceId) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("BenjaminHistory run state is unavailable.");
                return;
            }
            if (shared.BenjaminHistory is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Closed) return;
            _ready = false;
            state.Closed = true;
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("BenjaminHistory leave persistence is unknown.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"BenjaminHistory leave failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private bool IsActiveRoom(RunState run)
    {
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.BenjaminHistory is not { } state) return false;
        return RouteEventMapBoundary.IsActiveEvent(run, this, state.ActIndex - 1);
    }

    private void ShowConfirmedState(BenjaminHistoryState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(BenjaminHistoryState state)
    {
        string instanceId = state.InstanceId;
        return
        [
            new EventOption(this, () => Choose(state, instanceId),
                $"{LocalizationRoot}.pages.BENJAMIN.options.LEAVE", Array.Empty<IHoverTip>()),
        ];
    }

    private BenjaminHistoryState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.BenjaminHistory is { } state
            && state.InstanceId == $"BENJAMIN_HISTORY:{run.Rng.Seed}:{state.ActIndex}"
            && state.IsUsable()) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(BenjaminHistoryState state) =>
        L10NLookup($"{LocalizationRoot}.pages.BENJAMIN.description");
}
