using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HusserlEpocheStage { Invitation, ResultAppearance, ResultSuspension, ResultKept, ResultEmbodied }
internal enum HusserlEpocheAction { None, DescribeAppearance, SuspendThesis, TurnEmbodied, KeepDescartes, Leave }
internal enum HusserlEpocheStandard { None, Appearance, Suspension, Cartesian, Embodied }

/// <summary>
/// Kierkegaard's first-act room on the self-and-other line. The player either
/// takes the leap of faith, keeps the ethical universal, or stays with Socrates
/// and gives up the act's fixed slot.
/// </summary>
internal sealed class HusserlEpocheState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "DESCARTES__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "EDMUND_HUSSERL__SELF_AND_OTHER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_072";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public HusserlEpocheStage Stage { get; set; }
    public HusserlEpocheAction PendingAction { get; set; }
    public List<HusserlEpocheAction> ConfirmedActions { get; set; } = [];
    public HusserlEpocheStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 1;
    public string EdgeId { get; set; } = CompletionEdgeId;

    // The room opens at the first act's question node, standing on Descartes:
    // the fixed link it commits is the first act's, so its position is one.
    public static HusserlEpocheState Create(string instanceId) => CreateAt(instanceId, 1, CompletionEdgeId);

    public static HusserlEpocheState CreateAt(string instanceId, int actIndex, string edgeId) =>
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
            || Closed && PendingAction != HusserlEpocheAction.None
            || PendingAction != HusserlEpocheAction.None
                && !HusserlEpocheFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (HusserlEpocheAction action in ConfirmedActions)
        {
            if (replay.Closed || !HusserlEpocheFlow.Options(replay.Stage).Contains(action)) return false;
            HusserlEpocheFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class HusserlEpocheFlow
{
    public static IReadOnlyList<HusserlEpocheAction> Options(HusserlEpocheStage stage) => stage switch
    {
        HusserlEpocheStage.Invitation =>
            [HusserlEpocheAction.DescribeAppearance, HusserlEpocheAction.SuspendThesis,
                HusserlEpocheAction.TurnEmbodied, HusserlEpocheAction.KeepDescartes],
        HusserlEpocheStage.ResultAppearance or HusserlEpocheStage.ResultSuspension
            or HusserlEpocheStage.ResultKept or HusserlEpocheStage.ResultEmbodied
            => [HusserlEpocheAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(HusserlEpocheState state, HusserlEpocheAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != HusserlEpocheAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(HusserlEpocheState state, HusserlEpocheAction action)
    {
        switch (action)
        {
            case HusserlEpocheAction.DescribeAppearance:
                state.Standard = HusserlEpocheStandard.Appearance;
                state.EdgeCommitted = true;
                state.Stage = HusserlEpocheStage.ResultAppearance;
                break;
            case HusserlEpocheAction.SuspendThesis:
                state.Standard = HusserlEpocheStandard.Suspension;
                state.EdgeCommitted = true;
                state.Stage = HusserlEpocheStage.ResultSuspension;
                break;
            case HusserlEpocheAction.TurnEmbodied:
                state.Standard = HusserlEpocheStandard.Embodied;
                state.EdgeCommitted = true;
                state.Stage = HusserlEpocheStage.ResultEmbodied;
                break;
            case HusserlEpocheAction.KeepDescartes:
                state.Standard = HusserlEpocheStandard.Cartesian;
                state.FixedDeclined = true;
                state.Stage = HusserlEpocheStage.ResultKept;
                break;
            case HusserlEpocheAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = HusserlEpocheAction.None;
    }

    public static bool TryCommitPending(HusserlEpocheState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == HusserlEpocheAction.None) return false;
        HusserlEpocheAction action = state.PendingAction;
        bool adopts = action is HusserlEpocheAction.DescribeAppearance
            or HusserlEpocheAction.SuspendThesis or HusserlEpocheAction.TurnEmbodied;
        bool declines = action == HusserlEpocheAction.KeepDescartes;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        // Husserl stands on two lines of this entry. The first act's step cannot
        // tell them apart, so the room's answer names the one it entered: the
        // appearance line, or the embodied line the direction option asks for.
        if (adopts) graph.TryNameRoute(journey, action == HusserlEpocheAction.TurnEmbodied
            ? "EMBODIED_PERCEPTION" : "APPEARANCE_AND_INTERPRETATION");
        if (declines && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HusserlEpocheState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        int declined = state.ActIndex;
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == state.ActIndex && !adopted && !journey.DeclinedFixedActs.Contains(declined)
                && journey.CurrentNodeId == HusserlEpocheState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == Math.Min(3, state.ActIndex + 1)
                && journey.CurrentNodeId == HusserlEpocheState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == state.ActIndex && journey.DeclinedFixedActs.Contains(declined)
            && journey.CurrentNodeId == HusserlEpocheState.SourceNodeId;
    }

    public static string DescriptionKey(HusserlEpocheState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(HusserlEpocheStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HusserlEpocheAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
