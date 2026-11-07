using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum GadamerFusionStage { Invitation, ResultFusion, ResultHistory, ResultKept, Echo }
internal enum GadamerFusionAction { None, HorizonFusion, EffectiveHistory, KeepHeidegger, Leave, HearEcho }
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
    public const string FixedRequiredEdgeId = "WESTERN_EDGE_164";
    public const string FixedCompletionEdgeId = "WESTERN_EDGE_161";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int ActIndex { get; set; } = 3;
    public string EdgeId { get; set; } = CompletionEdgeId;
    public bool FixedMode { get; set; }
    public GadamerFusionStage Stage { get; set; }
    public GadamerFusionAction PendingAction { get; set; }
    public List<GadamerFusionAction> ConfirmedActions { get; set; } = [];
    public GadamerFusionStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static GadamerFusionState Create(string instanceId) => new() { InstanceId = instanceId };

    public static GadamerFusionState CreateAt(string instanceId, int actIndex, string edgeId, bool fixedMode) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId, FixedMode = fixedMode };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= CompletionEdgeId;
        if (ActIndex is < 1 or > 3) ActIndex = 3;
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
        var replay = CreateAt(InstanceId, ActIndex, EdgeId, FixedMode);
        foreach (GadamerFusionAction action in ConfirmedActions)
        {
            if (replay.Closed || !GadamerFusionFlow.Options(replay.Stage).Contains(action)) return false;
            GadamerFusionFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && FixedDeclined == replay.FixedDeclined
            && Closed == replay.Closed;
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
            or GadamerFusionStage.ResultKept => [GadamerFusionAction.HearEcho, GadamerFusionAction.Leave],
        GadamerFusionStage.Echo => [GadamerFusionAction.Leave],
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
                if (state.FixedMode) state.FixedDeclined = true;
                else state.QuestionDeclined = true;
                state.Stage = GadamerFusionStage.ResultKept;
                break;
            case GadamerFusionAction.HearEcho:
                state.Stage = GadamerFusionStage.Echo;
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
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && state.FixedMode && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        if (declines && !state.FixedMode && !journey.ResolvedQuestionActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Question", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(GadamerFusionState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        string required = state.FixedMode ? GadamerFusionState.FixedRequiredEdgeId : GadamerFusionState.RequiredEdgeId;
        if (required.Length > 0 && !journey.CompletedEdgeIds.Contains(required)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        bool declined = state.FixedMode
            ? journey.DeclinedFixedActs.Contains(state.ActIndex)
            : journey.ResolvedQuestionActs.Contains(state.ActIndex);
        if (!state.EdgeCommitted && !declined)
        {
            if (state.FixedMode)
                return journey.LastFixedAct is 2 or 3 && !adopted
                    && journey.CurrentNodeId == GadamerFusionState.SourceNodeId;
            return journey.LastFixedAct == 3 && !adopted
                && journey.CurrentNodeId == GadamerFusionState.SourceNodeId;
        }
        if (state.EdgeCommitted)
            return adopted && journey.LastFixedAct == 3 && (state.FixedMode || declined)
                && journey.CurrentNodeId == GadamerFusionState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && declined
            && journey.CurrentNodeId == GadamerFusionState.SourceNodeId;
    }

    public static string DescriptionKey(GadamerFusionState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(GadamerFusionStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(GadamerFusionAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
