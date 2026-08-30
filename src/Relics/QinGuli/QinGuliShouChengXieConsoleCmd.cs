using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2Philosophers;

public sealed class QinGuliShouChengXieConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "qingulishouchengxie";

    public override string Args => "";

    public override string Description => "Grant City Defense Machinery for Qin Guli testing.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer is null)
        {
            return new CmdResult(false, "A run is currently not in progress.");
        }

        if (issuingPlayer.GetRelicById(ModelDb.GetId<QinGuliShouChengXie>()) is not null)
        {
            return new CmdResult(true, "City Defense Machinery is already in this player's relic inventory.");
        }

        return new CmdResult(
            RelicCmd.Obtain<QinGuliShouChengXie>(issuingPlayer),
            true,
            "Granted City Defense Machinery.");
    }
}
