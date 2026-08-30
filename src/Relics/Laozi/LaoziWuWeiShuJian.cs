using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2Philosophers;

public sealed class LaoziWuWeiShuJian : RelicModel
{
    private const int MaximumCardsPlayed = 2;
    private const int EnergyReward = 2;
    private const int DexterityReward = 2;

    private LaoziWuWeiShuJianState _laoziWuWeiShuJian;
    private bool _hasResolvedPhilosophersGazeContinuation;

    public override RelicRarity Rarity => RelicRarity.None;

    public override string PackedIconPath => "res://STS2Philosophers/images/laozi_wu_wei_shu_jian.png";

    protected override string PackedIconOutlinePath => "res://STS2Philosophers/images/laozi_wu_wei_shu_jian_outline.png";

    protected override string BigIconPath => "res://STS2Philosophers/images/laozi_wu_wei_shu_jian.png";

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasResolvedPhilosophersGazeContinuation
    {
        get => _hasResolvedPhilosophersGazeContinuation;
        private set
        {
            AssertMutable();
            _hasResolvedPhilosophersGazeContinuation = value;
        }
    }

    internal void RecordPhilosophersGazeContinuation()
    {
        HasResolvedPhilosophersGazeContinuation = true;
    }

    internal void RestorePhilosophersGazeContinuation(bool resolved)
    {
        HasResolvedPhilosophersGazeContinuation = resolved;
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CardsPlayedThisTurn
    {
        get => _laoziWuWeiShuJian.CardsPlayedThisTurn;
        private set
        {
            AssertMutable();
            _laoziWuWeiShuJian.RestoreCardsPlayedThisTurn(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastEvaluatedTurn
    {
        get => _laoziWuWeiShuJian.LastEvaluatedTurn;
        private set
        {
            AssertMutable();
            _laoziWuWeiShuJian.RestoreLastEvaluatedTurn(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingRewardTurn
    {
        get => _laoziWuWeiShuJian.PendingRewardTurn;
        private set
        {
            AssertMutable();
            _laoziWuWeiShuJian.RestorePendingRewardTurn(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastRewardedTurn
    {
        get => _laoziWuWeiShuJian.LastRewardedTurn;
        private set
        {
            AssertMutable();
            _laoziWuWeiShuJian.RestoreLastRewardedTurn(value);
        }
    }

    public override Task BeforeCombatStart()
    {
        _laoziWuWeiShuJian.BeginCombat();
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == CombatSide.Player && participants.Contains(Owner.Creature))
        {
            _laoziWuWeiShuJian.BeginPlayerTurn();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner)
        {
            _laoziWuWeiShuJian.RecordCardPlayed(cardPlay);
        }

        return Task.CompletedTask;
    }

    public override Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        PlayerCombatState? combatState = Owner.PlayerCombatState;
        if (player != Owner
            || combatState is null
            || !_laoziWuWeiShuJian.TryQualifyTurn(
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

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        PlayerCombatState? combatState = Owner.PlayerCombatState;
        if (player != Owner
            || combatState is null
            || !_laoziWuWeiShuJian.TryTakePendingReward(combatState.TurnNumber))
        {
            return;
        }

        Flash();
        await PlayerCmd.GainEnergy(EnergyReward, Owner);
        await PowerCmd.Apply<DexterityPower>(
            choiceContext,
            Owner.Creature,
            DexterityReward,
            Owner.Creature,
            null);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _laoziWuWeiShuJian.EndCombat();
        return Task.CompletedTask;
    }
}
