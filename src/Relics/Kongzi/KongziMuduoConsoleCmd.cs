using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2MinimalMod;

public sealed class KongziMuduoConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "kongzimuduo";

    public override string Args => "";

    public override string Description => "Grant the Muduo relic for Confucian MVP01 testing.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer is null)
        {
            return new CmdResult(false, "A run is currently not in progress.");
        }

        if (issuingPlayer.GetRelicById(ModelDb.GetId<KongziMuduo>()) is not null)
        {
            return new CmdResult(true, "Muduo is already in this player's relic inventory.");
        }

        return new CmdResult(
            RelicCmd.Obtain<KongziMuduo>(issuingPlayer),
            true,
            "Granted Muduo. Zhou Li begins at the start of the player's next turn.");
    }
}
