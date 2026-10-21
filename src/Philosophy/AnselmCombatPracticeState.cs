using System.Text.Json;

namespace STS2Philosophers;

internal sealed class AnselmCombatPracticeState
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

    public static bool TryRestore(string payload, out AnselmCombatPracticeState state)
    {
        state = new();
        if (string.IsNullOrEmpty(payload)) return true;
        try
        {
            AnselmCombatPracticeState? restored = JsonSerializer.Deserialize<AnselmCombatPracticeState>(payload);
            if (restored is null || !restored.IsValid()) return false;
            state = restored;
            return true;
        }
        catch (JsonException) { return false; }
    }
}
