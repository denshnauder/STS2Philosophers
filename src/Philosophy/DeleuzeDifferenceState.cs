using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum DeleuzeDifferenceStage { Invitation, ResultDifference, ResultRhizome, ResultKept }
internal enum DeleuzeDifferenceAction { None, DifferenceRepetition, Rhizome, KeepSchelling, Leave }
internal enum DeleuzeDifferenceStandard { None, Difference, Rhizome, Schellingian }

/// <summary>
/// Deleuze's third-act resolution on the being-and-change line. Schelling's
/// second-act room is the prerequisite; the act's single resolution is a
/// question edge (difference and repetition) or keeping Schelling.
/// </summary>
internal sealed class DeleuzeDifferenceState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "FRIEDRICH_WILHELM_JOSEPH_SCHELLING__BEING_AND_CHANGE__CORE";
    public const string AdoptedNodeId = "GILLES_DELEUZE__BEING_AND_CHANGE__CORE";
    public const string RequiredEdgeId = "";
    public const string CompletionEdgeId = "WESTERN_EDGE_058";
    public const string FixedRequiredEdgeId = "WESTERN_EDGE_162";
    public const string FixedCompletionEdgeId = "WESTERN_EDGE_157";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int ActIndex { get; set; } = 3;
    public string EdgeId { get; set; } = CompletionEdgeId;
    public bool FixedMode { get; set; }
    public DeleuzeDifferenceStage Stage { get; set; }
    public DeleuzeDifferenceAction PendingAction { get; set; }
    public List<DeleuzeDifferenceAction> ConfirmedActions { get; set; } = [];
    public DeleuzeDifferenceStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static DeleuzeDifferenceState Create(string instanceId) => new() { InstanceId = instanceId };

    public static DeleuzeDifferenceState CreateAt(string instanceId, int actIndex, string edgeId, bool fixedMode) =>
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
            || Closed && PendingAction != DeleuzeDifferenceAction.None
            || PendingAction != DeleuzeDifferenceAction.None
                && !DeleuzeDifferenceFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId, FixedMode);
        foreach (DeleuzeDifferenceAction action in ConfirmedActions)
        {
            if (replay.Closed || !DeleuzeDifferenceFlow.Options(replay.Stage).Contains(action)) return false;
            DeleuzeDifferenceFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && FixedDeclined == replay.FixedDeclined
            && Closed == replay.Closed;
    }
}

internal static class DeleuzeDifferenceFlow
{
    public static IReadOnlyList<DeleuzeDifferenceAction> Options(DeleuzeDifferenceStage stage) => stage switch
    {
        DeleuzeDifferenceStage.Invitation =>
            [DeleuzeDifferenceAction.DifferenceRepetition, DeleuzeDifferenceAction.Rhizome,
                DeleuzeDifferenceAction.KeepSchelling],
        DeleuzeDifferenceStage.ResultDifference or DeleuzeDifferenceStage.ResultRhizome
            or DeleuzeDifferenceStage.ResultKept => [DeleuzeDifferenceAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(DeleuzeDifferenceState state, DeleuzeDifferenceAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != DeleuzeDifferenceAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(DeleuzeDifferenceState state, DeleuzeDifferenceAction action)
    {
        switch (action)
        {
            case DeleuzeDifferenceAction.DifferenceRepetition:
                state.Standard = DeleuzeDifferenceStandard.Difference;
                state.EdgeCommitted = true;
                state.Stage = DeleuzeDifferenceStage.ResultDifference;
                break;
            case DeleuzeDifferenceAction.Rhizome:
                state.Standard = DeleuzeDifferenceStandard.Rhizome;
                state.EdgeCommitted = true;
                state.Stage = DeleuzeDifferenceStage.ResultRhizome;
                break;
            case DeleuzeDifferenceAction.KeepSchelling:
                state.Standard = DeleuzeDifferenceStandard.Schellingian;
                if (state.FixedMode) state.FixedDeclined = true;
                else state.QuestionDeclined = true;
                state.Stage = DeleuzeDifferenceStage.ResultKept;
                break;
            case DeleuzeDifferenceAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = DeleuzeDifferenceAction.None;
    }

    public static bool TryCommitPending(DeleuzeDifferenceState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == DeleuzeDifferenceAction.None) return false;
        DeleuzeDifferenceAction action = state.PendingAction;
        bool adopts = action is DeleuzeDifferenceAction.DifferenceRepetition or DeleuzeDifferenceAction.Rhizome;
        bool declines = action == DeleuzeDifferenceAction.KeepSchelling;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && state.FixedMode && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        if (declines && !state.FixedMode && !journey.ResolvedQuestionActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Question", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(DeleuzeDifferenceState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        string required = state.FixedMode ? DeleuzeDifferenceState.FixedRequiredEdgeId : DeleuzeDifferenceState.RequiredEdgeId;
        if (required.Length > 0 && !journey.CompletedEdgeIds.Contains(required)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        bool declined = state.FixedMode
            ? journey.DeclinedFixedActs.Contains(state.ActIndex)
            : journey.ResolvedQuestionActs.Contains(state.ActIndex);
        if (!state.EdgeCommitted && !declined)
        {
            if (state.FixedMode)
                return journey.LastFixedAct is 2 or 3 && !adopted
                    && journey.CurrentNodeId == DeleuzeDifferenceState.SourceNodeId;
            return journey.LastFixedAct == 3 && !adopted
                && journey.CurrentNodeId == DeleuzeDifferenceState.SourceNodeId;
        }
        if (state.EdgeCommitted)
            return adopted && journey.LastFixedAct == 3 && (state.FixedMode || declined)
                && journey.CurrentNodeId == DeleuzeDifferenceState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && declined
            && journey.CurrentNodeId == DeleuzeDifferenceState.SourceNodeId;
    }

    public static string DescriptionKey(DeleuzeDifferenceState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(DeleuzeDifferenceStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(DeleuzeDifferenceAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
