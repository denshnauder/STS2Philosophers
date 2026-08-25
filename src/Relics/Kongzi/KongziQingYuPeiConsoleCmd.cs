using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2MinimalMod;

public sealed class KongziQingYuPeiConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "kongziqingyupei";

    public override string Args => "";

    public override string Description => "Grant the Green Jade Pendant relic for Confucian MVP02 testing.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer is null)
        {
            return new CmdResult(false, "A run is currently not in progress.");
        }

        if (issuingPlayer.GetRelicById(ModelDb.GetId<KongziQingYuPei>()) is not null)
        {
            return new CmdResult(true, "Green Jade Pendant is already in this player's relic inventory.");
        }

        return new CmdResult(
            RelicCmd.Obtain<KongziQingYuPei>(issuingPlayer),
            true,
            "Granted Green Jade Pendant. Its conduct check begins on the player's next turn.");
    }
}
