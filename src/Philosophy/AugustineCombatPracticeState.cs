using System.Text.Json;

namespace STS2Philosophers;

// The event's public/private work result lives in Ritsu run state. The relic only
// remembers the native combat identity already paid, including after save/load.
internal sealed class AugustineCombatPracticeState
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public string LastGrantedCombatKey { get; set; } = string.Empty;

    public bool IsValid() => Version == CurrentVersion && LastGrantedCombatKey is not null
        && LastGrantedCombatKey.Length <= 100
        && LastGrantedCombatKey.All(c => char.IsAsciiLetterOrDigit(c) || c is ':' or '-');

    public bool TryClaim(string combatKey)
    {
        if (string.IsNullOrWhiteSpace(combatKey) || combatKey.Length > 100
            || string.Equals(LastGrantedCombatKey, combatKey, StringComparison.Ordinal)) return false;
        LastGrantedCombatKey = combatKey;
        return true;
    }

    public string Encode() => JsonSerializer.Serialize(this);

    public static bool TryRestore(string payload, out AugustineCombatPracticeState state)
    {
        state = new();
        if (string.IsNullOrEmpty(payload)) return true;
        try
        {
            AugustineCombatPracticeState? restored = JsonSerializer.Deserialize<AugustineCombatPracticeState>(payload);
            if (restored is null || !restored.IsValid()) return false;
            state = restored;
            return true;
        }
        catch (JsonException) { return false; }
    }
}
