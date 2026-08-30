using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2Philosophers;

public sealed class HuishiLiWuChouConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "huishiliwuchou";

    public override string Args => "";

    public override string Description => "Grant Enumeration Counters for Huishi testing.";

    public override bool IsNetworked => true;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer is null)
        {
            return new CmdResult(false, "A run is currently not in progress.");
        }

        if (issuingPlayer.GetRelicById(ModelDb.GetId<HuishiLiWuChou>()) is not null)
        {
            return new CmdResult(true, "Enumeration Counters is already in this player's relic inventory.");
        }

        return new CmdResult(
            RelicCmd.Obtain<HuishiLiWuChou>(issuingPlayer),
            true,
            "Granted Enumeration Counters.");
    }
}
