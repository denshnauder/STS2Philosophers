using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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

public sealed class MoziShouChengTu : RelicModel
{
    private MoziShouChengTuState _moziShouChengTu;

    public override RelicRarity Rarity => RelicRarity.None;

    public override string PackedIconPath => "res://STS2MinimalMod/images/mozi_shou_cheng_tu.png";

    protected override string PackedIconOutlinePath => "res://STS2MinimalMod/images/mozi_shou_cheng_tu_outline.png";

    protected override string BigIconPath => "res://STS2MinimalMod/images/mozi_shou_cheng_tu.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(MoziShouChengTuState.BlockAmount, ValueProp.Unpowered),
    ];

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasActiveDefenseWindow
    {
        get => _moziShouChengTu.HasActiveDefenseWindow;
        set
        {
            AssertMutable();
            _moziShouChengTu.HasActiveDefenseWindow = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool EnemyDamageTaken
    {
        get => _moziShouChengTu.EnemyDamageTaken;
        set
        {
            AssertMutable();
            _moziShouChengTu.EnemyDamageTaken = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastBoundaryTurn
    {
        get => _moziShouChengTu.LastBoundaryTurn;
        set
        {
            AssertMutable();
            _moziShouChengTu.LastBoundaryTurn = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastIntentSampleTurn
    {
        get => _moziShouChengTu.LastIntentSampleTurn;
        set
        {
            AssertMutable();
            _moziShouChengTu.LastIntentSampleTurn = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool HasPendingReward
    {
        get => _moziShouChengTu.HasPendingReward;
        set
        {
            AssertMutable();
            _moziShouChengTu.HasPendingReward = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PendingRewardTurn
    {
        get => _moziShouChengTu.PendingRewardTurn;
        set
        {
            AssertMutable();
            _moziShouChengTu.PendingRewardTurn = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastRewardedTurn
    {
        get => _moziShouChengTu.LastRewardedTurn;
        set
        {
            AssertMutable();
            _moziShouChengTu.LastRewardedTurn = Math.Max(0, value);
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

        MoziShouChengTuState nextState = _moziShouChengTu;
        if (nextState.BeginPlayerTurn(playerCombatState.TurnNumber))
        {
            ApplyState(nextState);
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

        MoziShouChengTuState nextState = _moziShouChengTu;
        if (!nextState.TryTakePendingReward(
                playerCombatState.TurnNumber,
                out bool shouldGrantBlock))
        {
            return;
        }

        // Save the payout lock before awaiting so synchronization or replay cannot
        // grant the same successful defense twice.
        ApplyState(nextState);
        if (!shouldGrantBlock)
        {
            return;
        }

        Flash();
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay: null,
            fast: true);
    }

    public override Task AfterPlayerTurnStartLate(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner)
        {
            return Task.CompletedTask;
        }

        PlayerCombatState? playerCombatState = Owner.PlayerCombatState;
        ICombatState? combatState = Owner.Creature.CombatState;
        if (playerCombatState is null || combatState is null)
        {
            return Task.CompletedTask;
        }

        bool anyLivingEnemyIntendsToAttack = combatState.Enemies.Any(enemy =>
            enemy.IsAlive
            && enemy.Monster is { IntendsToAttack: true });

        MoziShouChengTuState nextState = _moziShouChengTu;
        if (nextState.SampleEnemyIntents(
                playerCombatState.TurnNumber,
                anyLivingEnemyIntendsToAttack))
        {
            ApplyState(nextState);
        }

        return Task.CompletedTask;
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (!MoziShouChengTuDamagePolicy.IsEnemyHpLossToOwner(
                result.Receiver == Owner.Creature,
                target == Owner.Creature,
                dealer is { Side: CombatSide.Enemy },
                result.UnblockedDamage))
        {
            return Task.CompletedTask;
        }

        MoziShouChengTuState nextState = _moziShouChengTu;
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
        MoziShouChengTuState nextState = _moziShouChengTu;
        nextState.Reset();
        ApplyState(nextState);
    }

    private void ApplyState(MoziShouChengTuState state)
    {
        AssertMutable();
        _moziShouChengTu = state;
    }
}
