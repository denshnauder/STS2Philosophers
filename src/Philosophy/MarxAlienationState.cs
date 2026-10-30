using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum MarxAlienationStage { Invitation, ResultAlienation, ResultProgress, ResultKept }
internal enum MarxAlienationAction { None, AlienatedLabor, HistoricalProgress, KeepHegel, Leave }
internal enum MarxAlienationStandard { None, Alienation, Progress, Hegelian }

/// <summary>
/// Marx's third-act resolution on the history-and-power line. Hegel's fixed
/// second act is the prerequisite; the third act's single resolution is the
/// existing question edge 039 (switch to early Marx) or keeping Hegel.
/// </summary>
internal sealed class MarxAlienationState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "GEORG_WILHELM_FRIEDRICH_HEGEL__HISTORY_AND_POWER__CORE";
    public const string AdoptedNodeId = "KARL_MARX__HISTORY_AND_POWER__EARLY";
    public const string RequiredEdgeId = "WESTERN_EDGE_038";
    public const string CompletionEdgeId = "WESTERN_EDGE_039";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public MarxAlienationStage Stage { get; set; }
    public MarxAlienationAction PendingAction { get; set; }
    public List<MarxAlienationAction> ConfirmedActions { get; set; } = [];
    public MarxAlienationStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static MarxAlienationState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != MarxAlienationAction.None
            || PendingAction != MarxAlienationAction.None
                && !MarxAlienationFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (MarxAlienationAction action in ConfirmedActions)
        {
            if (replay.Closed || !MarxAlienationFlow.Options(replay.Stage).Contains(action)) return false;
            MarxAlienationFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class MarxAlienationFlow
{
    public static IReadOnlyList<MarxAlienationAction> Options(MarxAlienationStage stage) => stage switch
    {
        MarxAlienationStage.Invitation =>
            [MarxAlienationAction.AlienatedLabor, MarxAlienationAction.HistoricalProgress,
                MarxAlienationAction.KeepHegel],
        MarxAlienationStage.ResultAlienation or MarxAlienationStage.ResultProgress
            or MarxAlienationStage.ResultKept => [MarxAlienationAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(MarxAlienationState state, MarxAlienationAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != MarxAlienationAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(MarxAlienationState state, MarxAlienationAction action)
    {
        switch (action)
        {
            case MarxAlienationAction.AlienatedLabor:
                state.Standard = MarxAlienationStandard.Alienation;
                state.EdgeCommitted = true;
                state.Stage = MarxAlienationStage.ResultAlienation;
                break;
            case MarxAlienationAction.HistoricalProgress:
                state.Standard = MarxAlienationStandard.Progress;
                state.EdgeCommitted = true;
                state.Stage = MarxAlienationStage.ResultProgress;
                break;
            case MarxAlienationAction.KeepHegel:
                state.Standard = MarxAlienationStandard.Hegelian;
                state.QuestionDeclined = true;
                state.Stage = MarxAlienationStage.ResultKept;
                break;
            case MarxAlienationAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = MarxAlienationAction.None;
    }

    public static bool TryCommitPending(MarxAlienationState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == MarxAlienationAction.None) return false;
        MarxAlienationAction action = state.PendingAction;
        bool adopts = action is MarxAlienationAction.AlienatedLabor or MarxAlienationAction.HistoricalProgress;
        bool declines = action == MarxAlienationAction.KeepHegel;
        if (adopts && !journey.CompletedEdgeIds.Contains(MarxAlienationState.CompletionEdgeId)
            && !graph.TryApply(journey, MarxAlienationState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(3)
            && !graph.TryDecline(journey, "Question", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(MarxAlienationState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(MarxAlienationState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(MarxAlienationState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !adopted && !journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == MarxAlienationState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 3
                && journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == MarxAlienationState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.ResolvedQuestionActs.Contains(3)
            && journey.CurrentNodeId == MarxAlienationState.SourceNodeId;
    }

    public static string DescriptionKey(MarxAlienationState state) => state.Stage switch
    {
        MarxAlienationStage.Invitation => "INVITATION",
        MarxAlienationStage.ResultAlienation => "RESULT_ALIENATION",
        MarxAlienationStage.ResultProgress => "RESULT_PROGRESS",
        MarxAlienationStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(MarxAlienationStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(MarxAlienationAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
