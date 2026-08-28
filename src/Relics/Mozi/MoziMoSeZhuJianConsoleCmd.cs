using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2MinimalMod;

public sealed class MoziMoSeZhuJianConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "mozimosezhujian";

    public override string Args => "";

    public override string Description => "Grant the Ink Bamboo Slips relic for Mohist testing.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer is null)
        {
            return new CmdResult(false, "A run is currently not in progress.");
        }

        if (issuingPlayer.GetRelicById(ModelDb.GetId<MoziMoSeZhuJian>()) is not null)
        {
            return new CmdResult(true, "Ink Bamboo Slips is already in this player's relic inventory.");
        }

        return new CmdResult(
            RelicCmd.Obtain<MoziMoSeZhuJian>(issuingPlayer),
            true,
            "Granted Ink Bamboo Slips. Mutual Benefit begins tracking this combat's next player turn.");
    }
}
