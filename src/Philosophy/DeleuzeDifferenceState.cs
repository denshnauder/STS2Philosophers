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

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public DeleuzeDifferenceStage Stage { get; set; }
    public DeleuzeDifferenceAction PendingAction { get; set; }
    public List<DeleuzeDifferenceAction> ConfirmedActions { get; set; } = [];
    public DeleuzeDifferenceStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static DeleuzeDifferenceState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != DeleuzeDifferenceAction.None
            || PendingAction != DeleuzeDifferenceAction.None
                && !DeleuzeDifferenceFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (DeleuzeDifferenceAction action in ConfirmedActions)
        {
            if (replay.Closed || !DeleuzeDifferenceFlow.Options(replay.Stage).Contains(action)) return false;
            DeleuzeDifferenceFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
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
                state.QuestionDeclined = true;
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
        if (adopts && !journey.CompletedEdgeIds.Contains(DeleuzeDifferenceState.CompletionEdgeId)
            && !graph.TryApply(journey, DeleuzeDifferenceState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(3)
            && !graph.TryDecline(journey, "Question", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(DeleuzeDifferenceState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || DeleuzeDifferenceState.RequiredEdgeId.Length > 0
                && !journey.CompletedEdgeIds.Contains(DeleuzeDifferenceState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(DeleuzeDifferenceState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !adopted && !journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == DeleuzeDifferenceState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 3
                && journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == DeleuzeDifferenceState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.ResolvedQuestionActs.Contains(3)
            && journey.CurrentNodeId == DeleuzeDifferenceState.SourceNodeId;
    }

    public static string DescriptionKey(DeleuzeDifferenceState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(DeleuzeDifferenceStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(DeleuzeDifferenceAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
