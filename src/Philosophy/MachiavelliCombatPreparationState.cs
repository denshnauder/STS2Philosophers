using System.Text.Json;

namespace STS2Philosophers;

internal sealed class MachiavelliPreparationReceipt
{
    public bool? Prepared { get; set; }
    public bool? RewardClaimed { get; set; }
}

internal sealed class MachiavelliCombatPreparationState
{
    public int Version { get; set; }
    public Dictionary<string, MachiavelliPreparationReceipt> Receipts { get; set; } = null!;
    public static MachiavelliCombatPreparationState Create() => new() { Version = 1, Receipts = [] };
    private static bool ValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 100
        && key.All(c => char.IsAsciiDigit(c) || c is ':' or '-');
    public bool ObserveFirstManual(string key, bool matches)
    {
        if (!ValidKey(key) || Receipts.Count >= 512 || Receipts.ContainsKey(key)) return false;
        Receipts.Add(key, new() { Prepared = matches, RewardClaimed = false });
        return true;
    }
    public bool TryClaim(string key)
    {
        if (!ValidKey(key) || !Receipts.TryGetValue(key, out var receipt)
            || receipt.Prepared != true || receipt.RewardClaimed != false) return false;
        receipt.RewardClaimed = true;
        return true;
    }
    public int Counter(string key) => !Receipts.TryGetValue(key, out var receipt) ? 2
        : receipt.Prepared == true && receipt.RewardClaimed == false ? 1 : 0;
    public string Encode() => JsonSerializer.Serialize(this);
    public static bool TryRestore(string payload, out MachiavelliCombatPreparationState state)
    {
        state = Create();
        if (string.IsNullOrEmpty(payload)) return true;
        try
        {
            var restored = JsonSerializer.Deserialize<MachiavelliCombatPreparationState>(payload);
            if (restored is not { Version: 1, Receipts: not null } || restored.Receipts.Count > 512
                || restored.Receipts.Any(pair => !ValidKey(pair.Key) || pair.Value is null
                    || !pair.Value.Prepared.HasValue || !pair.Value.RewardClaimed.HasValue
                    || pair.Value.RewardClaimed == true && pair.Value.Prepared != true)) return false;
            state = restored; return true;
        }
        catch (JsonException) { return false; }
    }
}
