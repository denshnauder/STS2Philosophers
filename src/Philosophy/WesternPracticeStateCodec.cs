using System.Text.Json;

namespace STS2Philosophers;

internal static class WesternPracticeStateCodec
{
    public static string Encode(WesternPracticeState state) => JsonSerializer.Serialize(state);

    // A node carrier has one self-contained saved payload; restoration cannot depend on property setter order.
    public static WesternPracticeState RestoreNode(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return new();
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("NodeId", out var node)
                || node.ValueKind != JsonValueKind.String) return new();
            string nodeId = node.GetString()!;
            string problemId = WesternNodePracticePolicy.ProblemFor(nodeId);
            return problemId.Length > 0 ? Restore(payload, problemId, nodeId) : new();
        }
        catch (JsonException) { return new(); }
    }

    public static WesternPracticeState Restore(string? payload, string problemId, string nodeId = "")
    {
        WesternPracticeState Fresh() => new() { ProblemId = problemId, NodeId = nodeId };
        if (nodeId.Length > 0 && WesternNodePracticePolicy.ProblemFor(nodeId) != problemId) return Fresh();
        if (string.IsNullOrWhiteSpace(payload)) return Fresh();
        try
        {
            WesternPracticeState? state = JsonSerializer.Deserialize<WesternPracticeState>(payload);
            if (state is null || state.ProblemId != problemId || state.NodeId != nodeId || state.Plays is null || state.PreviousKinds is null
                || state.Turn < 0 || state.PreviousCards < 0 || state.PendingTurn < 0
                || state.LastClaimedTurn < 0 || state.LastClaimedTurn > state.Turn
                || state.SuccessfulTurns < 0 || state.BrokenTurns < 0
                || state.PreviousKinds.Any(kind => !Enum.IsDefined(kind))
                // Old saves have no sequence: absence is allowed, fabricated facts are not.
                || (state.PreviousKinds.Count > 0 && (state.Turn <= 1 || state.PreviousKinds.Count != state.PreviousCards))
                || state.Plays.Any(play => play is null || string.IsNullOrWhiteSpace(play.PlayId)
                    || string.IsNullOrWhiteSpace(play.CardModelId) || !Enum.IsDefined(play.Kind))
                || state.Plays.Select(play => play.PlayId).Distinct(StringComparer.Ordinal).Count() != state.Plays.Count)
                return Fresh();
            WesternPracticeReward reward = state.PendingReward;
            // Legacy entries keep their fixed package; node rewards require the actual closed-turn evidence.
            WesternPracticeReward allowed = WesternPracticeState.RewardFor(problemId);
            if (nodeId.Length > 0 && reward != default)
            {
                if (state.PendingEvidence is not { Plays: not null, PreviousKinds: not null } evidence) return Fresh();
                allowed = WesternNodePracticePolicy.Evaluate(nodeId, evidence.Plays, evidence.PreviousKinds, evidence.PreviousCards);
            }
            if (reward != default && (reward != allowed
                || state.PendingTurn != state.Turn + (state.Closed ? 1 : 0)
                || state.PendingTurn <= state.LastClaimedTurn)) return Fresh();
            if (reward == default && (state.PendingTurn != 0 || state.PendingEvidence is not null)) return Fresh();
            return state;
        }
        catch (JsonException) { return Fresh(); }
    }
}
