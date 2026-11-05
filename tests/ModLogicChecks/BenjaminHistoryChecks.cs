using System.Text.Json;
using STS2Philosophers;

internal static class BenjaminHistoryChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        var state = BenjaminHistoryState.CreateAt("BENJAMIN_HISTORY:TEST:2", 2);
        Check(state.ActIndex == 2 && state.IsUsable() && !state.Closed,
            "The independent Benjamin perspective records its second-act identity.");
        state.Closed = true;
        Check(state.IsUsable(),
            "A closed Benjamin perspective remains reloadable.");

        BenjaminHistoryState reloaded = JsonSerializer.Deserialize<BenjaminHistoryState>(
            JsonSerializer.Serialize(state))!;
        Check(reloaded.IsUsable() && reloaded.ActIndex == 2 && reloaded.Closed,
            "The independent Benjamin perspective survives a reload.");

        var corrupt = BenjaminHistoryState.CreateAt("BENJAMIN_HISTORY:TEST:2", 2);
        corrupt.Version = 0;
        Check(!corrupt.IsUsable(),
            "Benjamin rejects an unknown state version.");
    }
}
