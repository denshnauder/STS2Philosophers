namespace STS2Philosophers;

internal static class RitsuRunPersistenceProbeStateChecks
{
    public static void Run()
    {
        RitsuRunPersistenceProbeState state = new();
        Assert(!state.IsValid(), "A fresh probe must not masquerade as restored data.");

        state.RecordWrite();
        Assert(state.IsValid(), "A recorded probe must carry the fixed schema and token.");
        Assert(state.WriteCount == 1, "The first probe write must have revision one.");

        state.RecordWrite();
        Assert(state.IsValid() && state.WriteCount == 2,
            "A second write must preserve identity and advance the revision.");

        state.ProbeToken = "WRONG";
        Assert(!state.IsValid(), "A mismatched token must fail validation.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
