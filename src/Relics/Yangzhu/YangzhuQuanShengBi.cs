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

public sealed class YangzhuQuanShengBi : RelicModel
{
    private const int MaximumPreservedEnergy = 2;

    private YangzhuQuanShengBiState _yangzhuQuanShengBi;

    public override RelicRarity Rarity => RelicRarity.None;

    public override bool ShowCounter => IsMutable
        && _yangzhuQuanShengBi.PreservedEnergy > 0;

    public override int DisplayAmount => IsMutable
        ? _yangzhuQuanShengBi.PreservedEnergy
        : 0;

    public override string PackedIconPath => "res://STS2Philosophers/images/yangzhu_quan_sheng_bi.png";

    protected override string PackedIconOutlinePath => "res://STS2Philosophers/images/yangzhu_quan_sheng_bi_outline.png";

    protected override string BigIconPath => "res://STS2Philosophers/images/yangzhu_quan_sheng_bi.png";

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PreservedEnergy
    {
        get => _yangzhuQuanShengBi.PreservedEnergy;
        private set
        {
            AssertMutable();
            _yangzhuQuanShengBi.RestorePreservedEnergy(value);
            UpdatePresentation();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool DamageReductionActive
    {
        get => _yangzhuQuanShengBi.DamageReductionActive;
        private set
        {
            AssertMutable();
            _yangzhuQuanShengBi.RestoreDamageReductionActive(value);
            UpdatePresentation();
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastEvaluatedTurn
    {
        get => _yangzhuQuanShengBi.LastEvaluatedTurn;
        private set
        {
            AssertMutable();
            _yangzhuQuanShengBi.RestoreLastEvaluatedTurn(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingReturnTurn
    {
        get => _yangzhuQuanShengBi.PendingReturnTurn;
        private set
        {
            AssertMutable();
            _yangzhuQuanShengBi.RestorePendingReturnTurn(value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastReturnedTurn
    {
        get => _yangzhuQuanShengBi.LastReturnedTurn;
        private set
        {
            AssertMutable();
            _yangzhuQuanShengBi.RestoreLastReturnedTurn(value);
        }
    }

    public override Task BeforeCombatStart()
    {
        _yangzhuQuanShengBi.BeginCombat();
        UpdatePresentation();
        return Task.CompletedTask;
    }

    public override Task BeforeFlush(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        PlayerCombatState? combatState = Owner.PlayerCombatState;
        if (player == Owner
            && combatState is not null
            && _yangzhuQuanShengBi.EvaluateTurnEnd(
                combatState.TurnNumber,
                combatState.Energy,
                MaximumPreservedEnergy))
        {
            Flash();
        }

        UpdatePresentation();
        return Task.CompletedTask;
    }

    public override async Task AfterEnergyResetLate(Player player)
    {
        PlayerCombatState? combatState = Owner.PlayerCombatState;
        if (player != Owner
            || combatState is null
            || !_yangzhuQuanShengBi.TryTakePreservedEnergy(
                combatState.TurnNumber,
                out int energy))
        {
            return;
        }

        // Lock the return before awaiting so repeated reset hooks cannot return
        // the same preserved Energy more than once.
        UpdatePresentation();
        Flash();
        await PlayerCmd.GainEnergy(energy, Owner);
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal damage,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        return YangzhuQuanShengBiDamagePolicy.GetMultiplier(
            _yangzhuQuanShengBi.DamageReductionActive,
            target == Owner.Creature,
            dealer is { Side: CombatSide.Enemy },
            (props & ValueProp.Move) != 0);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _yangzhuQuanShengBi.EndCombat();
        UpdatePresentation();
        return Task.CompletedTask;
    }

    private void UpdatePresentation()
    {
        Status = _yangzhuQuanShengBi.DamageReductionActive
            ? RelicStatus.Active
            : RelicStatus.Normal;
        InvokeDisplayAmountChanged();
    }
}
