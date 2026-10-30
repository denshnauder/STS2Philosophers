using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HorkheimerReasonStage { Invitation, ResultReason, ResultCulture, ResultKept }
internal enum HorkheimerReasonAction { None, InstrumentalReason, CultureIndustry, KeepMarx, Leave }
internal enum HorkheimerReasonStandard { None, Reason, Culture, Marxian }

/// <summary>
/// Horkheimer's second-act room on the social-critique line. The player either
/// criticises instrumental reason, describes the culture industry, or keeps
/// Marx and gives up the act's fixed slot.
/// </summary>
internal sealed class HorkheimerReasonState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "KARL_MARX__HISTORY_AND_POWER__EARLY";
    public const string AdoptedNodeId = "MAX_HORKHEIMER__HISTORY_AND_POWER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_084";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    /// <summary>True when a route, not the default line, placed this room in its act.</summary>
    public bool RoutePlaced { get; set; }

    /// <summary>The node a route arrives from when it places this room.</summary>
    public string RouteSourceNodeId { get; set; } = string.Empty;

    public HorkheimerReasonStage Stage { get; set; }
    public HorkheimerReasonAction PendingAction { get; set; }
    public List<HorkheimerReasonAction> ConfirmedActions { get; set; } = [];
    public HorkheimerReasonStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 1;
    public string EdgeId { get; set; } = CompletionEdgeId;

    public static HorkheimerReasonState Create(string instanceId) => CreateAt(instanceId, 1, CompletionEdgeId);

    public static HorkheimerReasonState CreateAt(string instanceId, int actIndex, string edgeId) =>
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
            || Closed && PendingAction != HorkheimerReasonAction.None
            || PendingAction != HorkheimerReasonAction.None
                && !HorkheimerReasonFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (HorkheimerReasonAction action in ConfirmedActions)
        {
            if (replay.Closed || !HorkheimerReasonFlow.Options(replay.Stage).Contains(action)) return false;
            HorkheimerReasonFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class HorkheimerReasonFlow
{
    public static IReadOnlyList<HorkheimerReasonAction> Options(HorkheimerReasonStage stage) => stage switch
    {
        HorkheimerReasonStage.Invitation =>
            [HorkheimerReasonAction.InstrumentalReason, HorkheimerReasonAction.CultureIndustry,
                HorkheimerReasonAction.KeepMarx],
        HorkheimerReasonStage.ResultReason or HorkheimerReasonStage.ResultCulture
            or HorkheimerReasonStage.ResultKept =>
            [HorkheimerReasonAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(HorkheimerReasonState state, HorkheimerReasonAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != HorkheimerReasonAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(HorkheimerReasonState state, HorkheimerReasonAction action)
    {
        switch (action)
        {
            case HorkheimerReasonAction.InstrumentalReason:
                state.Standard = HorkheimerReasonStandard.Reason;
                state.EdgeCommitted = true;
                state.Stage = HorkheimerReasonStage.ResultReason;
                break;
            case HorkheimerReasonAction.CultureIndustry:
                state.Standard = HorkheimerReasonStandard.Culture;
                state.EdgeCommitted = true;
                state.Stage = HorkheimerReasonStage.ResultCulture;
                break;
            case HorkheimerReasonAction.KeepMarx:
                state.Standard = HorkheimerReasonStandard.Marxian;
                state.FixedDeclined = true;
                state.Stage = HorkheimerReasonStage.ResultKept;
                break;
            case HorkheimerReasonAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = HorkheimerReasonAction.None;
    }

    public static bool TryCommitPending(HorkheimerReasonState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == HorkheimerReasonAction.None) return false;
        HorkheimerReasonAction action = state.PendingAction;
        bool adopts = action is HorkheimerReasonAction.InstrumentalReason or HorkheimerReasonAction.CultureIndustry;
        bool declines = action == HorkheimerReasonAction.KeepMarx;
        if (adopts && !journey.CompletedEdgeIds.Contains(HorkheimerReasonState.CompletionEdgeId)
            && !graph.TryApply(journey, HorkheimerReasonState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HorkheimerReasonState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(HorkheimerReasonState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == (state.RoutePlaced ? state.ActIndex : 2) && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && (journey.CurrentNodeId == HorkheimerReasonState.SourceNodeId || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId);
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == HorkheimerReasonState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && (journey.CurrentNodeId == HorkheimerReasonState.SourceNodeId || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId);
    }

    public static string DescriptionKey(HorkheimerReasonState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(HorkheimerReasonStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HorkheimerReasonAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
