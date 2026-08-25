using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2MinimalMod;

public sealed class MengziXiongZhang : RelicModel
{
    private MengziXiongZhangState _mengziXiongZhang;

    public override RelicRarity Rarity => RelicRarity.None;

    // Explicit temporary placeholder until Bear Paw receives final artwork.
    public override string PackedIconPath => "res://STS2MinimalMod/images/kongzi_qing_yu_pei.png";

    protected override string PackedIconOutlinePath => "res://STS2MinimalMod/images/kongzi_qing_yu_pei_outline.png";

    protected override string BigIconPath => "res://STS2MinimalMod/images/kongzi_qing_yu_pei.png";

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int LastTriggeredTurn
    {
        get => _mengziXiongZhang.LastTriggeredTurn;
        private set
        {
            AssertMutable();
            _mengziXiongZhang.RestoreLastTriggeredTurn(value);
        }
    }

    public override Task BeforeCombatStart()
    {
        _mengziXiongZhang.BeginCombat();
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner || player.PlayerCombatState is null)
        {
            return;
        }

        int turnNumber = player.PlayerCombatState.TurnNumber;
        int virtue = KongziQingYuPei.GetVirtue(Owner);
        if (!_mengziXiongZhang.TryTrigger(turnNumber, virtue))
        {
            return;
        }

        // Lock this saved turn before awaiting either reward. A repeated hook,
        // reconnect, or restored combat therefore cannot pay the same turn twice.
        Flash();
        Log.Info($"[STS2MinimalMod] Bear Paw triggered for player {Owner.NetId} on turn {turnNumber} with {virtue} Virtue.");
        await PlayerCmd.GainEnergy(1m, Owner);
        await CardPileCmd.Draw(choiceContext, 2m, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _mengziXiongZhang.EndCombat();
        return Task.CompletedTask;
    }
}
