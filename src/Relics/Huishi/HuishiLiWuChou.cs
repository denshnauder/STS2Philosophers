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

namespace STS2Philosophers;

public sealed class HuishiLiWuChou : RelicModel
{
    private HuishiLiWuChouState _huishiLiWuChou;

    public override RelicRarity Rarity => RelicRarity.None;

    public override string PackedIconPath => "res://STS2Philosophers/images/huishi_li_wu_chou.png";

    protected override string PackedIconOutlinePath => "res://STS2Philosophers/images/huishi_li_wu_chou_outline.png";

    protected override string BigIconPath => "res://STS2Philosophers/images/huishi_li_wu_chou.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(HuishiLiWuChouState.SharedBlock, ValueProp.Unpowered),
        new DamageVar(HuishiLiWuChouState.RewardDamage, ValueProp.Unpowered),
    ];

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastTurnStarted
    {
        get => _huishiLiWuChou.LastTurnStarted;
        private set
        {
            AssertMutable();
            _huishiLiWuChou.LastTurnStarted = Math.Max(0, value);
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastTriggeredTurn
    {
        get => _huishiLiWuChou.LastTriggeredTurn;
        private set
        {
            AssertMutable();
            _huishiLiWuChou.LastTriggeredTurn = Math.Max(0, value);
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

        HuishiLiWuChouState nextState = _huishiLiWuChou;
        if (!nextState.BeginPlayerTurn(playerCombatState.TurnNumber))
        {
            return;
        }

        ApplyState(nextState);
        List<Creature> targets =
        [
            Owner.Creature,
            .. combatState.Enemies.Where(enemy => enemy.IsAlive),
        ];

        Flash();
        foreach (Creature target in targets)
        {
            await CreatureCmd.GainBlock(
                target,
                HuishiLiWuChouState.SharedBlock,
                ValueProp.Unpowered,
                cardPlay: null,
                fast: true);
        }
    }

    public override async Task AfterBlockBroken(
        PlayerChoiceContext choiceContext,
        Creature target,
        Creature? breaker)
    {
        if (Owner.PlayerCombatState is not { } playerCombatState)
        {
            return;
        }

        HuishiLiWuChouState nextState = _huishiLiWuChou;
        if (!nextState.TryRewardBlockBreak(
                playerCombatState.TurnNumber,
                target.Side == CombatSide.Enemy,
                breaker is { Side: CombatSide.Player }))
        {
            return;
        }

        ApplyState(nextState);
        Flash();
        await PlayerCmd.GainEnergy(1m, Owner);
        await CardPileCmd.Draw(choiceContext, 1, Owner);

        if (Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        Creature[] livingEnemies = combatState.Enemies
            .Where(enemy => enemy.IsAlive)
            .ToArray();
        if (livingEnemies.Length > 0)
        {
            await CreatureCmd.Damage(
                choiceContext,
                livingEnemies,
                HuishiLiWuChouState.RewardDamage,
                ValueProp.Unpowered,
                Owner.Creature);
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetCombatState();
        return Task.CompletedTask;
    }

    private void ResetCombatState()
    {
        HuishiLiWuChouState nextState = _huishiLiWuChou;
        nextState.Reset();
        ApplyState(nextState);
    }

    private void ApplyState(HuishiLiWuChouState state)
    {
        AssertMutable();
        _huishiLiWuChou = state;
    }
}
