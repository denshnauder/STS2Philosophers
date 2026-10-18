using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2Philosophers;

public sealed class AnaximenesBreath : RelicModel
{
    private AnaximenesBreathPracticeState _practice;

    public override RelicRarity Rarity => RelicRarity.None;
    public override string PackedIconPath => "res://STS2Philosophers/images/anaximenes_breath.svg";
    protected override string PackedIconOutlinePath => "res://STS2Philosophers/images/anaximenes_breath_outline.svg";
    protected override string BigIconPath => PackedIconPath;
    public override bool ShowCounter => Owner?.PlayerCombatState is not null;
    public override int DisplayAmount => _practice.BankedTurn > _practice.RedeemedTurn ? 1 : 0;

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int BankedTurn
    {
        get => _practice.BankedTurn;
        private set { AssertMutable(); _practice.BankedTurn = Math.Max(0, value); }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int RedeemedTurn
    {
        get => _practice.RedeemedTurn;
        private set { AssertMutable(); _practice.RedeemedTurn = Math.Max(0, value); }
    }

    public override Task BeforeCombatStart()
    {
        _practice.Reset();
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }

    public override Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner && Owner.PlayerCombatState is { } combat
            && _practice.TryBank(combat.TurnNumber, combat.Energy))
        {
            Flash();
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStartLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner || Owner.PlayerCombatState is not { } combat
            || !_practice.TryRedeem(combat.TurnNumber)) return;
        Flash();
        InvokeDisplayAmountChanged();
        await PlayerCmd.GainEnergy(1m, Owner);
        await CardPileCmd.Draw(choiceContext, 1m, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _practice.Reset();
        InvokeDisplayAmountChanged();
        return Task.CompletedTask;
    }
}
