using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace STS2Philosophers;

// An opt-in diagnostic, not a trial ability or a persistent card identity service.
public sealed class SocratesCardProbeConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "socratescardprobe";
    public override string Args => "";
    public override string Description => "Inspect native Silent basic card conversion references without saving or changing cards.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 0) return new CmdResult(false, "Usage: socratescardprobe");
        if (issuingPlayer is null || issuingPlayer.RunState.Players.Count != 1)
            return new CmdResult(false, "A single-player run is required.");
        if (CombatManager.Instance.IsInProgress)
            return new CmdResult(false, "Run this diagnostic outside combat.");

        // ToSerializable invokes saved-property getters. Only these two native types,
        // with no enchantment and no saved properties, are covered by this probe.
        var methods = typeof(CardModel).GetMethods().Where(m => m.Name == nameof(CardModel.ToSerializable))
            .Concat(typeof(SavedProperties).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name is "From" or "FromInternal"));
        if (methods.Any(m => Harmony.GetPatchInfo(m)?.Owners.Count > 0))
            return new CmdResult(false, "Conversion methods are patched; this limited probe refuses to invoke them.");

        try
        {
            CardModel[] deck = issuingPlayer.Deck.Cards.ToArray();
            CardModel[] cards = deck.Where(c => (c.GetType() == typeof(StrikeSilent) || c.GetType() == typeof(DefendSilent))
                && c.IsMutable && c.Enchantment is null).ToArray();
            if (cards.Length == 0)
                return new CmdResult(true, "INCONCLUSIVE: no supported, unenchanted native Silent basic cards in this deck.");
            if (cards.Any(c => c.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(p => p.CustomAttributes.Any(a => a.AttributeType == typeof(SavedPropertyAttribute)))))
                return new CmdResult(false, "Saved-property getters exist; this limited probe refuses to invoke them.");

            var rows = new List<SocratesCardProbeRow>();
            foreach (CardModel card in cards)
            {
                string before = Fields(card);
                SerializableCard first = card.ToSerializable();
                SerializableCard second = card.ToSerializable();
                if (first.Props is not null || second.Props is not null || first.Enchantment is not null || second.Enchantment is not null)
                    return new CmdResult(false, "Unexpected serialized properties or enchantment; evidence rejected.");
                rows.Add(new SocratesCardProbeRow(card, first, second, before, Fields(card), Fields(first), Fields(second)));
            }
            if (!SocratesCardProbeReport.Validate(deck, issuingPlayer.Deck.Cards.ToArray(), rows, out string reason))
                return new CmdResult(false, "EVIDENCE REJECTED: " + reason);

            int matchingGroups = rows.GroupBy(r => r.BeforeFields).Count(g => g.Count() > 1);
            string status = matchingGroups > 0 ? "OBSERVED" : "INCONCLUSIVE (no matching copies)";
            return new CmdResult(true,
                $"{status}: {rows.Count} existing card references, {rows.Count * 2} distinct conversion record references; "
                + $"{matchingGroups} group(s) with matching model/upgrade/floor fields; observed fields and deck references unchanged. "
                + "References apply only to this invocation. This does not verify final save arrays, load identity, transformation, or trial safety.");
        }
        catch (Exception exception)
        {
            return new CmdResult(false, $"Probe stopped: {exception.GetType().Name}. No card repair or save operation attempted.");
        }
    }

    private static string Fields(CardModel card) => $"{card.Id}|{card.CurrentUpgradeLevel}|{card.FloorAddedToDeck}";
    private static string Fields(SerializableCard card) => $"{card.Id}|{card.CurrentUpgradeLevel}|{card.FloorAddedToDeck}";
}
