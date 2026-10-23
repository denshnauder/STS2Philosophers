using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace STS2Philosophers;

// Measurements are actual native EnergySpent values, not a card's quoted cost.
// Completed combat identities remain recorded so revisiting a prior key cannot reset a claim.
internal sealed class GalileoRepeatedPaymentState
{
    [JsonRequired] public int Version { get; set; }
    [JsonRequired] public string CombatKey { get; set; } = null!;
    [JsonRequired] public string SampleModelId { get; set; } = null!;
    [JsonRequired] public decimal SamplePaid { get; set; }
    [JsonRequired] public int SampleTurn { get; set; }
    [JsonRequired] public bool Claimed { get; set; }
    [JsonRequired] public List<string> PreviousCombatKeys { get; set; } = null!;

    public static GalileoRepeatedPaymentState Create() => new()
    {
        Version = 1, CombatKey = string.Empty, SampleModelId = string.Empty, PreviousCombatKeys = [],
    };

    private static bool ValidCombatKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 80) return false;
        string[] parts = key.Split(':');
        return parts.Length == 3 && parts.All(part => int.TryParse(part, NumberStyles.None,
            CultureInfo.InvariantCulture, out int value) && value >= 0);
    }
    private static bool ValidModelId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 200
        && id.StartsWith("CARD.", StringComparison.Ordinal) && id.Length > 5
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.');

    public bool IsUsable()
    {
        if (Version != 1 || CombatKey is null || SampleModelId is null || PreviousCombatKeys is null
            || PreviousCombatKeys.Count > 511 || PreviousCombatKeys.Any(key => !ValidCombatKey(key))
            || PreviousCombatKeys.Distinct(StringComparer.Ordinal).Count() != PreviousCombatKeys.Count
            || PreviousCombatKeys.Contains(CombatKey, StringComparer.Ordinal)) return false;
        if (CombatKey.Length == 0)
            return PreviousCombatKeys.Count == 0 && SampleModelId.Length == 0 && SamplePaid == 0 && SampleTurn == 0 && !Claimed;
        return ValidCombatKey(CombatKey) && (SampleModelId.Length == 0
            ? SamplePaid == 0 && SampleTurn == 0 && !Claimed
            : ValidModelId(SampleModelId) && SamplePaid > 0 && SampleTurn > 0);
    }

    public bool BeginCombat(string combatKey)
    {
        if (!IsUsable() || !ValidCombatKey(combatKey)) return false;
        if (CombatKey == combatKey) return true;
        if (PreviousCombatKeys.Contains(combatKey, StringComparer.Ordinal)
            || CombatKey.Length > 0 && PreviousCombatKeys.Count >= 511) return false;
        if (CombatKey.Length > 0) PreviousCombatKeys.Add(CombatKey);
        CombatKey = combatKey; SampleModelId = string.Empty; SamplePaid = 0; SampleTurn = 0; Claimed = false;
        return true;
    }

    public bool ObservePayment(string combatKey, string modelId, decimal energySpent, int turn,
        bool isManual, bool costsX, bool isAttackOrSkill)
    {
        if (!IsUsable() || CombatKey != combatKey || CombatKey.Length == 0 || Claimed || SampleModelId.Length != 0
            || !isManual || costsX || !isAttackOrSkill || !ValidModelId(modelId) || energySpent <= 0 || turn <= 0) return false;
        SampleModelId = modelId; SamplePaid = energySpent; SampleTurn = turn;
        return true;
    }

    public bool TryClaim(string combatKey, string modelId, decimal energySpent, int turn,
        bool isManual, bool costsX, bool actualReleaseExhausted)
    {
        if (!IsUsable() || CombatKey.Length == 0 || CombatKey != combatKey || Claimed || SampleModelId.Length == 0
            || !isManual || costsX || !actualReleaseExhausted || !ValidModelId(modelId) || energySpent <= 0
            || SampleModelId != modelId || SamplePaid != energySpent || turn <= SampleTurn) return false;
        Claimed = true; // The caller must claim before awaiting the native draw.
        return true;
    }

    public string Encode() => IsUsable() ? JsonSerializer.Serialize(this)
        : throw new InvalidDataException("The Galileo payment sample or combat receipt is invalid.");

    public static bool TryRestore(string payload, out GalileoRepeatedPaymentState state)
    {
        state = Create();
        if (string.IsNullOrEmpty(payload)) return true;
        try
        {
            var restored = JsonSerializer.Deserialize<GalileoRepeatedPaymentState>(payload);
            if (restored?.IsUsable() != true) return false;
            state = restored; return true;
        }
        catch (JsonException) { return false; }
    }
}
