using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2MinimalMod;

public sealed class MoziShouChengTuConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "mozishouchengtu";

    public override string Args => "";

    public override string Description => "Grant the City Defense Diagram relic for Mohist testing.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer is null)
        {
            return new CmdResult(false, "A run is currently not in progress.");
        }

        if (issuingPlayer.GetRelicById(ModelDb.GetId<MoziShouChengTu>()) is not null)
        {
            return new CmdResult(true, "City Defense Diagram is already in this player's relic inventory.");
        }

        return new CmdResult(
            RelicCmd.Obtain<MoziShouChengTu>(issuingPlayer),
            true,
            "Granted City Defense Diagram. Defensive observation begins on the next player turn.");
    }
}
