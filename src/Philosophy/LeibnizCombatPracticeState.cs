using System.Text.Json;

namespace STS2Philosophers;

internal enum LeibnizPracticeCardKind { Other, Attack, Skill }

internal enum LeibnizPracticeReward { None, Block4, Draw1 }

internal sealed class LeibnizCombatPracticeState
{
    public const int MaxCombatKeyLength = 128;

    public int Version { get; set; }
    public string CombatKey { get; set; } = "";
    public int TurnNumber { get; set; }
    public int AttackCount { get; set; }
    public int SkillCount { get; set; }
    public bool FirstTurnAttack { get; set; }
    public bool FirstTurnSkill { get; set; }
    public bool BlockClaimed { get; set; }
    public bool DrawClaimed { get; set; }
    public bool EnergyClaimed { get; set; }

    public static LeibnizCombatPracticeState CreateDefault() => new()
    {
        Version = 1,
        CombatKey = "",
        TurnNumber = 0,
        AttackCount = 0,
        SkillCount = 0,
        FirstTurnAttack = false,
        FirstTurnSkill = false,
        BlockClaimed = false,
        DrawClaimed = false,
        EnergyClaimed = false
    };

    public bool IsCleanDefault() =>
        Version == 1 && CombatKey.Length == 0 && TurnNumber == 0
        && AttackCount == 0 && SkillCount == 0
        && !FirstTurnAttack && !FirstTurnSkill
        && !BlockClaimed && !DrawClaimed && !EnergyClaimed;

    public bool BeginCombat(string combatKey)
    {
        if (!IsValidKey(combatKey)) return false;
        if (string.Equals(CombatKey, combatKey, StringComparison.Ordinal)) return false;
        Version = 1;
        CombatKey = combatKey;
        TurnNumber = 0;
        AttackCount = 0;
        SkillCount = 0;
        FirstTurnAttack = false;
        FirstTurnSkill = false;
        BlockClaimed = false;
        DrawClaimed = false;
        EnergyClaimed = false;
        return true;
    }

    public bool StartTurn(string combatKey, int turnNumber)
    {
        if (!IsValidKey(combatKey) || turnNumber <= 0) return false;
        if (!string.Equals(CombatKey, combatKey, StringComparison.Ordinal)) return false;
        if (turnNumber <= TurnNumber) return false;
        TurnNumber = turnNumber;
        return true;
    }

    public LeibnizPracticeReward ObserveManual(string combatKey, int turnNumber, LeibnizDisposition disposition, LeibnizPracticeCardKind kind)
    {
        if (!IsValidKey(combatKey) || turnNumber <= 0) return LeibnizPracticeReward.None;
        if (!string.Equals(CombatKey, combatKey, StringComparison.Ordinal)) return LeibnizPracticeReward.None;
        if (turnNumber != TurnNumber) return LeibnizPracticeReward.None;
        if (kind is not (LeibnizPracticeCardKind.Attack or LeibnizPracticeCardKind.Skill)) return LeibnizPracticeReward.None;

        if (disposition == LeibnizDisposition.Independent)
        {
            if (kind == LeibnizPracticeCardKind.Attack)
            {
                if (AttackCount >= 2) return LeibnizPracticeReward.None;
                AttackCount++;
                if (AttackCount == 2 && !BlockClaimed)
                {
                    BlockClaimed = true;
                    return LeibnizPracticeReward.Block4;
                }
                return LeibnizPracticeReward.None;
            }
            if (SkillCount >= 2) return LeibnizPracticeReward.None;
            SkillCount++;
            if (SkillCount == 2 && !DrawClaimed)
            {
                DrawClaimed = true;
                return LeibnizPracticeReward.Draw1;
            }
            return LeibnizPracticeReward.None;
        }

        if (disposition == LeibnizDisposition.Coordinated && turnNumber == 1)
        {
            if (kind == LeibnizPracticeCardKind.Attack) FirstTurnAttack = true;
            else FirstTurnSkill = true;
        }
        return LeibnizPracticeReward.None;
    }

    public bool TryClaimEnergy(string combatKey, int turnNumber, LeibnizDisposition disposition)
    {
        if (disposition != LeibnizDisposition.Coordinated) return false;
        if (!IsValidKey(combatKey) || turnNumber != 2) return false;
        if (!string.Equals(CombatKey, combatKey, StringComparison.Ordinal)) return false;
        if (turnNumber != TurnNumber) return false;
        if (!FirstTurnAttack || !FirstTurnSkill) return false;
        if (EnergyClaimed) return false;
        EnergyClaimed = true;
        return true;
    }

