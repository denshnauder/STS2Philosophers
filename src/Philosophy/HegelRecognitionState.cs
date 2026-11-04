using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HegelRecognitionStage { Invitation, ResultRecognition, ResultInstrumental, ResultExit }
internal enum HegelRecognitionAction { None, Renegotiate, KeepArrangement, AbandonOrder, Leave }
internal enum HegelRecognitionStandard { None, Recognition, Instrumental, Exit }

/// <summary>
/// Hegel's fixed room on the autonomy-and-history line. The workshop order is
/// already signed; the player either reopens its terms, keeps the arrangement,
/// or gives up the order and the act's fixed slot.
/// </summary>
internal sealed class HegelRecognitionState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "IMMANUEL_KANT__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "GEORG_WILHELM_FRIEDRICH_HEGEL__HISTORY_AND_POWER__CORE";
    public const string DefaultEdgeId = "WESTERN_EDGE_096";
    public const string EarlyEdgeId = "WESTERN_EDGE_154";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>The act this room occupies: the default third act or the early second.</summary>
    public int ActIndex { get; set; } = 3;

    /// <summary>The edge this room's adoption commits.</summary>
    public string EdgeId { get; set; } = DefaultEdgeId;

    /// <summary>True when the route, not the default line, placed this room in its act.</summary>
    public bool RoutePlaced { get; set; } = true;

    /// <summary>The node a route arrives from when it places this room.</summary>
    public string RouteSourceNodeId { get; set; } = SourceNodeId;

    public HegelRecognitionStage Stage { get; set; }
    public HegelRecognitionAction PendingAction { get; set; }
    public List<HegelRecognitionAction> ConfirmedActions { get; set; } = [];
    public HegelRecognitionStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static HegelRecognitionState CreateAt(string instanceId, int actIndex, string edgeId) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
        if (ActIndex is < 2 or > 3) ActIndex = 3;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || ActIndex is < 2 or > 3
            || !((ActIndex == 3 && EdgeId == DefaultEdgeId) || (ActIndex == 2 && EdgeId == EarlyEdgeId))
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != HegelRecognitionAction.None
            || PendingAction != HegelRecognitionAction.None
                && !HegelRecognitionFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (HegelRecognitionAction action in ConfirmedActions)
        {
            if (replay.Closed || !HegelRecognitionFlow.Options(replay.Stage).Contains(action)) return false;
            HegelRecognitionFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class HegelRecognitionFlow
{
    public static IReadOnlyList<HegelRecognitionAction> Options(HegelRecognitionStage stage) => stage switch
    {
        HegelRecognitionStage.Invitation =>
            [HegelRecognitionAction.Renegotiate, HegelRecognitionAction.KeepArrangement,
                HegelRecognitionAction.AbandonOrder],
        HegelRecognitionStage.ResultRecognition or HegelRecognitionStage.ResultInstrumental
            or HegelRecognitionStage.ResultExit => [HegelRecognitionAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(HegelRecognitionState state, HegelRecognitionAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != HegelRecognitionAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(HegelRecognitionState state, HegelRecognitionAction action)
    {
        switch (action)
        {
            case HegelRecognitionAction.Renegotiate:
                state.Standard = HegelRecognitionStandard.Recognition;
                state.EdgeCommitted = true;
                state.Stage = HegelRecognitionStage.ResultRecognition;
                break;
            case HegelRecognitionAction.KeepArrangement:
                state.Standard = HegelRecognitionStandard.Instrumental;
                state.EdgeCommitted = true;
                state.Stage = HegelRecognitionStage.ResultInstrumental;
                break;
            case HegelRecognitionAction.AbandonOrder:
                state.Standard = HegelRecognitionStandard.Exit;
                state.FixedDeclined = true;
                state.Stage = HegelRecognitionStage.ResultExit;
                break;
            case HegelRecognitionAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = HegelRecognitionAction.None;
    }

    public static bool TryCommitPending(HegelRecognitionState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == HegelRecognitionAction.None) return false;
        HegelRecognitionAction action = state.PendingAction;
        bool adopts = action is HegelRecognitionAction.Renegotiate or HegelRecognitionAction.KeepArrangement;
        bool declines = action == HegelRecognitionAction.AbandonOrder;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HegelRecognitionState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        int act = state.RoutePlaced ? state.ActIndex : 3;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        bool declined = journey.DeclinedFixedActs.Contains(act);
        if (state.EdgeCommitted)
            // The third act's fixed slot is the last one; the graph caps
            // LastFixedAct at three after either placement.
            return !state.FixedDeclined && adopted && journey.LastFixedAct == Math.Min(3, act + 1)
                && journey.CurrentNodeId == HegelRecognitionState.AdoptedNodeId;
        if (state.FixedDeclined)
            return !adopted && declined && journey.LastFixedAct == act
                && journey.CurrentNodeId == HegelRecognitionState.SourceNodeId;
        return journey.LastFixedAct == act && !adopted && !declined
            && journey.CurrentNodeId == HegelRecognitionState.SourceNodeId;
    }

    public static string DescriptionKey(HegelRecognitionState state) => state.Stage switch
    {
        HegelRecognitionStage.Invitation => "INVITATION",
        HegelRecognitionStage.ResultRecognition => "RESULT_RECOGNITION",
        HegelRecognitionStage.ResultInstrumental => "RESULT_INSTRUMENTAL",
        HegelRecognitionStage.ResultExit => "RESULT_EXIT",
        _ => "INVITATION",
    };

    public static string StageKey(HegelRecognitionStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HegelRecognitionAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
