using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2Philosophers;

public sealed class PlatoCivicSeal : WesternPracticeRelic
{
    protected override string ProblemId => "FREEDOM_AND_INSTITUTIONS";
    protected override string ResourceName => "plato_civic_seal";

    internal bool CanOfferLuther => IsMutable && Owner is not null && IsSoleDoctrine;

    private AugustineCombatPracticeState? _augustinePractice;
    private string? _invalidAugustinePayload;
    // Canonical models are copied to mutable relics. Keep the canonical field
    // empty so separate copies never share a mutable first-card receipt.
    private AquinasCombatPracticeState? _aquinasPractice;
    private string? _invalidAquinasPayload;
    private ZenoCivicSealUpgradeState? _zenoUpgrade;
    private string? _invalidZenoPayload;
    private HashSet<CardPlay>? _zenoCallbacks;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string ZenoCivicUpgradePayload
    {
        get => _invalidZenoPayload ?? _zenoUpgrade?.Encode() ?? string.Empty;
        private set
        {
            AssertMutable();
            // A bad/unknown native saved property remains verbatim and inert.
            if (_invalidZenoPayload is not null) return;
            if (ZenoCivicSealUpgradeState.TryRestore(value, out var restored)) _zenoUpgrade = restored;
            else _invalidZenoPayload = value;
        }
    }

    internal bool HasZenoCivicUpgrade(string instanceId, ZenoCivicUpgradeBranch branch) =>
        _invalidZenoPayload is null && _zenoUpgrade?.Matches(instanceId, branch) == true;

    internal bool CanGrantZenoCivicUpgrade(string instanceId, ZenoCivicUpgradeBranch branch) =>
        CanOfferLuther && _invalidZenoPayload is null && (_zenoUpgrade is not null
            ? _zenoUpgrade.Matches(instanceId, branch)
            : ZenoCivicSealUpgradeState.Create(instanceId, branch).IsUsable());

