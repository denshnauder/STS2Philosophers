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

public sealed class KongziMuduo : RelicModel
{
    private const int StrengthCap = 5;

    private KongziMuduoState _kongziMuduo;
    private bool _hasResolvedPhilosophersGazeContinuation;

    public override RelicRarity Rarity => RelicRarity.None;

    public override bool ShowCounter => CombatManager.Instance.IsInProgress;

    public override int DisplayAmount => IsMutable ? _kongziMuduo.GrantedStrength : 0;

    public override string PackedIconPath => "res://STS2MinimalMod/images/kongzi_muduo.png";

    protected override string PackedIconOutlinePath => "res://STS2MinimalMod/images/kongzi_muduo_outline.png";

    protected override string BigIconPath => "res://STS2MinimalMod/images/kongzi_muduo.png";

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

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(Owner.Creature))
        {
            _kongziMuduo.BeginTurn();
            UpdatePresentation();
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner)
        {
            return;
        }

        if (cardPlay.Card.Type is CardType.Skill or CardType.Power)
        {
            _kongziMuduo.HonorRitual();
            UpdatePresentation();
            return;
        }

        if (cardPlay.Card.Type == CardType.Attack && _kongziMuduo.TryDishonor())
        {
            UpdatePresentation();
            Flash();
            await PowerCmd.Apply<KongziMuduoDiscourtesyStrengthPower>(
                new ThrowingPlayerChoiceContext(),
                Owner.Creature,
                1m,
                Owner.Creature,
                cardPlay.Card);
        }
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || cardPlay.Card.Type != CardType.Skill)
        {
            return;
        }

        int amountToGrant = _kongziMuduo.GetNextSkillStrengthToGrant(StrengthCap);
        if (amountToGrant <= 0)
        {
            return;
        }

        int strengthBefore = Owner.Creature.GetPowerAmount<KongziMuduoRitualStrengthPower>();
        Flash();
        await PowerCmd.Apply<KongziMuduoRitualStrengthPower>(
            choiceContext,
            Owner.Creature,
            amountToGrant,
            Owner.Creature,
            cardPlay.Card);
        int strengthAfter = Owner.Creature.GetPowerAmount<KongziMuduoRitualStrengthPower>();

        _kongziMuduo.RecordGrantedStrength(strengthAfter - strengthBefore, StrengthCap);
        UpdatePresentation();
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return;
        }

        _kongziMuduo.EndTurn();
        UpdatePresentation();
        await Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _kongziMuduo.EndTurn();
        UpdatePresentation();
        return Task.CompletedTask;
    }

    private void UpdatePresentation()
    {
        Status = _kongziMuduo.Order switch
        {
            KongziMuduoOrder.Honored => RelicStatus.Active,
            KongziMuduoOrder.Dishonored => RelicStatus.Disabled,
            _ => RelicStatus.Normal,
        };
        InvokeDisplayAmountChanged();
    }
}
