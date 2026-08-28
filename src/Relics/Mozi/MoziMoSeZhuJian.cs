using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2MinimalMod;

public sealed class MoziMoSeZhuJian : RelicModel
{
    private MoziMoSeZhuJianState _moziMoSeZhuJian;

    public override RelicRarity Rarity => RelicRarity.None;

    public override bool ShowCounter => CombatManager.Instance.IsInProgress;

    public override int DisplayAmount => IsMutable ? XiangLi : 0;

    public override string PackedIconPath => "res://STS2MinimalMod/images/mozi_mo_se_zhu_jian.png";

    protected override string PackedIconOutlinePath => "res://STS2MinimalMod/images/mozi_mo_se_zhu_jian_outline.png";

    protected override string BigIconPath => "res://STS2MinimalMod/images/mozi_mo_se_zhu_jian.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(MoziMoSeZhuJianState.BlockAmount, ValueProp.Unpowered),
    ];

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int XiangLi
    {
        get => _moziMoSeZhuJian.XiangLi;
        set
        {
            AssertMutable();
            _moziMoSeZhuJian.XiangLi = Math.Clamp(
                value,
                0,
                MoziMoSeZhuJianState.XiangLiCap);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool CurrentRoundUnharmed
    {
        get => _moziMoSeZhuJian.CurrentRoundUnharmed;
        set
        {
            AssertMutable();
            _moziMoSeZhuJian.CurrentRoundUnharmed = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastBoundaryTurn
    {
        get => _moziMoSeZhuJian.LastBoundaryTurn;
        set
        {
            AssertMutable();
            _moziMoSeZhuJian.LastBoundaryTurn = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingRewardAmount
    {
        get => _moziMoSeZhuJian.PendingRewardAmount;
        set
        {
            AssertMutable();
            _moziMoSeZhuJian.PendingRewardAmount = Math.Clamp(
                value,
                0,
                MoziMoSeZhuJianState.XiangLiCap);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingRewardTurn
    {
        get => _moziMoSeZhuJian.PendingRewardTurn;
        set
        {
            AssertMutable();
            _moziMoSeZhuJian.PendingRewardTurn = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastRewardedTurn
    {
        get => _moziMoSeZhuJian.LastRewardedTurn;
        set
        {
            AssertMutable();
            _moziMoSeZhuJian.LastRewardedTurn = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastBlockGrantedTurn
    {
        get => _moziMoSeZhuJian.LastBlockGrantedTurn;
        set
        {
            AssertMutable();
            _moziMoSeZhuJian.LastBlockGrantedTurn = Math.Max(0, value);
        }
    }

    public override Task BeforeCombatStart()
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        PlayerCombatState? playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState is null)
        {
            return Task.CompletedTask;
        }

        MoziMoSeZhuJianState nextState = _moziMoSeZhuJian;
        if (nextState.BeginPlayerTurn(playerCombatState.TurnNumber))
        {
            ApplyState(nextState);
            UpdatePresentation();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (creature != Owner.Creature)
        {
            return;
        }

        PlayerCombatState? playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState is null)
        {
            return;
        }

        int turnNumber = playerCombatState.TurnNumber;
        MoziMoSeZhuJianState nextState = _moziMoSeZhuJian;
        if (!nextState.TryMarkBlockGranted(turnNumber))
        {
            return;
        }

        ApplyState(nextState);
        ICombatState? combatState = Owner.Creature.CombatState;
        if (combatState is null)
        {
            return;
        }

        List<Creature> targets = combatState.Creatures
            .Where(target => target.IsAlive)
            .ToList();
        Flash();
        foreach (Creature target in targets)
        {
            await CreatureCmd.GainBlock(
                target,
                DynamicVars.Block,
                cardPlay: null,
                fast: true);
        }
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner)
        {
            return;
        }

        PlayerCombatState? playerCombatState = Owner.PlayerCombatState;
        if (playerCombatState is null)
        {
            return;
        }

        MoziMoSeZhuJianState nextState = _moziMoSeZhuJian;
        if (!nextState.TryTakePendingReward(
                playerCombatState.TurnNumber,
                out int amount))
        {
            return;
        }

        ApplyState(nextState);
        if (amount <= 0)
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(amount, Owner);
        await CardPileCmd.Draw(choiceContext, amount, Owner);
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        ICombatState? combatState = Owner.Creature.CombatState;
        if (delta >= 0m
            || !CombatManager.Instance.IsInProgress
            || combatState is null
            || !combatState.ContainsCreature(creature))
        {
            return Task.CompletedTask;
        }

        MoziMoSeZhuJianState nextState = _moziMoSeZhuJian;
        if (nextState.RecordHpChange(delta))
        {
            ApplyState(nextState);
            UpdatePresentation();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    private void ResetCombatState()
    {
        MoziMoSeZhuJianState nextState = _moziMoSeZhuJian;
        nextState.Reset();
        ApplyState(nextState);
        UpdatePresentation();
    }

    private void ApplyState(MoziMoSeZhuJianState state)
    {
        AssertMutable();
        _moziMoSeZhuJian = state;
    }

    private void UpdatePresentation()
    {
        Status = XiangLi > 0
            ? RelicStatus.Active
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }
}
