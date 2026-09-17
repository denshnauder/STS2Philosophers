using STS2Philosophers;

internal static class SocratesCardProbeChecks
{
    private sealed record EqualCard(string Name);

    public static void Run()
    {
        object a = new EqualCard("same"), b = new EqualCard("same"), clone = new EqualCard("same");
        object[] deck = [a, b];
        SocratesCardProbeRow first = new(a, new object(), new object(), "same", "same", "same", "same");
        SocratesCardProbeRow second = new(b, new object(), new object(), "same", "same", "same", "same");
        Check(SocratesCardProbeReport.Validate(deck, deck, [first, second], out _), "Matching fields must not merge distinct references.");
        Check(!SocratesCardProbeReport.Validate(deck, [b, a], [first, second], out _), "A reordered deck invalidates the evidence.");
        Check(!SocratesCardProbeReport.Validate(deck, [a, clone], [first, second], out _), "An equal clone is not the original card.");
        Check(!SocratesCardProbeReport.Validate(deck, [a], [first], out _), "A changed deck count invalidates the evidence.");
        Check(!SocratesCardProbeReport.Validate(deck, deck, [first, first], out _), "Repeated source references are invalid.");
        Check(!SocratesCardProbeReport.Validate(deck, deck, [first with { Source = clone }], out _), "A matching card outside the deck is invalid.");
        Check(!SocratesCardProbeReport.Validate(deck, deck, [first with { SecondRecord = first.FirstRecord }], out _), "Repeated conversion output is invalid.");
        Check(!SocratesCardProbeReport.Validate(deck, deck, [first, second with { FirstRecord = first.FirstRecord }], out _), "Shared records across distinct sources are invalid.");
        Check(!SocratesCardProbeReport.Validate(deck, deck, [first with { AfterFields = "changed" }], out _), "A changed source snapshot is invalid.");
        Check(!SocratesCardProbeReport.Validate(deck, deck, [first with { FirstFields = "changed" }], out _), "Mismatching converted fields are invalid.");
        Console.WriteLine("Socrates card probe checks passed (10 reference and field counterexamples; no native game invocation).");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
