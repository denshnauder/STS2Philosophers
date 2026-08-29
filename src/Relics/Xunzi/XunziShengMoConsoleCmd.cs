using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2Philosophers;

public sealed class XunziShengMoConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "xunzishengmo";

    public override string Args => "";

    public override string Description => "Grant the Ink Line relic for Xunzi testing.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer is null)
        {
            return new CmdResult(false, "A run is currently not in progress.");
        }

        if (issuingPlayer.GetRelicById(ModelDb.GetId<XunziShengMo>()) is not null)
        {
            return new CmdResult(true, "Ink Line is already in this player's relic inventory.");
        }

        return new CmdResult(
            RelicCmd.Obtain<XunziShengMo>(issuingPlayer),
            true,
            "Granted Ink Line. Its sequence tracking begins with the player's next card play.");
    }
}
