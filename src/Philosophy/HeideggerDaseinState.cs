using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HeideggerDaseinStage { Invitation, ResultSituation, ResultDasein, ResultKept }
internal enum HeideggerDaseinAction { None, BeingInSituation, DescribeDasein, KeepHusserl, Leave }
internal enum HeideggerDaseinStandard { None, Situation, Dasein, Husserlian }

/// <summary>
/// Heidegger's second-act room on the phenomenology-and-interpretation line.
/// The player either reads being from its situation, describes Dasein's
/// structures, or keeps Husserl and gives up the act's fixed slot.
/// </summary>
internal sealed class HeideggerDaseinState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "EDMUND_HUSSERL__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "MARTIN_HEIDEGGER__SELF_AND_OTHER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_073";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    /// <summary>True when a route, not the default line, placed this room in its act.</summary>
    public bool RoutePlaced { get; set; }

    /// <summary>The node a route arrives from when it places this room.</summary>
    public string RouteSourceNodeId { get; set; } = string.Empty;

    public HeideggerDaseinStage Stage { get; set; }
    public HeideggerDaseinAction PendingAction { get; set; }
    public List<HeideggerDaseinAction> ConfirmedActions { get; set; } = [];
    public HeideggerDaseinStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 1;
    public string EdgeId { get; set; } = CompletionEdgeId;

    public static HeideggerDaseinState Create(string instanceId) => CreateAt(instanceId, 1, CompletionEdgeId);

    public static HeideggerDaseinState CreateAt(string instanceId, int actIndex, string edgeId) =>
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
            || Closed && PendingAction != HeideggerDaseinAction.None
            || PendingAction != HeideggerDaseinAction.None
                && !HeideggerDaseinFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (HeideggerDaseinAction action in ConfirmedActions)
        {
            if (replay.Closed || !HeideggerDaseinFlow.Options(replay.Stage).Contains(action)) return false;
            HeideggerDaseinFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class HeideggerDaseinFlow
{
    public static IReadOnlyList<HeideggerDaseinAction> Options(HeideggerDaseinStage stage) => stage switch
    {
        HeideggerDaseinStage.Invitation =>
            [HeideggerDaseinAction.BeingInSituation, HeideggerDaseinAction.DescribeDasein,
                HeideggerDaseinAction.KeepHusserl],
        HeideggerDaseinStage.ResultSituation or HeideggerDaseinStage.ResultDasein
            or HeideggerDaseinStage.ResultKept =>
            [HeideggerDaseinAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(HeideggerDaseinState state, HeideggerDaseinAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != HeideggerDaseinAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(HeideggerDaseinState state, HeideggerDaseinAction action)
    {
        switch (action)
        {
            case HeideggerDaseinAction.BeingInSituation:
                state.Standard = HeideggerDaseinStandard.Situation;
                state.EdgeCommitted = true;
                state.Stage = HeideggerDaseinStage.ResultSituation;
                break;
            case HeideggerDaseinAction.DescribeDasein:
                state.Standard = HeideggerDaseinStandard.Dasein;
                state.EdgeCommitted = true;
                state.Stage = HeideggerDaseinStage.ResultDasein;
                break;
            case HeideggerDaseinAction.KeepHusserl:
                state.Standard = HeideggerDaseinStandard.Husserlian;
                state.FixedDeclined = true;
                state.Stage = HeideggerDaseinStage.ResultKept;
                break;
            case HeideggerDaseinAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = HeideggerDaseinAction.None;
    }

    public static bool TryCommitPending(HeideggerDaseinState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == HeideggerDaseinAction.None) return false;
        HeideggerDaseinAction action = state.PendingAction;
        bool adopts = action is HeideggerDaseinAction.BeingInSituation or HeideggerDaseinAction.DescribeDasein;
        bool declines = action == HeideggerDaseinAction.KeepHusserl;
        if (adopts && !journey.CompletedEdgeIds.Contains(HeideggerDaseinState.CompletionEdgeId)
            && !graph.TryApply(journey, HeideggerDaseinState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HeideggerDaseinState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(HeideggerDaseinState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && (journey.CurrentNodeId == HeideggerDaseinState.SourceNodeId || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId);
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == HeideggerDaseinState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && (journey.CurrentNodeId == HeideggerDaseinState.SourceNodeId || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId);
    }

    public static string DescriptionKey(HeideggerDaseinState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(HeideggerDaseinStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HeideggerDaseinAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
