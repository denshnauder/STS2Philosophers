using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HusserlEpocheStage { Invitation, ResultAppearance, ResultSuspension, ResultKept }
internal enum HusserlEpocheAction { None, DescribeAppearance, SuspendThesis, KeepDescartes, Leave }
internal enum HusserlEpocheStandard { None, Appearance, Suspension, Cartesian }

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

    public static HusserlEpocheState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != HusserlEpocheAction.None
            || PendingAction != HusserlEpocheAction.None
                && !HusserlEpocheFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
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
                HusserlEpocheAction.KeepDescartes],
        HusserlEpocheStage.ResultAppearance or HusserlEpocheStage.ResultSuspension
            or HusserlEpocheStage.ResultKept => [HusserlEpocheAction.Leave],
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
        bool adopts = action is HusserlEpocheAction.DescribeAppearance or HusserlEpocheAction.SuspendThesis;
        bool declines = action == HusserlEpocheAction.KeepDescartes;
        if (adopts && !journey.CompletedEdgeIds.Contains(HusserlEpocheState.CompletionEdgeId)
            && !graph.TryApply(journey, HusserlEpocheState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(2)
            && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HusserlEpocheState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(HusserlEpocheState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 1 && !adopted && !journey.DeclinedFixedActs.Contains(2)
                && journey.CurrentNodeId == HusserlEpocheState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 2
                && journey.CurrentNodeId == HusserlEpocheState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 2 && journey.DeclinedFixedActs.Contains(2)
            && journey.CurrentNodeId == HusserlEpocheState.SourceNodeId;
    }

    public static string DescriptionKey(HusserlEpocheState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(HusserlEpocheStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HusserlEpocheAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