    internal bool TryGrantZenoCivicUpgrade(string instanceId, ZenoCivicUpgradeBranch branch)
    {
        if (!CanGrantZenoCivicUpgrade(instanceId, branch)) return false;
        if (_zenoUpgrade is not null) return true;
        var grant = ZenoCivicSealUpgradeState.Create(instanceId, branch);
        _zenoUpgrade = grant;
        Flash();
        InvokeDisplayAmountChanged();
        return true;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string AugustinePracticePayload
    {
        get => _invalidAugustinePayload ?? _augustinePractice?.Encode() ?? string.Empty;
        private set
        {
            AssertMutable();
            if (AugustineCombatPracticeState.TryRestore(value, out AugustineCombatPracticeState restored))
            {
                _augustinePractice = restored;
                _invalidAugustinePayload = null;
            }
            else _invalidAugustinePayload = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public string AquinasPracticePayload
    {
        get => _invalidAquinasPayload ?? _aquinasPractice?.Encode() ?? string.Empty;
        private set
        {
            AssertMutable();
            if (AquinasCombatPracticeState.TryRestore(value, out AquinasCombatPracticeState restored))
            {
                _aquinasPractice = restored;
                _invalidAquinasPayload = null;
            }
            else _invalidAquinasPayload = value;
        }
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            List<IHoverTip> tips = base.ExtraHoverTips.ToList();
            if (HasLutherSwitch()) tips.Add(new HoverTip(
                new LocString("relics", "PLATO_CIVIC_SEAL.luther.title"),
                new LocString("relics", "PLATO_CIVIC_SEAL.luther.description")));
            else if (_invalidAugustinePayload is null && TryGetAugustineWork(out AugustineWork work))
            {
                string branch = work == AugustineWork.Public ? "public" : "private";
                tips.Add(new HoverTip(
                    new LocString("relics", $"PLATO_CIVIC_SEAL.augustine.{branch}.title"),
                    new LocString("relics", $"PLATO_CIVIC_SEAL.augustine.{branch}.description")));
            }
            else if (_invalidAquinasPayload is null && TryGetAquinasRule(out AquinasRule rule))
            {
                string branch = rule == AquinasRule.Published ? "published" : "emergency";
                tips.Add(new HoverTip(
                    new LocString("relics", $"PLATO_CIVIC_SEAL.aquinas.{branch}.title"),
                    new LocString("relics", $"PLATO_CIVIC_SEAL.aquinas.{branch}.description")));
            }
            string? zeno = _invalidZenoPayload is not null ? "unavailable"
                : TryGetZenoUpgrade(out var upgrade) ? upgrade.Branch == ZenoCivicUpgradeBranch.Public
                    ? "public" : "conditional" : null;
            if (zeno is not null) tips.Add(new HoverTip(
                new LocString("relics", $"PLATO_CIVIC_SEAL.zeno.{zeno}.title"),
                new LocString("relics", $"PLATO_CIVIC_SEAL.zeno.{zeno}.description")));
            return tips;
        }
    }

    public override Task BeforeCombatStart()
    {
        _zenoCallbacks = null;
        return base.BeforeCombatStart();
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Capture the already established law before the base hook can establish
        // it with this combat's first manual Attack/Skill. Also remember that
        // first callback: replaying it must never turn lawmaking into a reward.
        WesternPracticeCardKind lawBefore = CurrentLawKind;
        bool freshZenoCallback = cardPlay.Player == Owner
            && (_zenoCallbacks ??= new(ReferenceEqualityComparer.Instance)).Add(cardPlay);
        await base.AfterCardPlayed(choiceContext, cardPlay);
        if (freshZenoCallback) await ResolveZenoUpgrade(choiceContext, cardPlay, lawBefore);
        if (cardPlay.Player != Owner || cardPlay.IsAutoPlay || _invalidAquinasPayload is not null
            || !TryGetAquinasRule(out AquinasRule rule)) return;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run?.CurrentRoom is not CombatRoom room) return;
        string combatKey = $"{run.CurrentActIndex}:{run.ActFloor}:{room.Id}";
        bool matches = rule == AquinasRule.Published && cardPlay.Card.Type == CardType.Skill
            || rule == AquinasRule.Emergency && cardPlay.Card.Type == CardType.Attack;
        if (!(_aquinasPractice ??= new()).ObserveFirstManual(combatKey, matches)) return;
        Flash();
        if (rule == AquinasRule.Published)
            await CreatureCmd.GainBlock(Owner.Creature, 5, ValueProp.Unpowered,
                cardPlay: null, fast: true);
        else
            await PlayerCmd.GainEnergy(1m, Owner);
    }

    private async Task ResolveZenoUpgrade(PlayerChoiceContext context, CardPlay play,
        WesternPracticeCardKind lawBefore)
    {
        if (play.IsAutoPlay || lawBefore is not (WesternPracticeCardKind.Attack or WesternPracticeCardKind.Skill)
            || play.Card.Type is not (CardType.Attack or CardType.Skill)
            || Owner.PlayerCombatState is not { } combat || combat.TurnNumber <= 0
            || !TryGetZenoUpgrade(out var upgrade)) return;
        WesternPracticeCardKind kind = play.Card.Type == CardType.Attack
            ? WesternPracticeCardKind.Attack : WesternPracticeCardKind.Skill;
        bool matches = upgrade.Branch == ZenoCivicUpgradeBranch.Public ? kind == lawBefore : kind != lawBefore;
        RunState? run = RunManager.Instance.DebugOnlyGetState();
        if (!matches || run?.CurrentRoom is not CombatRoom { Id: int roomId }) return;
        string key = $"{run.CurrentActIndex}:{run.ActFloor}:{roomId}";
        // Persist the claim before an awaited native command can cause callbacks.
        if (!upgrade.TryClaim(key, combat.TurnNumber)) return;
        Flash();
        if (upgrade.Branch == ZenoCivicUpgradeBranch.Public)
            await CreatureCmd.GainBlock(Owner.Creature, 3, ValueProp.Unpowered, cardPlay: null, fast: true);
        else await CardPileCmd.Draw(context, 1m, Owner);
    }

    private bool TryGetZenoUpgrade(out ZenoCivicSealUpgradeState upgrade)
    {
        upgrade = null!;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (!CanOfferLuther || _invalidZenoPayload is not null || _zenoUpgrade?.IsUsable() != true
            || run is null || run.Players.Count != 1 || !ReferenceEquals(run.Players[0], Owner)
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || shared.ZenoCivicChallenge is not { Closed: true, EdgeCommitted: true,
                SealUpgradeVersion: 1, SealUpgradeGranted: true } state || !state.IsUsable()
            || !_zenoUpgrade.Matches(state.InstanceId, state.SealUpgradeBranch)
            || !journey.ResolvedQuestionActs.Contains(1)
            || !journey.CompletedEdgeIds.Contains(ZenoCivicChallengeState.EdgeId)) return false;
        upgrade = _zenoUpgrade;
        return true;
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        await base.AfterPlayerTurnStartLate(choiceContext, player);
        if (player != Owner || _invalidAugustinePayload is not null
            || Owner.PlayerCombatState?.TurnNumber != 1
            || !TryGetAugustineWork(out AugustineWork work)) return;
        RunState run = RunManager.Instance.DebugOnlyGetState()!;
        if (run.CurrentRoom is not CombatRoom room) return;
        string combatKey = $"{run.CurrentActIndex}:{run.ActFloor}:{room.Id}";
        if (!(_augustinePractice ??= new()).TryClaim(combatKey)) return;
        Flash();
        if (work == AugustineWork.Public)
            await CreatureCmd.GainBlock(Owner.Creature, 6, ValueProp.Unpowered,
                cardPlay: null, fast: true);
        else
            await PlayerCmd.GainEnergy(1m, Owner);
    }

    private bool HasLutherSwitch()
    {
        if (!CanOfferLuther) return false;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        return run is not null && run.Players.Count == 1 && ReferenceEquals(run.Players[0], Owner)
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.WesternJourney is { } journey && shared.AugustinePromise is { } predecessor
            && shared.LutherSignedCare is { Closed: true, EdgeCommitted: true } state
            && LutherSignedCareFlow.IsRouteConsistent(state, journey, predecessor);
    }

    private bool TryGetAugustineWork(out AugustineWork work)
    {
        work = AugustineWork.None;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex < 1 || run.Players.Count != 1
            || !ReferenceEquals(run.Players[0], Owner) || !IsSoleDoctrine
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || shared.AugustinePromise is not { EdgeCommitted: true, Closed: true } state
            || state.Work is not (AugustineWork.Public or AugustineWork.Private)
            || !AugustinePromiseFlow.IsRouteConsistent(state, journey)) return false;
        work = state.Work;
        return true;
    }

    private bool TryGetAquinasRule(out AquinasRule rule)
    {
        rule = AquinasRule.None;
        RunManager manager = RunManager.Instance;
        RunState? run = manager.IsInProgress ? manager.DebugOnlyGetState() : null;
        if (run is null || run.CurrentActIndex < 2 || run.Players.Count != 1
            || !ReferenceEquals(run.Players[0], Owner) || !IsSoleDoctrine
            || !PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            || shared?.WesternJourney is not { } journey
            || shared.AquinasBridgeNotice is not { EdgeCommitted: true, Closed: true } state
            || state.Rule is not (AquinasRule.Published or AquinasRule.Emergency)
            || !AquinasBridgeNoticeFlow.IsRouteConsistent(state, journey)) return false;
        rule = state.Rule;
        return true;
    }
}
