using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum LeviStraussMythStage { Invitation, ResultStructure, ResultHistory, ResultKept }
internal enum LeviStraussMythAction { None, StructuralReading, HistoricalReading, KeepSaussure, Leave }
internal enum LeviStraussMythStandard { None, Structure, History, Saussurean }

/// <summary>
/// Levi-Strauss's second-act room on the language-and-meaning line. The player
/// either reads the myth structurally, keeps a historical reading, or keeps
/// Saussure and gives up the act's fixed slot.
/// </summary>
internal sealed class LeviStraussMythState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "FERDINAND_DE_SAUSSURE__LANGUAGE_AND_MEANING__CORE";
    public const string AdoptedNodeId = "CLAUDE_LEVI_STRAUSS__LANGUAGE_AND_MEANING__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_081";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public LeviStraussMythStage Stage { get; set; }
    public LeviStraussMythAction PendingAction { get; set; }
    public List<LeviStraussMythAction> ConfirmedActions { get; set; } = [];
    public LeviStraussMythStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 1;
    public string EdgeId { get; set; } = CompletionEdgeId;

    public static LeviStraussMythState Create(string instanceId) => CreateAt(instanceId, 1, CompletionEdgeId);

    public static LeviStraussMythState CreateAt(string instanceId, int actIndex, string edgeId) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= CompletionEdgeId;
        if (ActIndex is < 1 or > 3) ActIndex = 1;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != LeviStraussMythAction.None
            || PendingAction != LeviStraussMythAction.None
                && !LeviStraussMythFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (LeviStraussMythAction action in ConfirmedActions)
        {
            if (replay.Closed || !LeviStraussMythFlow.Options(replay.Stage).Contains(action)) return false;
            LeviStraussMythFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class LeviStraussMythFlow
{
    public static IReadOnlyList<LeviStraussMythAction> Options(LeviStraussMythStage stage) => stage switch
    {
        LeviStraussMythStage.Invitation =>
            [LeviStraussMythAction.StructuralReading, LeviStraussMythAction.HistoricalReading,
                LeviStraussMythAction.KeepSaussure],
        LeviStraussMythStage.ResultStructure or LeviStraussMythStage.ResultHistory
            or LeviStraussMythStage.ResultKept =>
            [LeviStraussMythAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(LeviStraussMythState state, LeviStraussMythAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != LeviStraussMythAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(LeviStraussMythState state, LeviStraussMythAction action)
    {
        switch (action)
        {
            case LeviStraussMythAction.StructuralReading:
                state.Standard = LeviStraussMythStandard.Structure;
                state.EdgeCommitted = true;
                state.Stage = LeviStraussMythStage.ResultStructure;
                break;
            case LeviStraussMythAction.HistoricalReading:
                state.Standard = LeviStraussMythStandard.History;
                state.EdgeCommitted = true;
                state.Stage = LeviStraussMythStage.ResultHistory;
                break;
            case LeviStraussMythAction.KeepSaussure:
                state.Standard = LeviStraussMythStandard.Saussurean;
                state.FixedDeclined = true;
                state.Stage = LeviStraussMythStage.ResultKept;
                break;
            case LeviStraussMythAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = LeviStraussMythAction.None;
    }

    public static bool TryCommitPending(LeviStraussMythState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == LeviStraussMythAction.None) return false;
        LeviStraussMythAction action = state.PendingAction;
        bool adopts = action is LeviStraussMythAction.StructuralReading or LeviStraussMythAction.HistoricalReading;
        bool declines = action == LeviStraussMythAction.KeepSaussure;
        if (adopts && !journey.CompletedEdgeIds.Contains(LeviStraussMythState.CompletionEdgeId)
            && !graph.TryApply(journey, LeviStraussMythState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(LeviStraussMythState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(LeviStraussMythState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == LeviStraussMythState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == LeviStraussMythState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && journey.CurrentNodeId == LeviStraussMythState.SourceNodeId;
    }

    public static string DescriptionKey(LeviStraussMythState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(LeviStraussMythStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(LeviStraussMythAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
