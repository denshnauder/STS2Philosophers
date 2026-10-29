using System.Text.Json;

namespace STS2Philosophers;

internal enum HegelPracticeCardKind { Other, Attack, Skill }

internal enum HegelPracticeReward { None, DrawAndBlock }

// Hegel's successor exercise is combat-scoped, like the Spinoza and Leibniz
// practices carried by the same relic: the last four manual card kinds must
// read Attack, Skill, Attack, Skill, and the reward is paid once per combat.
internal sealed class HegelCombatPracticeState
{
    public const int MaxCombatKeyLength = 128;
    public const int WindowLength = 4;

    public int Version { get; set; }
    public string CombatKey { get; set; } = "";
    public List<HegelPracticeCardKind> Window { get; set; } = [];
    public bool Claimed { get; set; }

    public static HegelCombatPracticeState CreateDefault() => new()
    {
        Version = 1,
        CombatKey = "",
        Window = [],
        Claimed = false
    };

    public bool IsCleanDefault() => Version == 1 && CombatKey.Length == 0
        && Window.Count == 0 && !Claimed;

    public bool BeginCombat(string combatKey)
    {
        if (!IsValidKey(combatKey)) return false;
        if (string.Equals(CombatKey, combatKey, StringComparison.Ordinal)) return false;
        Version = 1;
        CombatKey = combatKey;
        Window = [];
        Claimed = false;
        return true;
    }

    public HegelPracticeReward ObserveManual(string combatKey, HegelPracticeCardKind kind)
    {
        if (!IsValidKey(combatKey) || !string.Equals(CombatKey, combatKey, StringComparison.Ordinal))
            return HegelPracticeReward.None;
        if (!Enum.IsDefined(kind)) return HegelPracticeReward.None;
        Window.Add(kind);
        if (Window.Count > WindowLength) Window.RemoveAt(0);
        if (Claimed || Window.Count != WindowLength
            || Window[0] != HegelPracticeCardKind.Attack
            || Window[1] != HegelPracticeCardKind.Skill
            || Window[2] != HegelPracticeCardKind.Attack
            || Window[3] != HegelPracticeCardKind.Skill) return HegelPracticeReward.None;
        Claimed = true;
        return HegelPracticeReward.DrawAndBlock;
    }

    public string Encode() => JsonSerializer.Serialize(new Payload
    {
        Version = Version,
        CombatKey = CombatKey,
        Window = Window.Select(kind => kind.ToString()).ToList(),
        Claimed = Claimed
    });

    public static bool TryRestore(string? json, out HegelCombatPracticeState state)
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
            var window = new List<HegelPracticeCardKind>();
            bool claimed = false;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return false;
                switch (property.Name)
                {
                    case "Version":
                        if (property.Value.ValueKind != JsonValueKind.Number
                            || !property.Value.TryGetInt32(out version)) return false;
                        break;
                    case "CombatKey":
                        if (property.Value.ValueKind != JsonValueKind.String) return false;
                        combatKey = property.Value.GetString() ?? "";
                        break;
                    case "Window":
                        if (property.Value.ValueKind != JsonValueKind.Array) return false;
                        foreach (JsonElement element in property.Value.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.String) return false;
                            if (!Enum.TryParse(element.GetString(), ignoreCase: false,
                                    out HegelPracticeCardKind kind) || !Enum.IsDefined(kind)) return false;
                            window.Add(kind);
                        }
                        break;
                    case "Claimed":
                        if (property.Value.ValueKind == JsonValueKind.True) claimed = true;
                        else if (property.Value.ValueKind != JsonValueKind.False) return false;
                        break;
                    default:
                        return false;
                }
            }
            if (seen.Count != 4 || version != 1) return false;
            if (combatKey.Length > MaxCombatKeyLength) return false;
            if (window.Count > WindowLength) return false;
            if (combatKey.Length == 0 && (window.Count != 0 || claimed)) return false;
            if (claimed && window.Count == 0) return false;
            state = new HegelCombatPracticeState
            {
                Version = version,
                CombatKey = combatKey,
                Window = window,
                Claimed = claimed
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

    private sealed class Payload
    {
        public int Version { get; set; }
        public string CombatKey { get; set; } = "";
        public List<string> Window { get; set; } = [];
        public bool Claimed { get; set; }
    }
}
