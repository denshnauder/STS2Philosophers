using System.Text.Json;

namespace STS2Philosophers;

internal sealed record SocratesCommitmentSave(int Version, string CombatId, int Turn,
    SocratesCommitmentPhase Phase, SocratesCommitmentMode Mode, bool Declined, bool DecisionMade,
    bool Revised, int? PublicIncoming, int InitialBlock, string[] Attackers, int PendingDraw,
    SocratesCommitmentObservation Observation);

internal static class SocratesBoundedCommitmentStateCodec
{
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "Version", "CombatId", "Turn", "Phase", "Mode", "Declined", "DecisionMade", "Revised",
        "PublicIncoming", "InitialBlock", "Attackers", "PendingDraw", "Observation"
    };

    public static string Serialize(SocratesBoundedCommitmentState state) => JsonSerializer.Serialize(state.Capture());

    public static bool TryRestore(string? payload, string expectedCombatId, out SocratesBoundedCommitmentState? state)
    {
        state = null;
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(expectedCombatId)) return false;
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (JsonProperty field in document.RootElement.EnumerateObject())
                if (!Fields.Contains(field.Name) || !seen.Add(field.Name)) return false;
            if (!seen.SetEquals(Fields)) return false;
            SocratesCommitmentSave? save = document.RootElement.Deserialize<SocratesCommitmentSave>();
            return save is not null && SocratesBoundedCommitmentState.TryRestore(save, expectedCombatId, out state);
        }
        catch (JsonException) { return false; }
    }
}

internal sealed partial class SocratesBoundedCommitmentState
{
    internal SocratesCommitmentSave Capture() => new(1, CombatId, Turn, Phase, Mode, Declined,
        DecisionMade, Revised, PublicIncoming, InitialBlock, FrozenAttackers.ToArray(), _pendingDraw, Observation);

    internal static bool TryRestore(SocratesCommitmentSave d, string expectedCombatId,
        out SocratesBoundedCommitmentState? state)
    {
        state = null;
        if (d.Version != 1 || string.IsNullOrWhiteSpace(d.CombatId) || d.CombatId != expectedCombatId ||
            d.Turn < 0 || d.InitialBlock < 0 || d.PublicIncoming < 0 || d.PendingDraw is < 0 or > 1 ||
            !Enum.IsDefined(d.Phase) || !Enum.IsDefined(d.Mode) || !Enum.IsDefined(d.Observation) ||
            d.Attackers is null || d.Attackers.Any(string.IsNullOrWhiteSpace) ||
            d.Attackers.Distinct(StringComparer.Ordinal).Count() != d.Attackers.Length ||
            (d.Revised && (!d.DecisionMade || d.Mode == SocratesCommitmentMode.None)) ||
            (d.Declined && (d.Mode != SocratesCommitmentMode.None || d.Revised || d.PendingDraw != 0)) ||
            (d.DecisionMade && d.Mode == SocratesCommitmentMode.None && !d.Declined)) return false;

        if (d.Phase == SocratesCommitmentPhase.AwaitingTurn)
        {
            if (d.Turn != 0 || d.Mode != SocratesCommitmentMode.None || d.Declined || d.DecisionMade ||
                d.PublicIncoming is not null || d.InitialBlock != 0 || d.Attackers.Length != 0 ||
                d.PendingDraw != 0 || d.Observation != SocratesCommitmentObservation.NotObserved) return false;
        }
        else if (d.Phase != SocratesCommitmentPhase.Ended && d.Turn == 0) return false;

        if (d.Phase != SocratesCommitmentPhase.Ended && d.PublicIncoming > 0 && d.Attackers.Length == 0) return false;
        if (d.Phase == SocratesCommitmentPhase.Opening && (d.DecisionMade || d.Revised ||
            d.PublicIncoming is not null || d.InitialBlock != 0 || d.Attackers.Length != 0 ||
            d.Observation != SocratesCommitmentObservation.NotObserved)) return false;
        if (d.Phase is SocratesCommitmentPhase.Choosing or SocratesCommitmentPhase.Acting &&
            (d.PendingDraw != 0 || d.Observation != SocratesCommitmentObservation.NotObserved)) return false;
        if (d.Phase == SocratesCommitmentPhase.Acting && d.Mode == SocratesCommitmentMode.None &&
            !d.Declined && d.Attackers.Length > 0) return false;
        if (d.PendingDraw > 0 && (d.Mode == SocratesCommitmentMode.None ||
            d.Phase is not (SocratesCommitmentPhase.Opening or SocratesCommitmentPhase.Closed))) return false;
        if (d.Phase == SocratesCommitmentPhase.Ended && (d.PendingDraw != 0 || d.Attackers.Length != 0)) return false;

        if (d.Phase == SocratesCommitmentPhase.Closed)
        {
            SocratesCommitmentObservation? required = d.Mode switch
            {
                SocratesCommitmentMode.None => SocratesCommitmentObservation.NoPractice,
                SocratesCommitmentMode.Guard when d.PublicIncoming is null => SocratesCommitmentObservation.Incomplete,
                SocratesCommitmentMode.Guard when d.PublicIncoming / 2 + d.PublicIncoming % 2 <= d.InitialBlock => SocratesCommitmentObservation.NoCondition,
                SocratesCommitmentMode.Clear when d.Attackers.Length == 0 => SocratesCommitmentObservation.NoCondition,
                _ => null
            };
            if (required is not null ? d.Observation != required :
                d.Observation is not (SocratesCommitmentObservation.Met or SocratesCommitmentObservation.Unmet or SocratesCommitmentObservation.Incomplete)) return false;
            if (d.Mode == SocratesCommitmentMode.Guard && required is null &&
                d.Observation == SocratesCommitmentObservation.Incomplete) return false;
            int expectedDraw = !d.Revised && d.Observation == SocratesCommitmentObservation.Met ? 1 : 0;
            if (d.PendingDraw != expectedDraw) return false;
        }

        state = new(d.CombatId)
        {
            Turn = d.Turn, Phase = d.Phase, Mode = d.Mode, Declined = d.Declined,
            DecisionMade = d.DecisionMade, Revised = d.Revised, PublicIncoming = d.PublicIncoming,
            InitialBlock = d.InitialBlock, _attackers = new(d.Attackers, StringComparer.Ordinal),
            _pendingDraw = d.PendingDraw, Observation = d.Observation
        };
        return true;
    }
}
