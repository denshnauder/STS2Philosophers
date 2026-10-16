using System.Text.Json;

namespace STS2Philosophers;

internal static class WesternEntryPracticeStateCodec
{
    private const int CurrentVersion = 1;

    public static string Encode(WesternEntryPracticeState state) =>
        JsonSerializer.Serialize(new WesternEntryPracticePayload(CurrentVersion, state));

    public static WesternEntryPracticeState Restore(string? payload, string problemId)
    {
        WesternEntryPracticeState Fresh() => new() { ProblemId = problemId };
        if (!WesternEntryPracticeState.IsKnownProblem(problemId) || string.IsNullOrWhiteSpace(payload)) return Fresh();
        try
        {
            WesternEntryPracticePayload? saved = JsonSerializer.Deserialize<WesternEntryPracticePayload>(payload);
            if (saved is not { Version: CurrentVersion, State: not null }
                || !saved.State.IsValidForRestore(problemId)) return Fresh();
            saved.State.SuspendLegacyFlow();
            return saved.State;
        }
        catch (JsonException) { return Fresh(); }
    }

    private sealed record WesternEntryPracticePayload(int Version, WesternEntryPracticeState State);
}
