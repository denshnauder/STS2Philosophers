using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

public sealed class LockeCargoIdentity : EventModel
{
    private const string LocalizationRoot = "LOCKE_CARGO_IDENTITY";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        L10NLookup($"{LocalizationRoot}.pages.INVITATION.description");

    public override IEnumerable<MegaCrit.Sts2.Core.Localization.LocString> GameInfoOptions =>
        new[]
        {
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.CHECK_MEMORY"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.CHECK_SIGNATURE"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.CHECK_MARK"),
            L10NLookup($"{LocalizationRoot}.pages.INVITATION.options.KEEP_DESCARTES"),
        };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => LockedOptions();

    protected override void SetInitialEventState(bool isPreFinished)
    {
        if (TryRestoreState() is not { } state || isPreFinished && !state.Closed)
        {
            SetEventState(InitialDescription, LockedOptions());
            Block("LockeCargoIdentity cannot restore its saved event state.");
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
                Block("LockeCargoIdentity run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("LockeCargoIdentity saved event state is unavailable.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block("LockeCargoIdentity entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != LockeCargoAction.None
                && (!CommitPending(shared, state)
                    || !await RouteEventMapBoundary.PersistEventAsync(run, shared)))
            {
                Block("The saved LockeCargoIdentity choice remains blocked for recovery.");
                return;
            }
            if (state.Berkeley is { Conclusion: BerkeleyConclusion.SecondObserver } berkeley
                && !await ApplyBerkeleyCostAsync(run, shared, berkeley)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"LockeCargoIdentity entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(LockeCargoIdentityState expectedState, string expectedInstanceId,
        LockeCargoStage expectedStage, int expectedActionCount, LockeCargoAction action)
    {
        if (expectedState.InstanceId != expectedInstanceId || expectedState.Stage != expectedStage
            || expectedState.ConfirmedActions.Count != expectedActionCount) return;
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null)
            {
                Block("LockeCargoIdentity run state is unavailable.");
                return;
            }
            if (shared.LockeCargoIdentity is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != LockeCargoAction.None || state.Closed
                || !LockeCargoIdentityFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"LockeCargoIdentity prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"LockeCargoIdentity choice could not commit: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"LockeCargoIdentity committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"LockeCargoIdentity choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private static bool CommitPending(PhilosophyRunState shared, LockeCargoIdentityState state) =>
        ReferenceEquals(shared.LockeCargoIdentity, state) && shared.WesternJourney is { } journey
        && LockeCargoIdentityFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded());

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 0);

    private void ShowConfirmedState(LockeCargoIdentityState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(LockeCargoIdentityState state)
    {
        if (state.Inspecting && state.Berkeley is { } berkeley)
        {
            string berkeleyId = berkeley.InstanceId;
            BerkeleyMarkStage berkeleyStage = berkeley.Stage;
            int berkeleyCount = berkeley.ConfirmedActions.Count;
            return BerkeleyPerceivedMarkFlow.Options(berkeleyStage)
                .Where(action => action != BerkeleyMarkAction.RequestSecondObserver
                    || CanAffordSecondObserver())
                .Select(action => new EventOption(this,
                    () => ChooseBerkeley(state, berkeleyId, berkeleyStage, berkeleyCount, action),
                    $"BERKELEY_PERCEIVED_MARK.pages.{BerkeleyPerceivedMarkFlow.DescriptionKey(berkeley)}.options.{BerkeleyActionKey(action)}",
                    Array.Empty<IHoverTip>()))
                .ToArray();
        }
        string instanceId = state.InstanceId;
        LockeCargoStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return LockeCargoIdentityFlow.Options(stage)
            .Where(action => action != LockeCargoAction.InspectScreen || state.Berkeley is { Closed: false })
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{LockeCargoIdentityFlow.StageKey(stage)}.options.{LockeCargoIdentityFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private async Task ChooseBerkeley(LockeCargoIdentityState expectedState, string expectedBerkeleyId,
        BerkeleyMarkStage expectedStage, int expectedActionCount, BerkeleyMarkAction action)
    {
        if (!_ready || _blocked || Interlocked.Exchange(ref _resolving, 1) != 0) return;
        try
        {
            if (Owner?.RunState is not RunState run || !IsActiveRoom(run)) return;
            if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared is null) return;
            if (shared.LockeCargoIdentity is not { } state || !ReferenceEquals(state, expectedState)
                || state.Berkeley is not { } berkeley || berkeley.InstanceId != expectedBerkeleyId
                || berkeley.Stage != expectedStage || berkeley.ConfirmedActions.Count != expectedActionCount
                || !state.Inspecting) return;
            _ready = false;
            if (action == BerkeleyMarkAction.RequestSecondObserver && !CanAffordSecondObserver())
            {
                Block("The second observer costs gold the player does not have.");
                return;
            }
            if (!LockeCargoIdentityFlow.TryApplyBerkeley(state, action))
            {
                Block($"The Berkeley segment could not apply: {action}.");
                return;
            }
            if (!await RouteEventMapBoundary.PersistEventAsync(run, shared))
            {
                Block($"The Berkeley segment persistence is unknown: {action}.");
                return;
            }
            if (!await ApplyBerkeleyCostAsync(run, shared, berkeley)) return;
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"The Berkeley segment failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private bool CanAffordSecondObserver() =>
        Owner is { } owner && owner.Gold >= BerkeleyPerceivedMarkState.SecondObserverGoldCost;

    private async Task<bool> ApplyBerkeleyCostAsync(RunState run, PhilosophyRunState shared,
        BerkeleyPerceivedMarkState berkeley)
    {
        if (berkeley.Conclusion != BerkeleyConclusion.SecondObserver) return true;
        EventGoldCostOutcome outcome = await EventGoldCostProtocol.ApplyAsync(
            berkeley,
            () => Owner is null ? null : (decimal?)Owner.Gold,
            async () => await PlayerCmd.LoseGold(berkeley.Cost, Owner!, GoldLossType.Spent),
            () => RouteEventMapBoundary.PersistEventAsync(run, shared));
        switch (outcome)
        {
            case EventGoldCostOutcome.OwnerUnavailable:
                Block("The second observer has no owner to charge.");
                return false;
            case EventGoldCostOutcome.PersistenceUnknown:
                Block("The second observer cost persistence is unknown.");
                return false;
            default:
                return true;
        }
    }

    private static string BerkeleyActionKey(BerkeleyMarkAction action) => string.Concat(
        action.ToString().SelectMany((c, i) => i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));

    private LockeCargoIdentityState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.LockeCargoIdentity is { } state
            && shared.WesternJourney is { } journey
            && state.InstanceId == $"{LocalizationRoot}:{run.Rng.Seed}:1"
            && LockeCargoIdentityFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(LockeCargoIdentityState state) =>
        state.Inspecting && state.Berkeley is { } berkeley
            ? L10NLookup($"BERKELEY_PERCEIVED_MARK.pages.{BerkeleyPerceivedMarkFlow.DescriptionKey(berkeley)}.description")
            : L10NLookup($"{LocalizationRoot}.pages.{LockeCargoIdentityFlow.DescriptionKey(state)}.description");
}
