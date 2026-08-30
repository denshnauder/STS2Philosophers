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
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2Philosophers;

public sealed class QinGuliShouChengXie : RelicModel
{
    private QinGuliShouChengXieState _qinGuliShouChengXie;

    public override RelicRarity Rarity => RelicRarity.None;

    public override string PackedIconPath => "res://STS2Philosophers/images/qin_guli_shou_cheng_xie.png";

    protected override string PackedIconOutlinePath => "res://STS2Philosophers/images/qin_guli_shou_cheng_xie_outline.png";

    protected override string BigIconPath => "res://STS2Philosophers/images/qin_guli_shou_cheng_xie.png";

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasActiveDefenseWindow
    {
        get => _qinGuliShouChengXie.HasActiveDefenseWindow;
        private set
        {
            AssertMutable();
            _qinGuliShouChengXie.HasActiveDefenseWindow = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool EnemyDamageTaken
    {
        get => _qinGuliShouChengXie.EnemyDamageTaken;
        private set
        {
            AssertMutable();
            _qinGuliShouChengXie.EnemyDamageTaken = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastBoundaryTurn
    {
        get => _qinGuliShouChengXie.LastBoundaryTurn;
        private set
        {
            AssertMutable();
            _qinGuliShouChengXie.LastBoundaryTurn = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastIntentSampleTurn
    {
        get => _qinGuliShouChengXie.LastIntentSampleTurn;
        private set
        {
            AssertMutable();
            _qinGuliShouChengXie.LastIntentSampleTurn = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastRewardedTurn
    {
        get => _qinGuliShouChengXie.LastRewardedTurn;
        private set
        {
            AssertMutable();
            _qinGuliShouChengXie.LastRewardedTurn = Math.Max(0, value);
        }
    }

    public override Task BeforeCombatStart()
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStartLate(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner
            || Owner.PlayerCombatState is not { } playerCombatState
            || Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        QinGuliShouChengXieState nextState = _qinGuliShouChengXie;
        if (!nextState.BeginPlayerTurn(
                playerCombatState.TurnNumber,
                out bool shouldApplyWeak))
        {
            return;
        }

        ApplyState(nextState);
        if (shouldApplyWeak)
        {
            Creature[] livingEnemies = combatState.Enemies
                .Where(enemy => enemy.IsAlive)
                .ToArray();
            if (livingEnemies.Length > 0)
            {
                Flash();
                await PowerCmd.Apply<WeakPower>(
                    choiceContext,
                    livingEnemies,
                    1m,
                    Owner.Creature,
                    null);
            }
        }

        int attackingEnemyCount = combatState.Enemies.Count(enemy =>
            enemy.IsAlive
            && enemy.Monster is { IntendsToAttack: true });
        nextState = _qinGuliShouChengXie;
        if (!nextState.SampleEnemyIntents(
                playerCombatState.TurnNumber,
                attackingEnemyCount,
                out int blockAmount))
        {
            return;
        }

        ApplyState(nextState);
        if (blockAmount > 0)
        {
            Flash();
            await CreatureCmd.GainBlock(
                Owner.Creature,
                blockAmount,
                ValueProp.Unpowered,
                cardPlay: null,
                fast: true);
        }
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (!QinGuliShouChengXieDamagePolicy.IsEnemyHpLossToOwner(
                result.Receiver == Owner.Creature,
                target == Owner.Creature,
                dealer is { Side: CombatSide.Enemy },
                result.UnblockedDamage))
        {
            return Task.CompletedTask;
        }

        QinGuliShouChengXieState nextState = _qinGuliShouChengXie;
        if (nextState.RecordEnemyHpLoss(result.UnblockedDamage))
        {
            ApplyState(nextState);
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
        QinGuliShouChengXieState nextState = _qinGuliShouChengXie;
        nextState.Reset();
        ApplyState(nextState);
    }

    private void ApplyState(QinGuliShouChengXieState state)
    {
        AssertMutable();
        _qinGuliShouChengXie = state;
    }
}
