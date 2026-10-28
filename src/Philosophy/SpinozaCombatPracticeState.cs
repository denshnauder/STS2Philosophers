using System.Text.Json;

namespace STS2Philosophers;

internal enum SpinozaPracticeReward { None, Weak, Draw }

internal sealed class SpinozaCombatPracticeState
{
    public const int CurrentVersion = 1;
    private const int MaxPayload = 4096;
    private const int MaxKeyLength = 128;

    public int Version { get; private set; } = CurrentVersion;
    public string CombatKey { get; private set; } = string.Empty;
    public bool ManualAttackSeen { get; private set; }
    public bool FirstSkillSeen { get; private set; }
    public bool InterventionSpent { get; private set; }
    public bool DrawClaimed { get; private set; }

    public bool BeginCombat(string combatKey)
    {
        if (!IsValidKey(combatKey) || combatKey == CombatKey)
            return false;

        ResetForCombat(combatKey);
        return true;
    }

    public SpinozaPracticeReward Observe(string combatKey, SpinozaDisposition disposition,
        bool isAttack, bool isSkill, bool isSingleEnemyTarget)
    {
        if (string.IsNullOrEmpty(combatKey) || combatKey != CombatKey
            || disposition is not (SpinozaDisposition.Intervened or SpinozaDisposition.Reordered)
            || isAttack && isSkill)
            return SpinozaPracticeReward.None;

        if (isAttack)
            ManualAttackSeen = true;

        switch (disposition)
        {
            case SpinozaDisposition.Intervened:
                if (!isAttack || !isSingleEnemyTarget || InterventionSpent)
                    return SpinozaPracticeReward.None;
                InterventionSpent = true;
                return SpinozaPracticeReward.Weak;
            case SpinozaDisposition.Reordered:
                if (!isSkill || FirstSkillSeen)
                    return SpinozaPracticeReward.None;
                FirstSkillSeen = true;
                if (ManualAttackSeen || DrawClaimed)
                    return SpinozaPracticeReward.None;
                DrawClaimed = true;
                return SpinozaPracticeReward.Draw;
            default:
                return SpinozaPracticeReward.None;
        }
    }

    public string Encode()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("Version", Version);
            writer.WriteString("CombatKey", CombatKey);
            writer.WriteBoolean("ManualAttackSeen", ManualAttackSeen);
            writer.WriteBoolean("FirstSkillSeen", FirstSkillSeen);
            writer.WriteBoolean("InterventionSpent", InterventionSpent);
            writer.WriteBoolean("DrawClaimed", DrawClaimed);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static bool TryRestore(string? payload, out SpinozaCombatPracticeState state)
    {
        state = new SpinozaCombatPracticeState();
        if (string.IsNullOrEmpty(payload) || payload.Length > MaxPayload)
            return false;

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            int version = 0;
            string? key = null;
            bool attack = false, skill = false, spent = false, draw = false;

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
                    case "ManualAttackSeen":
                        if (!TryReadBool(property.Value, out attack)) return false;
                        break;
                    case "FirstSkillSeen":
                        if (!TryReadBool(property.Value, out skill)) return false;
                        break;
                    case "InterventionSpent":
                        if (!TryReadBool(property.Value, out spent)) return false;
                        break;
                    case "DrawClaimed":
                        if (!TryReadBool(property.Value, out draw)) return false;
                        break;
                    default:
                        return false;
                }
            }

            if (seen.Count != 6 || key is null)
                return false;
            if (version != CurrentVersion)
                return false;

            if (key.Length == 0)
            {
                if (attack || skill || spent || draw)
                    return false;
            }
            else if (!IsValidKey(key))
            {
                return false;
            }

            if (draw && !skill)
                return false;
            if (spent && (!attack || skill || draw) || skill && !draw && !attack)
                return false;

            state = new SpinozaCombatPracticeState
            {
                Version = version,
                CombatKey = key,
                ManualAttackSeen = attack,
                FirstSkillSeen = skill,
                InterventionSpent = spent,
                DrawClaimed = draw,
            };
            return true;
        }
        catch (JsonException)
        {
            state = new SpinozaCombatPracticeState();
            return false;
        }
    }

    private void ResetForCombat(string combatKey)
    {
        CombatKey = combatKey;
        ManualAttackSeen = false;
        FirstSkillSeen = false;
        InterventionSpent = false;
        DrawClaimed = false;
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

    private static bool TryReadInt(JsonElement element, out int value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Number)
            return false;
        return element.TryGetInt32(out value);
    }

    private static bool TryReadBool(JsonElement element, out bool value)
    {
        value = false;
        if (element.ValueKind != JsonValueKind.True &&
            element.ValueKind != JsonValueKind.False)
            return false;
        value = element.GetBoolean();
        return true;
    }
}