    public string Encode() => JsonSerializer.Serialize(new Payload
    {
        Version = Version,
        CombatKey = CombatKey,
        TurnNumber = TurnNumber,
        AttackCount = AttackCount,
        SkillCount = SkillCount,
        FirstTurnAttack = FirstTurnAttack,
        FirstTurnSkill = FirstTurnSkill,
        BlockClaimed = BlockClaimed,
        DrawClaimed = DrawClaimed,
        EnergyClaimed = EnergyClaimed
    });

    public static bool TryRestore(string? json, out LeibnizCombatPracticeState state)
    {
        state = CreateDefault();
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int version = 0;
            string combatKey = "";
            int turnNumber = 0, attackCount = 0, skillCount = 0;
            bool firstTurnAttack = false, firstTurnSkill = false;
            bool blockClaimed = false, drawClaimed = false, energyClaimed = false;

            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return false;
                switch (property.Name)
                {
                    case "Version":
                        if (!TryReadInt(property.Value, out version)) return false;
                        break;
                    case "CombatKey":
                        if (property.Value.ValueKind != JsonValueKind.String) return false;
                        combatKey = property.Value.GetString() ?? "";
                        break;
                    case "TurnNumber":
                        if (!TryReadInt(property.Value, out turnNumber)) return false;
                        break;
                    case "AttackCount":
                        if (!TryReadInt(property.Value, out attackCount)) return false;
                        break;
                    case "SkillCount":
                        if (!TryReadInt(property.Value, out skillCount)) return false;
                        break;
                    case "FirstTurnAttack":
                        if (!TryReadBool(property.Value, out firstTurnAttack)) return false;
                        break;
                    case "FirstTurnSkill":
                        if (!TryReadBool(property.Value, out firstTurnSkill)) return false;
                        break;
                    case "BlockClaimed":
                        if (!TryReadBool(property.Value, out blockClaimed)) return false;
                        break;
                    case "DrawClaimed":
                        if (!TryReadBool(property.Value, out drawClaimed)) return false;
                        break;
                    case "EnergyClaimed":
                        if (!TryReadBool(property.Value, out energyClaimed)) return false;
                        break;
                    default:
                        return false;
                }
            }

            if (seen.Count != 10) return false;
            if (version != 1) return false;
            if (combatKey.Length > MaxCombatKeyLength) return false;
            if (combatKey.Length == 0)
            {
                if (turnNumber != 0 || attackCount != 0 || skillCount != 0
                    || firstTurnAttack || firstTurnSkill
                    || blockClaimed || drawClaimed || energyClaimed) return false;
            }
            else
            {
                if (turnNumber < 0) return false;
                if (turnNumber == 0
                    && (attackCount != 0 || skillCount != 0
                        || firstTurnAttack || firstTurnSkill
                        || blockClaimed || drawClaimed || energyClaimed)) return false;
            }
            if (attackCount is < 0 or > 2) return false;
            if (skillCount is < 0 or > 2) return false;
            if (blockClaimed != (attackCount == 2)) return false;
            if (drawClaimed != (skillCount == 2)) return false;
            if (firstTurnAttack || firstTurnSkill)
            {
                if (turnNumber < 1 || attackCount != 0 || skillCount != 0
                    || blockClaimed || drawClaimed) return false;
            }
            if (energyClaimed)
            {
                if (!firstTurnAttack || !firstTurnSkill) return false;
                if (turnNumber < 2) return false;
            }

            state = new LeibnizCombatPracticeState
            {
                Version = version,
                CombatKey = combatKey,
                TurnNumber = turnNumber,
                AttackCount = attackCount,
                SkillCount = skillCount,
                FirstTurnAttack = firstTurnAttack,
                FirstTurnSkill = firstTurnSkill,
                BlockClaimed = blockClaimed,
                DrawClaimed = drawClaimed,
                EnergyClaimed = energyClaimed
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsValidKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length <= MaxCombatKeyLength;

    private static bool TryReadInt(JsonElement element, out int value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static bool TryReadBool(JsonElement element, out bool value)
    {
        value = false;
        if (element.ValueKind == JsonValueKind.True) { value = true; return true; }
        if (element.ValueKind == JsonValueKind.False) { value = false; return true; }
        return false;
    }

    private sealed class Payload
    {
        public int Version { get; set; }
        public string CombatKey { get; set; } = "";
        public int TurnNumber { get; set; }
        public int AttackCount { get; set; }
        public int SkillCount { get; set; }
        public bool FirstTurnAttack { get; set; }
        public bool FirstTurnSkill { get; set; }
        public bool BlockClaimed { get; set; }
        public bool DrawClaimed { get; set; }
        public bool EnergyClaimed { get; set; }
    }
}
