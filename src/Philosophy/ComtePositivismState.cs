using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum ComtePositivismStage { Invitation, ResultStages, ResultAnomaly, ResultKept }
internal enum ComtePositivismAction { None, ThreeStages, KeepAnomaly, KeepBacon, Leave }
internal enum ComtePositivismStandard { None, Stages, Anomaly, Baconian }

/// <summary>
/// Comte's third-act resolution on the social-knowledge line. Bacon's delivered
/// third-act room is the prerequisite; the act's single resolution is a question
/// edge (switch to Comte's stage model) or keeping Bacon.
/// </summary>
internal sealed class ComtePositivismState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE";
    public const string AdoptedNodeId = "COMTE__KNOWLEDGE_AND_DOUBT__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_048";
    public const string CompletionEdgeId = "WESTERN_EDGE_068";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public ComtePositivismStage Stage { get; set; }
    public ComtePositivismAction PendingAction { get; set; }
    public List<ComtePositivismAction> ConfirmedActions { get; set; } = [];
    public ComtePositivismStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static ComtePositivismState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != ComtePositivismAction.None
            || PendingAction != ComtePositivismAction.None
                && !ComtePositivismFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (ComtePositivismAction action in ConfirmedActions)
        {
            if (replay.Closed || !ComtePositivismFlow.Options(replay.Stage).Contains(action)) return false;
            ComtePositivismFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class ComtePositivismFlow
{
    public static IReadOnlyList<ComtePositivismAction> Options(ComtePositivismStage stage) => stage switch
    {
        ComtePositivismStage.Invitation =>
            [ComtePositivismAction.ThreeStages, ComtePositivismAction.KeepAnomaly,
                ComtePositivismAction.KeepBacon],
        ComtePositivismStage.ResultStages or ComtePositivismStage.ResultAnomaly
            or ComtePositivismStage.ResultKept => [ComtePositivismAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(ComtePositivismState state, ComtePositivismAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != ComtePositivismAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(ComtePositivismState state, ComtePositivismAction action)
    {
        switch (action)
        {
            case ComtePositivismAction.ThreeStages:
                state.Standard = ComtePositivismStandard.Stages;
                state.EdgeCommitted = true;
                state.Stage = ComtePositivismStage.ResultStages;
                break;
            case ComtePositivismAction.KeepAnomaly:
                state.Standard = ComtePositivismStandard.Anomaly;
                state.EdgeCommitted = true;
                state.Stage = ComtePositivismStage.ResultAnomaly;
                break;
            case ComtePositivismAction.KeepBacon:
                state.Standard = ComtePositivismStandard.Baconian;
                state.QuestionDeclined = true;
                state.Stage = ComtePositivismStage.ResultKept;
                break;
            case ComtePositivismAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = ComtePositivismAction.None;
    }

    public static bool TryCommitPending(ComtePositivismState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == ComtePositivismAction.None) return false;
        ComtePositivismAction action = state.PendingAction;
        bool adopts = action is ComtePositivismAction.ThreeStages or ComtePositivismAction.KeepAnomaly;
        bool declines = action == ComtePositivismAction.KeepBacon;
        if (adopts && !journey.CompletedEdgeIds.Contains(ComtePositivismState.CompletionEdgeId)
            && !graph.TryApply(journey, ComtePositivismState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(3)
            && !graph.TryDecline(journey, "Question", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(ComtePositivismState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(ComtePositivismState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(ComtePositivismState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !adopted && !journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == ComtePositivismState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 3
                && journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == ComtePositivismState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.ResolvedQuestionActs.Contains(3)
            && journey.CurrentNodeId == ComtePositivismState.SourceNodeId;
    }

    public static string DescriptionKey(ComtePositivismState state) => state.Stage switch
    {
        ComtePositivismStage.Invitation => "INVITATION",
        ComtePositivismStage.ResultStages => "RESULT_ALIENATION",
        ComtePositivismStage.ResultAnomaly => "RESULT_PROGRESS",
        ComtePositivismStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(ComtePositivismStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(ComtePositivismAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
