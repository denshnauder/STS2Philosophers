using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2MinimalMod;

public sealed class XunziShengMo : RelicModel
{
    private XunziShengMoState _xunziShengMo;

    public override RelicRarity Rarity => RelicRarity.None;

    public override bool ShowCounter => CombatManager.Instance.IsInProgress
        && IsMutable
        && !_xunziShengMo.HasTriggeredThisTurn
        && _xunziShengMo.Progress > 0;

    public override int DisplayAmount => IsMutable ? _xunziShengMo.Progress : 0;

    public override string PackedIconPath => "res://STS2MinimalMod/images/xunzi_sheng_mo.png";

    protected override string PackedIconOutlinePath => "res://STS2MinimalMod/images/xunzi_sheng_mo_outline.png";

    protected override string BigIconPath => "res://STS2MinimalMod/images/xunzi_sheng_mo.png";

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int SequenceProgress
    {
        get => _xunziShengMo.Progress;
        private set
        {
            AssertMutable();
            _xunziShengMo.RestoreProgress(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasTriggeredThisTurn
    {
        get => _xunziShengMo.HasTriggeredThisTurn;
        private set
        {
            AssertMutable();
            _xunziShengMo.RestoreTriggeredThisTurn(value);
        }
    }

    public override Task BeforeCombatStart()
    {
        _xunziShengMo.BeginCombat();
        UpdatePresentation();
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
            _xunziShengMo.BeginTurn();
            UpdatePresentation();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner || _xunziShengMo.HasTriggeredThisTurn)
        {
            return;
        }

        XunziShengMoCardKind cardKind = cardPlay.Card.Type switch
        {
            CardType.Skill => XunziShengMoCardKind.Skill,
            CardType.Attack => XunziShengMoCardKind.Attack,
            _ => XunziShengMoCardKind.Other,
        };

        bool shouldGrantReward = _xunziShengMo.RecordCard(cardKind, cardPlay);
        UpdatePresentation();
        if (!shouldGrantReward)
        {
            return;
        }

        // RecordCard locks this turn before either awaited command so replayed hooks,
        // synchronization passes, and reconnect restoration cannot repay the sequence.
        Flash();
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            cardPlay.Card);
        await PowerCmd.Apply<DexterityPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            cardPlay.Card);
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner.Creature))
        {
            _xunziShengMo.EndTurn();
            UpdatePresentation();
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _xunziShengMo.EndCombat();
        UpdatePresentation();
        return Task.CompletedTask;
    }

    private void UpdatePresentation()
    {
        InvokeDisplayAmountChanged();
    }
}
