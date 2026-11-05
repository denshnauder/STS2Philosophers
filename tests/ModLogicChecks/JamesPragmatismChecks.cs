using System.Text.Json;
using STS2Philosophers;

internal static class JamesPragmatismChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        var state = JamesPragmatismState.CreateAt("JAMES_PRAGMATISM:TEST:2", 2);
        Check(state.ActIndex == 2 && state.IsUsable(),
            "The independent James question records its second-act identity.");
        Check(JamesPragmatismFlow.TryPrepare(state, JamesPragmatismAction.TestPublicly)
            && JamesPragmatismFlow.TryCommitPending(state)
            && state.Stage == JamesPragmatismStage.ResultPublic
            && state.Standard == JamesPragmatismStandard.Public,
            "James's independent question commits the public test.");

        JamesPragmatismState reloaded = JsonSerializer.Deserialize<JamesPragmatismState>(
            JsonSerializer.Serialize(state))!;
        Check(reloaded.IsUsable() && reloaded.ActIndex == 2
            && JamesPragmatismFlow.TryPrepare(reloaded, JamesPragmatismAction.Leave)
            && JamesPragmatismFlow.TryCommitPending(reloaded)
            && reloaded.Closed,
            "The independent James question survives a reload and closes.");

        var privateState = JamesPragmatismState.Create("JAMES_PRAGMATISM:TEST:2");
        Check(privateState.ActIndex == 2
            && JamesPragmatismFlow.TryPrepare(privateState, JamesPragmatismAction.KeepPrivate)
            && JamesPragmatismFlow.TryCommitPending(privateState)
            && privateState.Standard == JamesPragmatismStandard.Private,
            "The legacy factory still opens at act two and records the private option.");

        var corrupt = JamesPragmatismState.CreateAt("JAMES_PRAGMATISM:TEST:2", 2);
        corrupt.Stage = JamesPragmatismStage.ResultPublic;
        Check(!corrupt.IsUsable(),
            "James rejects a result stage with no recorded action.");
    }
}
