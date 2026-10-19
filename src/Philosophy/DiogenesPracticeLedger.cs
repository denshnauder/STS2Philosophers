namespace STS2Philosophers;

// Records observable choices at the existing Socrates virtue opening. It never
// infers motive, success, or a historical encounter from a skipped selection.
internal sealed record DiogenesPracticeObservation(
    int ActIndex,
    int RoomId,
    int EligibleCardCount,
    int SelectedCardCount);

internal sealed class DiogenesPracticeLedger
{
    public List<DiogenesPracticeObservation> Observations { get; set; } = [];

    public bool IsValid() => Observations is not null && Observations.Count <= 128
        && Observations.All(item => item is { ActIndex: >= 0 and <= 1, RoomId: >= 0,
            EligibleCardCount: > 0, SelectedCardCount: >= 0 and <= 1 }
            && item.SelectedCardCount <= item.EligibleCardCount)
        && Observations.Select(item => (item.ActIndex, item.RoomId)).Distinct().Count() == Observations.Count;

    public bool TryRecord(int actIndex, int roomId, int eligibleCardCount, int selectedCardCount)
    {
        if (!IsValid()) return false;
        DiogenesPracticeObservation next = new(actIndex, roomId, eligibleCardCount, selectedCardCount);
        if (next is not { ActIndex: >= 0 and <= 1, RoomId: >= 0,
            EligibleCardCount: > 0, SelectedCardCount: >= 0 and <= 1 }
            || selectedCardCount > eligibleCardCount) return false;
        DiogenesPracticeObservation? existing = Observations.FirstOrDefault(item =>
            item.ActIndex == actIndex && item.RoomId == roomId);
        if (existing is not null) return existing == next;
        if (Observations.Count == 128) return false;
        Observations.Add(next);
        return true;
    }

    public (int Eligible, int Foregone, int Used) SummaryBefore(int actIndex) =>
        (Observations.Count(item => item.ActIndex < actIndex),
            Observations.Count(item => item.ActIndex < actIndex && item.SelectedCardCount == 0),
            Observations.Count(item => item.ActIndex < actIndex && item.SelectedCardCount == 1));
}
