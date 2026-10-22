using System.Text.Json;

namespace STS2Philosophers;

// The first manual card is a real per-combat choice: an off-rule card spends
// the chance too. Keep that receipt with the existing civic relic across SL.
internal sealed class AquinasCombatPracticeState
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public string CombatKey { get; set; } = string.Empty;
    public bool FirstManualSeen { get; set; }

    public bool IsValid() => Version == CurrentVersion && CombatKey is not null
        && CombatKey.Length <= 100
        && (CombatKey.Length > 0 || !FirstManualSeen)
        && CombatKey.All(c => char.IsAsciiLetterOrDigit(c) || c is ':' or '-');

    public bool ObserveFirstManual(string combatKey, bool matchesRule)
    {
        if (string.IsNullOrWhiteSpace(combatKey) || combatKey.Length > 100
            || !combatKey.All(c => char.IsAsciiLetterOrDigit(c) || c is ':' or '-')) return false;
        if (!string.Equals(CombatKey, combatKey, StringComparison.Ordinal))
        {
            CombatKey = combatKey;
            FirstManualSeen = false;
        }
        if (FirstManualSeen) return false;
        FirstManualSeen = true;
        return matchesRule;
    }

    public string Encode() => JsonSerializer.Serialize(this);

    public static bool TryRestore(string payload, out AquinasCombatPracticeState state)
    {
        state = new();
        if (string.IsNullOrEmpty(payload)) return true;
        try
        {
            AquinasCombatPracticeState? restored = JsonSerializer.Deserialize<AquinasCombatPracticeState>(payload);
            if (restored is null || !restored.IsValid()) return false;
            state = restored;
            return true;
        }
        catch (JsonException) { return false; }
    }
}
