using System.Text.Json;

namespace STS2Philosophers;

internal static class WesternPracticeStateCodec
{
    public static string Encode(WesternPracticeState state) => JsonSerializer.Serialize(state);

    public static WesternPracticeState Restore(string? payload, string problemId)
    {
        WesternPracticeState Fresh() => new() { ProblemId = problemId };
        if (string.IsNullOrWhiteSpace(payload)) return Fresh();
        try
        {
            WesternPracticeState? state = JsonSerializer.Deserialize<WesternPracticeState>(payload);
            if (state is null || state.ProblemId != problemId || state.Plays is null
                || state.Turn < 0 || state.PreviousCards < 0 || state.PendingTurn < 0
                || state.LastClaimedTurn < 0 || state.LastClaimedTurn > state.Turn
                || state.SuccessfulTurns < 0 || state.BrokenTurns < 0
                || state.Plays.Any(play => play is null || string.IsNullOrWhiteSpace(play.PlayId)
                    || string.IsNullOrWhiteSpace(play.CardModelId) || !Enum.IsDefined(play.Kind))
                || state.Plays.Select(play => play.PlayId).Distinct(StringComparer.Ordinal).Count() != state.Plays.Count)
                return Fresh();
            WesternPracticeReward reward = state.PendingReward;
            // Saved rewards may only use the known entry's package and next-turn window.
            WesternPracticeReward allowed = WesternPracticeState.RewardFor(problemId);
            if (reward != default && (reward != allowed
                || state.PendingTurn != state.Turn + (state.Closed ? 1 : 0)
                || state.PendingTurn <= state.LastClaimedTurn)) return Fresh();
            if (reward == default && state.PendingTurn != 0) return Fresh();
            return state;
        }
        catch (JsonException) { return Fresh(); }
    }
}
