using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum FoucaultPowerStage { Invitation, ResultPower, ResultDiscipline, ResultKept, Benjamin }
internal enum FoucaultPowerAction { None, PowerKnowledge, Discipline, KeepNietzsche, BenjaminHistory, Leave }
internal enum FoucaultPowerStandard { None, Power, Discipline, Nietzschean }

/// <summary>
/// Foucault's second-act room on the genealogy-and-power line. The player either
/// links power to knowledge, describes disciplinary institutions, or keeps
/// Nietzsche and gives up the act's fixed slot.
/// </summary>
internal sealed class FoucaultPowerState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "FRIEDRICH_NIETZSCHE__HISTORY_AND_POWER__CORE";
    public const string AdoptedNodeId = "MICHEL_FOUCAULT__HISTORY_AND_POWER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_063";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    /// <summary>True when a route, not the default line, placed this room in its act.</summary>
    public bool RoutePlaced { get; set; }

    /// <summary>The node a route arrives from when it places this room.</summary>
    public string RouteSourceNodeId { get; set; } = string.Empty;

    public FoucaultPowerStage Stage { get; set; }
    public FoucaultPowerAction PendingAction { get; set; }
    public List<FoucaultPowerAction> ConfirmedActions { get; set; } = [];
    public FoucaultPowerStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 1;
    public string EdgeId { get; set; } = CompletionEdgeId;

    public static FoucaultPowerState Create(string instanceId) => CreateAt(instanceId, 1, CompletionEdgeId);

    public static FoucaultPowerState CreateAt(string instanceId, int actIndex, string edgeId) =>
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
            || Closed && PendingAction != FoucaultPowerAction.None
            || PendingAction != FoucaultPowerAction.None
                && !FoucaultPowerFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (FoucaultPowerAction action in ConfirmedActions)
        {
            if (replay.Closed || !FoucaultPowerFlow.Options(replay.Stage).Contains(action)) return false;
            FoucaultPowerFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class FoucaultPowerFlow
{
    public static IReadOnlyList<FoucaultPowerAction> Options(FoucaultPowerStage stage) => stage switch
    {
        FoucaultPowerStage.Invitation =>
            [FoucaultPowerAction.PowerKnowledge, FoucaultPowerAction.Discipline,
                FoucaultPowerAction.KeepNietzsche],
        FoucaultPowerStage.ResultPower or FoucaultPowerStage.ResultDiscipline
            or FoucaultPowerStage.ResultKept =>
            [FoucaultPowerAction.BenjaminHistory, FoucaultPowerAction.Leave],
        FoucaultPowerStage.Benjamin => [FoucaultPowerAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(FoucaultPowerState state, FoucaultPowerAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != FoucaultPowerAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(FoucaultPowerState state, FoucaultPowerAction action)
    {
        switch (action)
        {
            case FoucaultPowerAction.PowerKnowledge:
                state.Standard = FoucaultPowerStandard.Power;
                state.EdgeCommitted = true;
                state.Stage = FoucaultPowerStage.ResultPower;
                break;
            case FoucaultPowerAction.Discipline:
                state.Standard = FoucaultPowerStandard.Discipline;
                state.EdgeCommitted = true;
                state.Stage = FoucaultPowerStage.ResultDiscipline;
                break;
            case FoucaultPowerAction.KeepNietzsche:
                state.Standard = FoucaultPowerStandard.Nietzschean;
                state.FixedDeclined = true;
                state.Stage = FoucaultPowerStage.ResultKept;
                break;
            case FoucaultPowerAction.BenjaminHistory:
                state.Stage = FoucaultPowerStage.Benjamin;
                break;
            case FoucaultPowerAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = FoucaultPowerAction.None;
    }

    public static bool TryCommitPending(FoucaultPowerState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == FoucaultPowerAction.None) return false;
        FoucaultPowerAction action = state.PendingAction;
        bool adopts = action is FoucaultPowerAction.PowerKnowledge or FoucaultPowerAction.Discipline;
        bool declines = action == FoucaultPowerAction.KeepNietzsche;
        if (adopts && !journey.CompletedEdgeIds.Contains(FoucaultPowerState.CompletionEdgeId)
            && !graph.TryApply(journey, FoucaultPowerState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(FoucaultPowerState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(FoucaultPowerState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == (state.RoutePlaced ? state.ActIndex : 2)
                && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && (journey.CurrentNodeId == FoucaultPowerState.SourceNodeId || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId);
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == FoucaultPowerState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && (journey.CurrentNodeId == FoucaultPowerState.SourceNodeId || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId);
    }

    public static string DescriptionKey(FoucaultPowerState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(FoucaultPowerStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(FoucaultPowerAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
