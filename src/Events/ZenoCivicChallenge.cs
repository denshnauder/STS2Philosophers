using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;

namespace STS2Philosophers;

public sealed class ZenoCivicChallenge : EventModel
{
    private const string LocalizationRoot = "ZENO_CIVIC_CHALLENGE";
    private int _resolving;
    private bool _ready;
    private bool _blocked;

    public override MegaCrit.Sts2.Core.Localization.LocString InitialDescription =>
        PageDescription(ZenoCivicStage.Invitation);

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
            Block("ZenoCivicChallenge cannot restore its saved event state.");
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
                Block("ZenoCivicChallenge run state is unavailable.");
                return;
            }
            if (TryRestoreState() is not { } state)
            {
                Block("ZenoCivicChallenge saved event state is unavailable.");
                return;
            }
            // Old unfinished pages explicitly opt into the new closing reward
            // before buttons appear. Closed old records retain their old result.
            if (!state.Closed && !state.TryEnableSealUpgrade())
            {
                Block("ZenoCivicChallenge cannot enable its closing grant protocol.");
                return;
            }
            if (!await PersistCivicAsync(run, shared, state))
            {
                Block("ZenoCivicChallenge entry persistence is unknown.");
                return;
            }
            if (state.PendingAction != ZenoCivicAction.None
                && (!CommitPending(shared, state)
                    || !await PersistCivicAsync(run, shared, state)))
            {
                Block("The saved ZenoCivicChallenge choice remains blocked for recovery.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"ZenoCivicChallenge entry failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private async Task Choose(ZenoCivicChallengeState expectedState, string expectedInstanceId,
        ZenoCivicStage expectedStage, int expectedActionCount, ZenoCivicAction action)
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
                Block("ZenoCivicChallenge run state is unavailable.");
                return;
            }
            if (shared.ZenoCivicChallenge is not { } state || !ReferenceEquals(state, expectedState)
                || state.InstanceId != expectedInstanceId || state.Stage != expectedStage
                || state.ConfirmedActions.Count != expectedActionCount
                || state.PendingAction != ZenoCivicAction.None || state.Closed
                || !ZenoCivicChallengeFlow.TryPrepare(state, action)) return;
            _ready = false;
            SetEventState(PageDescription(state), LockedOptions());
            if (!await PersistCivicAsync(run, shared, state))
            {
                Block($"ZenoCivicChallenge prepared choice persistence is unknown: {action}.");
                return;
            }
            if (!IsActiveRoom(run) || !CommitPending(shared, state))
            {
                Block($"ZenoCivicChallenge choice could not commit: {action}.");
                return;
            }
            if (!await PersistCivicAsync(run, shared, state))
            {
                Block($"ZenoCivicChallenge committed choice persistence is unknown: {action}.");
                return;
            }
            if (IsActiveRoom(run)) ShowConfirmedState(state);
        }
        catch (Exception exception)
        {
            Block($"ZenoCivicChallenge choice failed: {exception}");
        }
        finally
        {
            Volatile.Write(ref _resolving, 0);
        }
    }

    private bool CommitPending(PhilosophyRunState shared, ZenoCivicChallengeState state)
    {
        if (!ReferenceEquals(shared.ZenoCivicChallenge, state) || shared.WesternJourney is not { } journey
            || !ZenoCivicChallengeFlow.IsRouteConsistent(state, journey)) return false;
        bool grants = state.PendingAction == ZenoCivicAction.Leave && state.SealUpgradeVersion == 1
            && state.SealUpgradeBranch != ZenoCivicUpgradeBranch.None;
        PlatoCivicSeal? seal = grants ? Owner?.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) as PlatoCivicSeal : null;
        if (grants && (seal is null || !seal.CanGrantZenoCivicUpgrade(state.InstanceId, state.SealUpgradeBranch))) return false;
        if (!ZenoCivicChallengeFlow.TryCommitPending(state, journey, WesternRouteGraph.LoadEmbedded())) return false;
        return !grants || seal!.TryGrantZenoCivicUpgrade(state.InstanceId, state.SealUpgradeBranch);
    }

    private async Task<bool> PersistCivicAsync(RunState run, PhilosophyRunState shared, ZenoCivicChallengeState state)
    {
        bool granted = state.SealUpgradeVersion == 1 && state.SealUpgradeGranted == true;
        PlatoCivicSeal? seal = granted ? Owner?.GetRelicById(ModelDb.GetId<PlatoCivicSeal>()) as PlatoCivicSeal : null;
        // A closed new record with a missing/mismatched native grant is blocked,
        // not repaired by replaying historical choices or inventing another seal.
        if (granted && (seal is null || !seal.HasZenoCivicUpgrade(state.InstanceId, state.SealUpgradeBranch))) return false;
        if (!await RouteEventMapBoundary.PersistEventAsync(run, shared)) return false;
        if (!granted) return true;
        var loaded = SaveManager.Instance.LoadRunSave();
        return loaded.Status == ReadSaveStatus.Success && loaded.SaveData is { } saved
            && SavedSealUpgradeMatches(saved, Owner!.NetId, seal!.ZenoCivicUpgradePayload,
                state.InstanceId, state.SealUpgradeBranch);
    }

    internal static bool SavedSealUpgradeMatches(SerializableRun save, ulong ownerId, string expectedPayload,
        string instanceId, ZenoCivicUpgradeBranch branch)
    {
        if (string.IsNullOrEmpty(expectedPayload) || save.Players is not { Count: 1 }
            || save.Players[0].NetId != ownerId) return false;
        var seals = save.Players[0].Relics.Where(relic => Equals(relic.Id, ModelDb.GetId<PlatoCivicSeal>())).ToArray();
        if (seals.Length != 1 || RelicModel.FromSerializable(seals[0]) is not PlatoCivicSeal restored) return false;
        // This native deserialization creates a detached verification copy; it
        // never replaces an active player's relic or rebinds shared run state.
        return restored.ZenoCivicUpgradePayload == expectedPayload
            && restored.HasZenoCivicUpgrade(instanceId, branch);
    }

    private bool IsActiveRoom(RunState run) => RouteEventMapBoundary.IsActiveEvent(run, this, 0);

    private void ShowConfirmedState(ZenoCivicChallengeState state)
    {
        _ready = !state.Closed;
        if (state.Closed) SetEventFinished(PageDescription(state));
        else SetEventState(PageDescription(state), GenerateOptions(state));
    }

    private IReadOnlyList<EventOption> LockedOptions() =>
        [new EventOption(this, null, "ROUTE_EVENT.options.RECOVERING", Array.Empty<IHoverTip>())];

    private IReadOnlyList<EventOption> GenerateOptions(ZenoCivicChallengeState state)
    {
        string instanceId = state.InstanceId;
        ZenoCivicStage stage = state.Stage;
        int actionCount = state.ConfirmedActions.Count;
        return ZenoCivicChallengeFlow.Options(stage)
            .Select(action => new EventOption(this,
                () => Choose(state, instanceId, stage, actionCount, action),
                $"{LocalizationRoot}.pages.{ZenoCivicChallengeFlow.StageKey(stage)}.options.{ZenoCivicChallengeFlow.ActionKey(action)}",
                Array.Empty<IHoverTip>()))
            .ToArray();
    }

    private ZenoCivicChallengeState? TryRestoreState()
    {
        if (Owner?.RunState is RunState run
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.ZenoCivicChallenge is { } state
            && shared.WesternJourney is { } journey
            && ZenoCivicChallengeFlow.IsRouteConsistent(state, journey)) return state;
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

    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(ZenoCivicStage stage) =>
        L10NLookup($"{LocalizationRoot}.pages.{ZenoCivicChallengeFlow.StageKey(stage)}.description");
    private MegaCrit.Sts2.Core.Localization.LocString PageDescription(ZenoCivicChallengeState state) =>
        L10NLookup($"{LocalizationRoot}.pages.{ZenoCivicChallengeFlow.DescriptionKey(state)}.description");
}
