using MegaCrit.Sts2.Core.Combat;
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

public sealed class LaoziShuiYu : RelicModel
{
    private LaoziShuiYuState _laoziShuiYu;
    private bool _hasResolvedPhilosophersGazeContinuation;

    public override RelicRarity Rarity => RelicRarity.None;

    public override string PackedIconPath => "res://STS2Philosophers/images/laozi_shui_yu.png";

    protected override string PackedIconOutlinePath => "res://STS2Philosophers/images/laozi_shui_yu_outline.png";

    protected override string BigIconPath => "res://STS2Philosophers/images/laozi_shui_yu.png";

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
    public bool DamageReductionActive
    {
        get => _laoziShuiYu.DamageReductionActive;
        private set
        {
            AssertMutable();
            _laoziShuiYu.RestoreDamageReductionActive(value);
            UpdatePresentation();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastEvaluatedTurn
    {
        get => _laoziShuiYu.LastEvaluatedTurn;
        private set
        {
            AssertMutable();
            _laoziShuiYu.RestoreLastEvaluatedTurn(value);
        }
    }

    public override Task BeforeCombatStart()
    {
        _laoziShuiYu.BeginCombat();
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
            _laoziShuiYu.BeginPlayerTurn();
            UpdatePresentation();
        }

        return Task.CompletedTask;
    }

    public override Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        PlayerCombatState? combatState = Owner.PlayerCombatState;
        if (player == Owner
            && combatState is not null
            && _laoziShuiYu.EvaluateTurnEnd(
                combatState.TurnNumber,
                combatState.Energy))
        {
            Flash();
        }

        UpdatePresentation();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal damage,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return LaoziShuiYuDamagePolicy.GetMultiplier(
            _laoziShuiYu.DamageReductionActive,
            target == Owner.Creature,
            dealer is { Side: CombatSide.Enemy },
            (props & ValueProp.Move) != 0);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _laoziShuiYu.EndCombat();
        UpdatePresentation();
        return Task.CompletedTask;
    }

    private void UpdatePresentation()
    {
        Status = _laoziShuiYu.DamageReductionActive
            ? RelicStatus.Active
            : RelicStatus.Normal;
    }
}
