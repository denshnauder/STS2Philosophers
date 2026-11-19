using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace STS2Philosophers;

// The seal remembers the first manual Attack and first manual Skill of a turn.
// It returns only the chronologically first of those two, and only while that
// exact card is still in the discard pile. Auto-plays never enter this record.
internal sealed class PlatoCivicSealTurnState
{
    private const string Version = "1";

    public int Turn { get; private set; }
    public string FirstAttackId { get; private set; } = string.Empty;
    public string FirstSkillId { get; private set; } = string.Empty;
    public CardModel? FirstAttackCard { get; private set; }
    public CardModel? FirstSkillCard { get; private set; }
    public bool FirstWasAttack { get; private set; }
    public CardModel? FirstCard => FirstWasAttack ? FirstAttackCard : FirstSkillCard;
    public bool AttackRecorded => FirstAttackId.Length > 0;
    public bool SkillRecorded => FirstSkillId.Length > 0;
    public bool Resolved { get; private set; }
    public bool Returned { get; private set; }
    public int RecordedCount => (AttackRecorded ? 1 : 0) + (SkillRecorded ? 1 : 0);

    public bool Observe(int turn, CardModel card, out string returnCardId) =>
        Observe(turn, card.Id.ToString(), card.Type, card, out returnCardId);

    public bool Observe(int turn, string cardId, CardType type, CardModel? card,
        out string returnCardId)
    {
        returnCardId = string.Empty;
        if (turn <= 0 || type is not (CardType.Attack or CardType.Skill)
            || string.IsNullOrWhiteSpace(cardId)) return false;
        if (Turn != turn)
        {
            Turn = turn;
            FirstAttackId = string.Empty;
            FirstSkillId = string.Empty;
            FirstAttackCard = null;
            FirstSkillCard = null;
            FirstWasAttack = false;
            Resolved = false;
            Returned = false;
        }

        if (type == CardType.Attack && FirstAttackId.Length == 0)
        {
            FirstAttackId = cardId;
            FirstAttackCard = card;
            if (FirstSkillId.Length == 0) FirstWasAttack = true;
        }
        else if (type == CardType.Skill && FirstSkillId.Length == 0)
        {
            FirstSkillId = cardId;
            FirstSkillCard = card;
            if (FirstAttackId.Length == 0) FirstWasAttack = false;
        }
        else
        {
            return false;
        }

        if (!AttackRecorded || !SkillRecorded || Resolved) return false;
        Resolved = true;
        returnCardId = FirstWasAttack ? FirstAttackId : FirstSkillId;
        return returnCardId.Length > 0;
    }

    public void MarkReturned()
    {
        Returned = true;
    }

    public string Encode() =>
        $"{Version}|{Turn}|{Escape(FirstAttackId)}|{Escape(FirstSkillId)}|{(FirstWasAttack ? "A" : "S")}|{(Resolved ? "1" : "0")}|{(Returned ? "1" : "0")}";

    public static bool TryRestore(string? payload, out PlatoCivicSealTurnState state)
    {
        state = new PlatoCivicSealTurnState();
        if (string.IsNullOrWhiteSpace(payload)) return true;
        string[] parts = payload.Split('|');
        if (parts.Length != 7 || parts[0] != Version
            || !int.TryParse(parts[1], out int turn) || turn < 0
            || !TryUnescape(parts[2], out string attack)
            || !TryUnescape(parts[3], out string skill)
            || parts[4] is not ("A" or "S")
            || parts[5] is not ("0" or "1")
            || parts[6] is not ("0" or "1")) return false;
        if (attack.Length == 0 && skill.Length == 0 && parts[5] == "1") return false;
        state.Turn = turn;
        state.FirstAttackId = attack;
        state.FirstSkillId = skill;
        state.FirstWasAttack = parts[4] == "A";
        state.Resolved = parts[5] == "1";
        state.Returned = parts[6] == "1";
        return true;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static bool TryUnescape(string value, out string result)
    {
        try
        {
            result = Uri.UnescapeDataString(value);
            return true;
        }
        catch (UriFormatException)
        {
            result = string.Empty;
            return false;
        }
    }
}
