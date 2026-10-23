using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum ZenoCivicUpgradeBranch { None, Public, Conditional }

// The actual seal owns both its one event grant and its spent combat/turn claims.
// An absent payload is an unimproved legacy seal, never permission to infer a grant.
internal sealed class ZenoCivicSealUpgradeState
{
    [JsonRequired] public int Version { get; set; }
    [JsonRequired] public string EventInstanceId { get; set; } = null!;
    [JsonRequired] public ZenoCivicUpgradeBranch Branch { get; set; }
    [JsonRequired] public Dictionary<string, List<int>> ClaimedTurns { get; set; } = null!;

    public static ZenoCivicSealUpgradeState Create(string instanceId, ZenoCivicUpgradeBranch branch) => new()
    {
        Version = 1, EventInstanceId = instanceId, Branch = branch, ClaimedTurns = [],
    };

    private static bool ValidCombatKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 80) return false;
        string[] parts = key.Split(':');
        return parts.Length == 3 && parts.All(part => int.TryParse(part, NumberStyles.None,
            CultureInfo.InvariantCulture, out int value) && value >= 0);
    }

    public bool IsUsable() => Version == 1 && !string.IsNullOrWhiteSpace(EventInstanceId)
        && EventInstanceId.Length <= 250 && Branch is ZenoCivicUpgradeBranch.Public or ZenoCivicUpgradeBranch.Conditional
        && ClaimedTurns is not null && ClaimedTurns.Count <= 512
        && ClaimedTurns.All(pair => ValidCombatKey(pair.Key) && pair.Value is { Count: > 0 and <= 1024 }
            && pair.Value.All(turn => turn > 0) && pair.Value.Distinct().Count() == pair.Value.Count
            && pair.Value.SequenceEqual(pair.Value.Order()));

    public bool Matches(string instanceId, ZenoCivicUpgradeBranch branch) => IsUsable()
        && EventInstanceId == instanceId && Branch == branch;

    public bool TryClaim(string combatKey, int turn)
    {
        if (!IsUsable() || !ValidCombatKey(combatKey) || turn <= 0) return false;
        if (!ClaimedTurns.TryGetValue(combatKey, out List<int>? turns))
        {
            if (ClaimedTurns.Count >= 512) return false;
            ClaimedTurns.Add(combatKey, [turn]);
            return true;
        }
        if (turns.Count >= 1024 || turn <= turns[^1]) return false;
        turns.Add(turn);
        return true;
    }

    public string Encode() => IsUsable() ? JsonSerializer.Serialize(this)
        : throw new InvalidDataException("The civic seal's grant or turn receipt is invalid.");

    public static bool TryRestore(string payload, out ZenoCivicSealUpgradeState? state)
    {
        state = null;
        if (string.IsNullOrEmpty(payload)) return true;
        try
        {
            var restored = JsonSerializer.Deserialize<ZenoCivicSealUpgradeState>(payload);
            if (restored?.IsUsable() != true) return false;
            state = restored;
            return true;
        }
        catch (JsonException) { return false; }
    }
}
