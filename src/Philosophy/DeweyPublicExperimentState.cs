using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum DeweyPublicExperimentStage { Invitation, ResultExperiment, ResultCommunity, ResultKept }
internal enum DeweyPublicExperimentAction { None, PublicExperiment, KeepInquiryCommunity, KeepPeirce, Leave }
internal enum DeweyPublicExperimentStandard { None, Experiment, Community, Peircean }

/// <summary>
/// Dewey's third-act resolution on the fallible-inquiry line. Peirce's second-act
/// room is the prerequisite; the act's single resolution is a question edge
/// (switch to Dewey's public experiment) or keeping Peirce.
/// </summary>
internal sealed class DeweyPublicExperimentState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "CHARLES_SANDERS_PEIRCE__KNOWLEDGE_AND_DOUBT__CORE";
    public const string AdoptedNodeId = "JOHN_DEWEY__KNOWLEDGE_AND_DOUBT__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_070";
    public const string CompletionEdgeId = "WESTERN_EDGE_071";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    /// <summary>True when a route, not the default line, placed this room in its act.</summary>
    public bool RoutePlaced { get; set; }

    /// <summary>The node a route arrives from when it places this room.</summary>
    public string RouteSourceNodeId { get; set; } = string.Empty;

    public DeweyPublicExperimentStage Stage { get; set; }
    public DeweyPublicExperimentAction PendingAction { get; set; }
    public List<DeweyPublicExperimentAction> ConfirmedActions { get; set; } = [];
    public DeweyPublicExperimentStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static DeweyPublicExperimentState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != DeweyPublicExperimentAction.None
            || PendingAction != DeweyPublicExperimentAction.None
                && !DeweyPublicExperimentFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (DeweyPublicExperimentAction action in ConfirmedActions)
        {
            if (replay.Closed || !DeweyPublicExperimentFlow.Options(replay.Stage).Contains(action)) return false;
            DeweyPublicExperimentFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class DeweyPublicExperimentFlow
{
    public static IReadOnlyList<DeweyPublicExperimentAction> Options(DeweyPublicExperimentStage stage) => stage switch
    {
        DeweyPublicExperimentStage.Invitation =>
            [DeweyPublicExperimentAction.PublicExperiment, DeweyPublicExperimentAction.KeepInquiryCommunity,
                DeweyPublicExperimentAction.KeepPeirce],
        DeweyPublicExperimentStage.ResultExperiment or DeweyPublicExperimentStage.ResultCommunity
            or DeweyPublicExperimentStage.ResultKept => [DeweyPublicExperimentAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(DeweyPublicExperimentState state, DeweyPublicExperimentAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != DeweyPublicExperimentAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(DeweyPublicExperimentState state, DeweyPublicExperimentAction action)
    {
        switch (action)
        {
            case DeweyPublicExperimentAction.PublicExperiment:
                state.Standard = DeweyPublicExperimentStandard.Experiment;
                state.EdgeCommitted = true;
                state.Stage = DeweyPublicExperimentStage.ResultExperiment;
                break;
            case DeweyPublicExperimentAction.KeepInquiryCommunity:
                state.Standard = DeweyPublicExperimentStandard.Community;
                state.EdgeCommitted = true;
                state.Stage = DeweyPublicExperimentStage.ResultCommunity;
                break;
            case DeweyPublicExperimentAction.KeepPeirce:
                state.Standard = DeweyPublicExperimentStandard.Peircean;
                state.QuestionDeclined = true;
                state.Stage = DeweyPublicExperimentStage.ResultKept;
                break;
            case DeweyPublicExperimentAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = DeweyPublicExperimentAction.None;
    }

    public static bool TryCommitPending(DeweyPublicExperimentState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == DeweyPublicExperimentAction.None) return false;
        DeweyPublicExperimentAction action = state.PendingAction;
        bool adopts = action is DeweyPublicExperimentAction.PublicExperiment or DeweyPublicExperimentAction.KeepInquiryCommunity;
        bool declines = action == DeweyPublicExperimentAction.KeepPeirce;
        if (adopts && !journey.CompletedEdgeIds.Contains(DeweyPublicExperimentState.CompletionEdgeId)
            && !graph.TryApply(journey, DeweyPublicExperimentState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(3)
            && !graph.TryDecline(journey, "Question", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(DeweyPublicExperimentState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(DeweyPublicExperimentState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(DeweyPublicExperimentState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !adopted && !journey.ResolvedQuestionActs.Contains(3)
                && (journey.CurrentNodeId == DeweyPublicExperimentState.SourceNodeId || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId);
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 3
                && journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == DeweyPublicExperimentState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.ResolvedQuestionActs.Contains(3)
            && (journey.CurrentNodeId == DeweyPublicExperimentState.SourceNodeId || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId);
    }

    public static string DescriptionKey(DeweyPublicExperimentState state) => state.Stage switch
    {
        DeweyPublicExperimentStage.Invitation => "INVITATION",
        DeweyPublicExperimentStage.ResultExperiment => "RESULT_ALIENATION",
        DeweyPublicExperimentStage.ResultCommunity => "RESULT_PROGRESS",
        DeweyPublicExperimentStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(DeweyPublicExperimentStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(DeweyPublicExperimentAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
