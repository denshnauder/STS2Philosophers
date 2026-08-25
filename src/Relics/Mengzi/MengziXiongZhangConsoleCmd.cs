using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2MinimalMod;

public sealed class MengziXiongZhangConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "mengzixiongzhang";

    public override string Args => "[virtue [amount:int]]";

    public override string Description => "Grant Bear Paw, inspect Virtue, or set Virtue for testing.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer is null)
        {
            return new CmdResult(false, "A run is currently not in progress.");
        }

        if (args.Length == 0)
        {
            if (issuingPlayer.GetRelicById(ModelDb.GetId<MengziXiongZhang>()) is not null)
            {
                return new CmdResult(
                    true,
                    $"Bear Paw is already in this player's relic inventory. Current Virtue: {KongziQingYuPei.GetVirtue(issuingPlayer)}.");
            }

            return new CmdResult(
                RelicCmd.Obtain<MengziXiongZhang>(issuingPlayer),
                true,
                $"Granted Bear Paw. Current Virtue: {KongziQingYuPei.GetVirtue(issuingPlayer)}.");
        }

        if (!args[0].Equals("virtue", StringComparison.OrdinalIgnoreCase))
        {
            return new CmdResult(false, "Usage: mengzixiongzhang [virtue [amount]].");
        }

        if (args.Length == 1)
        {
            return new CmdResult(true, $"Current Virtue: {KongziQingYuPei.GetVirtue(issuingPlayer)}.");
        }

        if (args.Length != 2 || !int.TryParse(args[1], out int virtue) || virtue < 0)
        {
            return new CmdResult(false, "Virtue must be a non-negative integer.");
        }

        return new CmdResult(
            SetVirtue(issuingPlayer, virtue),
            true,
            $"Set this player's Virtue to {virtue}.");
    }

    private static async Task SetVirtue(Player player, int virtue)
    {
        KongziQingYuPei? kongziQingYuPei = player.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) as KongziQingYuPei;
        kongziQingYuPei ??= await RelicCmd.Obtain<KongziQingYuPei>(player);
        kongziQingYuPei.SetVirtueForDebug(virtue);
    }
}
