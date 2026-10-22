using System.Text.Json;

namespace STS2Philosophers;

internal enum BaconComparisonKind { Other, Attack, Skill, Power }

internal sealed class BaconComparisonReceipt
{
    public int TargetTurn { get; set; }
    public BaconComparisonKind FirstKind { get; set; } = (BaconComparisonKind)(-1);
    public BaconComparisonKind SecondKind { get; set; } = (BaconComparisonKind)(-1);
    public int ObservedCount { get; set; }
    public bool? DrawClaimed { get; set; }
}

// Each combat keeps its partial comparison as well as its spent chance. A
// recovery combat-start hook must not replace the first observed card.
internal sealed class BaconCombatComparisonState
{
    public int Version { get; set; }
    public Dictionary<string, BaconComparisonReceipt> Receipts { get; set; } = null!;
    public static BaconCombatComparisonState Create() => new() { Version = 1, Receipts = [] };
    private static bool ValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 100
        && key.All(c => char.IsAsciiDigit(c) || c is ':' or '-');

    public bool ObserveManual(string key, int targetTurn, int turn, BaconComparisonKind kind)
    {
        if (!ValidKey(key) || targetTurn is not (1 or 2) || turn != targetTurn || !Enum.IsDefined(kind))
            return false;
        if (!Receipts.TryGetValue(key, out BaconComparisonReceipt? receipt))
        {
            if (Receipts.Count >= 512) return false;
            Receipts.Add(key, new() { TargetTurn = targetTurn, FirstKind = kind,
                ObservedCount = 1, DrawClaimed = false });
            return false;
        }
        if (receipt.TargetTurn != targetTurn || receipt.ObservedCount != 1) return false;
        receipt.ObservedCount = 2;
        receipt.SecondKind = kind;
        bool matches = receipt.FirstKind != BaconComparisonKind.Other && kind != BaconComparisonKind.Other
            && receipt.FirstKind != kind;
        // Claim before the awaited native draw, so a repeated callback cannot pay
        // again. Matching card effects do not establish a general scientific law.
        receipt.DrawClaimed = matches;
        return matches;
    }

    public int Counter(string key, int targetTurn, int turn)
    {
        if (!ValidKey(key) || targetTurn is not (1 or 2) || turn > targetTurn || turn < 1) return 0;
        if (!Receipts.TryGetValue(key, out BaconComparisonReceipt? receipt))
            return Receipts.Count < 512 ? 2 : 0;
        return receipt.TargetTurn == targetTurn ? 2 - receipt.ObservedCount : 0;
    }

    public string Encode() => JsonSerializer.Serialize(this);
    public static bool TryRestore(string payload, out BaconCombatComparisonState state)
    {
        state = Create();
        if (string.IsNullOrEmpty(payload)) return true;
        try
        {
            BaconCombatComparisonState? restored = JsonSerializer.Deserialize<BaconCombatComparisonState>(payload);
            if (restored is not { Version: 1, Receipts: not null } || restored.Receipts.Count > 512
                || restored.Receipts.Any(pair => !ValidKey(pair.Key) || pair.Value is null
                    || pair.Value.TargetTurn is not (1 or 2) || !Enum.IsDefined(pair.Value.FirstKind)
                    || pair.Value.ObservedCount is not (1 or 2) || !pair.Value.DrawClaimed.HasValue
                    || pair.Value.ObservedCount == 1 && ((int)pair.Value.SecondKind != -1
                        || pair.Value.DrawClaimed != false)
                    || pair.Value.ObservedCount == 2 && (!Enum.IsDefined(pair.Value.SecondKind)
                        || pair.Value.DrawClaimed != (pair.Value.FirstKind != BaconComparisonKind.Other
                            && pair.Value.SecondKind != BaconComparisonKind.Other
                            && pair.Value.FirstKind != pair.Value.SecondKind)))) return false;
            state = restored;
            return true;
        }
        catch (JsonException) { return false; }
    }
}
