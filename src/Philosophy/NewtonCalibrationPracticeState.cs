using System.Text.Json;

namespace STS2Philosophers;

internal enum NewtonCalibrationCardKind { Other, Attack, Skill }

internal sealed class NewtonCalibrationPracticeState
{
    public const int CurrentVersion = 1;
    private const int MaxCards = 3;
    private const int MaxPayload = 4096;
    private const int MaxKeyLength = 100;

    private readonly List<NewtonCalibrationCardKind> _playedCards = new();

    public int Version { get; private set; } = CurrentVersion;
    public string CombatKey { get; private set; } = string.Empty;
    public int TurnNumber { get; private set; }
    public int ManualCardCount { get; private set; }
    public IReadOnlyList<NewtonCalibrationCardKind> PlayedCards => _playedCards;
    public bool Claimed { get; private set; }
    public NewtonSite ClaimedSite { get; private set; } = NewtonSite.None;

    public bool StartCombat(string combatKey)
    {
        if (!IsValidKey(combatKey) || combatKey == CombatKey)
            return false;

        ResetForCombat(combatKey);
        return true;
    }

    public bool StartTurn(string combatKey, int turn)
    {
        if (!IsValidKey(combatKey) || turn <= 0)
            return false;

        if (combatKey != CombatKey)
        {
            ResetForCombat(combatKey);
            TurnNumber = turn;
            return true;
        }

        if (turn <= TurnNumber)
            return false;

        ResetForCombat(combatKey);
        TurnNumber = turn;
        return true;
    }

    public bool RecordManualCard(string combatKey, int turn, NewtonCalibrationCardKind kind)
    {
        if (string.IsNullOrEmpty(combatKey) || combatKey != CombatKey)
            return false;
        if (turn <= 0 || turn != TurnNumber)
            return false;
        if (!IsDefinedKind(kind) || Claimed)
            return false;
        if (ManualCardCount >= MaxCards)
            return false;

        _playedCards.Add(kind);
        ManualCardCount++;
        return true;
    }

    public bool TryClaim(string combatKey, int turn, NewtonSite site)
    {
        if (string.IsNullOrEmpty(combatKey) || combatKey != CombatKey)
            return false;
        if (turn <= 0 || turn != TurnNumber)
            return false;
        if (Claimed || ManualCardCount != 2 || _playedCards.Count != 2)
            return false;
        if (!MatchesSequence(site))
            return false;

        Claimed = true;
        ClaimedSite = site;
        return true;
    }

    public string Encode()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("Version", Version);
            writer.WriteString("CombatKey", CombatKey);
            writer.WriteNumber("TurnNumber", TurnNumber);
            writer.WriteNumber("ManualCardCount", ManualCardCount);
            writer.WriteStartArray("PlayedCards");
            foreach (var card in _playedCards)
                writer.WriteNumberValue((int)card);
            writer.WriteEndArray();
            writer.WriteBoolean("Claimed", Claimed);
            writer.WriteNumber("ClaimedSite", (int)ClaimedSite);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static bool TryRestore(string? payload, out NewtonCalibrationPracticeState state)
    {
        state = new NewtonCalibrationPracticeState();
        if (string.IsNullOrEmpty(payload) || payload.Length > MaxPayload)
            return false;

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            int version = 0, turn = 0, count = 0, siteValue = 0;
            string? key = null;
            bool claimed = false;
            List<NewtonCalibrationCardKind>? cards = null;

            foreach (var property in root.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                    return false;

                switch (property.Name)
                {
                    case "Version":
                        if (!TryReadInt(property.Value, out version)) return false;
                        break;
                    case "CombatKey":
                        if (property.Value.ValueKind != JsonValueKind.String) return false;
                        key = property.Value.GetString();
                        break;
                    case "TurnNumber":
                        if (!TryReadInt(property.Value, out turn)) return false;
                        break;
                    case "ManualCardCount":
                        if (!TryReadInt(property.Value, out count)) return false;
                        break;
                    case "PlayedCards":
                        if (property.Value.ValueKind != JsonValueKind.Array) return false;
                        cards = new List<NewtonCalibrationCardKind>();
                        foreach (var element in property.Value.EnumerateArray())
                        {
                            if (!TryReadInt(element, out var cardValue)) return false;
                            var kind = (NewtonCalibrationCardKind)cardValue;
                            if (!IsDefinedKind(kind)) return false;
                            cards.Add(kind);
                        }
                        break;
                    case "Claimed":
                        if (property.Value.ValueKind != JsonValueKind.True &&
                            property.Value.ValueKind != JsonValueKind.False) return false;
                        claimed = property.Value.GetBoolean();
                        break;
                    case "ClaimedSite":
                        if (!TryReadInt(property.Value, out siteValue)) return false;
                        break;
                    default:
                        return false;
                }
            }

            if (seen.Count != 7 || key is null || cards is null)
                return false;
            if (version != CurrentVersion)
                return false;
            if (turn < 0 || count < 0 || count > MaxCards)
                return false;
            if (cards.Count != count)
                return false;
            if (!IsDefinedSite(siteValue))
                return false;

            var site = (NewtonSite)siteValue;

            if (key.Length == 0)
            {
                if (turn != 0 || count != 0 || claimed || site != NewtonSite.None)
                    return false;
            }
            else if (!IsValidKey(key))
            {
                return false;
            }

            if (turn == 0 && (count != 0 || claimed))
                return false;

            if (!claimed)
            {
                if (site != NewtonSite.None)
                    return false;
            }
            else
            {
                if (turn <= 0 || count != 2)
                    return false;
                if (!MatchesSequence(site, cards))
                    return false;
            }

            var restored = new NewtonCalibrationPracticeState
            {
                Version = version,
                CombatKey = key,
                TurnNumber = turn,
                ManualCardCount = count,
                Claimed = claimed,
                ClaimedSite = site,
            };
            restored._playedCards.AddRange(cards);
            state = restored;
            return true;
        }
        catch (JsonException)
        {
            state = new NewtonCalibrationPracticeState();
            return false;
        }
    }

    private void ResetForCombat(string combatKey)
    {
        CombatKey = combatKey;
        TurnNumber = 0;
        ManualCardCount = 0;
        _playedCards.Clear();
        Claimed = false;
        ClaimedSite = NewtonSite.None;
    }

    private bool MatchesSequence(NewtonSite site) => MatchesSequence(site, _playedCards);

    private static bool MatchesSequence(NewtonSite site, IReadOnlyList<NewtonCalibrationCardKind> cards)
    {
        if (cards.Count != 2)
            return false;

        return site switch
        {
            NewtonSite.Drop => cards[0] == NewtonCalibrationCardKind.Attack &&
                               cards[1] == NewtonCalibrationCardKind.Skill,
            NewtonSite.Tide => cards[0] == NewtonCalibrationCardKind.Skill &&
                               cards[1] == NewtonCalibrationCardKind.Attack,
            _ => false,
        };
    }

    private static bool IsValidKey(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
            return false;

        foreach (var c in key)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                  (c >= '0' && c <= '9') || c == ':' || c == '-'))
                return false;
        }

        return true;
    }

    private static bool IsDefinedKind(NewtonCalibrationCardKind kind) =>
        kind is NewtonCalibrationCardKind.Other or
                NewtonCalibrationCardKind.Attack or
                NewtonCalibrationCardKind.Skill;

    private static bool IsDefinedSite(int value) =>
        value is (int)NewtonSite.None or (int)NewtonSite.Drop or (int)NewtonSite.Tide;

    private static bool TryReadInt(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Number)
            return false;
        return element.TryGetInt32(out value);
    }
}
