using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2Philosophers;

public sealed class ZhuangziDaHu : RelicModel
{
    private const int MaximumCardsPlayed = 2;
    private const int DiscountedPlayCount = 2;
    private const int BlockPerDiscountedPlay = 4;

    private ZhuangziDaHuState _zhuangziDaHu;

    public override RelicRarity Rarity => RelicRarity.None;

    public override string PackedIconPath => "res://STS2Philosophers/images/zhuangzi_da_hu.png";

    protected override string PackedIconOutlinePath => "res://STS2Philosophers/images/zhuangzi_da_hu_outline.png";

    protected override string BigIconPath => "res://STS2Philosophers/images/zhuangzi_da_hu.png";

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CardsPlayedThisTurn
    {
        get => _zhuangziDaHu.CardsPlayedThisTurn;
        private set
        {
            AssertMutable();
            _zhuangziDaHu.RestoreCardsPlayedThisTurn(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastStartedTurn
    {
        get => _zhuangziDaHu.LastStartedTurn;
        private set
        {
            AssertMutable();
            _zhuangziDaHu.RestoreLastStartedTurn(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastEvaluatedTurn
    {
        get => _zhuangziDaHu.LastEvaluatedTurn;
        private set
        {
            AssertMutable();
            _zhuangziDaHu.RestoreLastEvaluatedTurn(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingRewardTurn
    {
        get => _zhuangziDaHu.PendingRewardTurn;
        private set
        {
            AssertMutable();
            _zhuangziDaHu.RestorePendingRewardTurn(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int DiscountedPlaysRemaining
    {
        get => _zhuangziDaHu.DiscountedPlaysRemaining;
        private set
        {
            AssertMutable();
            _zhuangziDaHu.RestoreDiscountedPlaysRemaining(value);
        }
    }

    public override Task BeforeCombatStart()
    {
        _zhuangziDaHu.BeginCombat();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == CombatSide.Player
            && participants.Contains(Owner.Creature)
            && Owner.PlayerCombatState is { } playerCombatState)
        {
            _zhuangziDaHu.BeginPlayerTurn(
                playerCombatState.TurnNumber,
                DiscountedPlayCount);
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal cost,
        out decimal modifiedCost)
    {
        modifiedCost = cost;
        if (card.Owner != Owner
            || card.EnergyCost.CostsX
            || !_zhuangziDaHu.CanDiscountNextPlay)
        {
            return false;
        }

        modifiedCost = Math.Max(0m, cost - 1m);
        return modifiedCost != cost;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner)
        {
            return;
        }

        bool shouldGrantBlock = _zhuangziDaHu.RecordCardPlayed(cardPlay);
        if (!shouldGrantBlock)
        {
            return;
        }

        Flash();
        await CreatureCmd.GainBlock(
            Owner.Creature,
            BlockPerDiscountedPlay,
            ValueProp.Unpowered,
            cardPlay,
            fast: true);
    }

    public override Task BeforeFlush(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        PlayerCombatState? combatState = Owner.PlayerCombatState;
        if (player != Owner
            || combatState is null
            || !_zhuangziDaHu.TryQualifyTurn(
                combatState.TurnNumber,
                MaximumCardsPlayed))
        {
            return Task.CompletedTask;
        }

        foreach (CardModel card in combatState.Hand.Cards)
        {
            CardCmd.ApplySingleTurnRetain(card);
        }

        Flash();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _zhuangziDaHu.EndCombat();
        return Task.CompletedTask;
    }
}
