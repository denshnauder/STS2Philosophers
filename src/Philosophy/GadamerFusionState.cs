using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum GadamerFusionStage { Invitation, ResultFusion, ResultHistory, ResultKept }
internal enum GadamerFusionAction { None, HorizonFusion, EffectiveHistory, KeepHeidegger, Leave }
internal enum GadamerFusionStandard { None, Fusion, History, Heideggerian }

/// <summary>
/// Gadamer's third-act resolution on the self-and-other line. Heidegger's
/// second-act room is the prerequisite; the act's single resolution is a
/// question edge (horizon fusion) or keeping Heidegger.
/// </summary>
internal sealed class GadamerFusionState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "MARTIN_HEIDEGGER__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "HANS_GEORG_GADAMER__SELF_AND_OTHER__CORE";
    public const string RequiredEdgeId = "";
    public const string CompletionEdgeId = "WESTERN_EDGE_075";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public GadamerFusionStage Stage { get; set; }
    public GadamerFusionAction PendingAction { get; set; }
    public List<GadamerFusionAction> ConfirmedActions { get; set; } = [];
    public GadamerFusionStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static GadamerFusionState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != GadamerFusionAction.None
            || PendingAction != GadamerFusionAction.None
                && !GadamerFusionFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (GadamerFusionAction action in ConfirmedActions)
        {
            if (replay.Closed || !GadamerFusionFlow.Options(replay.Stage).Contains(action)) return false;
            GadamerFusionFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class GadamerFusionFlow
{
    public static IReadOnlyList<GadamerFusionAction> Options(GadamerFusionStage stage) => stage switch
    {
        GadamerFusionStage.Invitation =>
            [GadamerFusionAction.HorizonFusion, GadamerFusionAction.EffectiveHistory,
                GadamerFusionAction.KeepHeidegger],
        GadamerFusionStage.ResultFusion or GadamerFusionStage.ResultHistory
            or GadamerFusionStage.ResultKept => [GadamerFusionAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(GadamerFusionState state, GadamerFusionAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != GadamerFusionAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(GadamerFusionState state, GadamerFusionAction action)
    {
        switch (action)
        {
            case GadamerFusionAction.HorizonFusion:
                state.Standard = GadamerFusionStandard.Fusion;
                state.EdgeCommitted = true;
                state.Stage = GadamerFusionStage.ResultFusion;
                break;
            case GadamerFusionAction.EffectiveHistory:
                state.Standard = GadamerFusionStandard.History;
                state.EdgeCommitted = true;
                state.Stage = GadamerFusionStage.ResultHistory;
                break;
            case GadamerFusionAction.KeepHeidegger:
                state.Standard = GadamerFusionStandard.Heideggerian;
                state.QuestionDeclined = true;
                state.Stage = GadamerFusionStage.ResultKept;
                break;
            case GadamerFusionAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = GadamerFusionAction.None;
    }

    public static bool TryCommitPending(GadamerFusionState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == GadamerFusionAction.None) return false;
        GadamerFusionAction action = state.PendingAction;
        bool adopts = action is GadamerFusionAction.HorizonFusion or GadamerFusionAction.EffectiveHistory;
        bool declines = action == GadamerFusionAction.KeepHeidegger;
        if (adopts && !journey.CompletedEdgeIds.Contains(GadamerFusionState.CompletionEdgeId)
            && !graph.TryApply(journey, GadamerFusionState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(3)
            && !graph.TryDecline(journey, "Question", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(GadamerFusionState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || GadamerFusionState.RequiredEdgeId.Length > 0
                && !journey.CompletedEdgeIds.Contains(GadamerFusionState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(GadamerFusionState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !adopted && !journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == GadamerFusionState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 3
                && journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == GadamerFusionState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.ResolvedQuestionActs.Contains(3)
            && journey.CurrentNodeId == GadamerFusionState.SourceNodeId;
    }

    public static string DescriptionKey(GadamerFusionState state) => state.Stage switch
    {
        GadamerFusionStage.Invitation => "INVITATION",
        GadamerFusionStage.ResultFusion => "RESULT_ALIENATION",
        GadamerFusionStage.ResultHistory => "RESULT_PROGRESS",
        GadamerFusionStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(GadamerFusionStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(GadamerFusionAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
