namespace STS2Philosophers;

internal sealed record SocratesCardProbeRow(
    object Source, object FirstRecord, object SecondRecord,
    string BeforeFields, string AfterFields, string FirstFields, string SecondFields);

internal static class SocratesCardProbeReport
{
    public static bool Validate(
        IReadOnlyList<object> beforeDeck, IReadOnlyList<object> afterDeck,
        IReadOnlyList<SocratesCardProbeRow> rows, out string reason)
    {
        reason = "";
        if (beforeDeck.Count != afterDeck.Count
            || beforeDeck.Where((card, i) => !ReferenceEquals(card, afterDeck[i])).Any())
        {
            reason = "Deck references changed during the probe.";
            return false;
        }

        var sources = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var records = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var row in rows)
        {
            if (!beforeDeck.Any(card => ReferenceEquals(card, row.Source)) || !sources.Add(row.Source))
            {
                reason = "A source reference is missing or repeated.";
                return false;
            }
            if (!records.Add(row.FirstRecord) || !records.Add(row.SecondRecord))
            {
                reason = "A conversion reused a record reference.";
                return false;
            }
            if (row.BeforeFields != row.AfterFields || row.BeforeFields != row.FirstFields
                || row.BeforeFields != row.SecondFields)
            {
                reason = "Observed card fields changed or conversion fields differ.";
                return false;
            }
        }
        return true;
    }
}
