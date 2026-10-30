using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HabermasCommunicationStage { Invitation, ResultAction, ResultEthics, ResultKept }
internal enum HabermasCommunicationAction { None, CommunicativeAction, DiscourseEthics, KeepHorkheimer, Leave }
internal enum HabermasCommunicationStandard { None, Action, Ethics, Horkheimerian }

/// <summary>
/// Habermas's third-act resolution on the social-critique line. Horkheimer's
/// second-act room is the prerequisite; the act's single resolution is a
/// question edge (communicative action) or keeping Horkheimer.
/// </summary>
internal sealed class HabermasCommunicationState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "MAX_HORKHEIMER__HISTORY_AND_POWER__CORE";
    public const string AdoptedNodeId = "JURGEN_HABERMAS__HISTORY_AND_POWER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_084";
    public const string CompletionEdgeId = "WESTERN_EDGE_086";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public HabermasCommunicationStage Stage { get; set; }
    public HabermasCommunicationAction PendingAction { get; set; }
    public List<HabermasCommunicationAction> ConfirmedActions { get; set; } = [];
    public HabermasCommunicationStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static HabermasCommunicationState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != HabermasCommunicationAction.None
            || PendingAction != HabermasCommunicationAction.None
                && !HabermasCommunicationFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (HabermasCommunicationAction action in ConfirmedActions)
        {
            if (replay.Closed || !HabermasCommunicationFlow.Options(replay.Stage).Contains(action)) return false;
            HabermasCommunicationFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class HabermasCommunicationFlow
{
    public static IReadOnlyList<HabermasCommunicationAction> Options(HabermasCommunicationStage stage) => stage switch
    {
        HabermasCommunicationStage.Invitation =>
            [HabermasCommunicationAction.CommunicativeAction, HabermasCommunicationAction.DiscourseEthics,
                HabermasCommunicationAction.KeepHorkheimer],
        HabermasCommunicationStage.ResultAction or HabermasCommunicationStage.ResultEthics
            or HabermasCommunicationStage.ResultKept => [HabermasCommunicationAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(HabermasCommunicationState state, HabermasCommunicationAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != HabermasCommunicationAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(HabermasCommunicationState state, HabermasCommunicationAction action)
    {
        switch (action)
        {
            case HabermasCommunicationAction.CommunicativeAction:
                state.Standard = HabermasCommunicationStandard.Action;
                state.EdgeCommitted = true;
                state.Stage = HabermasCommunicationStage.ResultAction;
                break;
            case HabermasCommunicationAction.DiscourseEthics:
                state.Standard = HabermasCommunicationStandard.Ethics;
                state.EdgeCommitted = true;
                state.Stage = HabermasCommunicationStage.ResultEthics;
                break;
            case HabermasCommunicationAction.KeepHorkheimer:
                state.Standard = HabermasCommunicationStandard.Horkheimerian;
                state.QuestionDeclined = true;
                state.Stage = HabermasCommunicationStage.ResultKept;
                break;
            case HabermasCommunicationAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = HabermasCommunicationAction.None;
    }

    public static bool TryCommitPending(HabermasCommunicationState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == HabermasCommunicationAction.None) return false;
        HabermasCommunicationAction action = state.PendingAction;
        bool adopts = action is HabermasCommunicationAction.CommunicativeAction or HabermasCommunicationAction.DiscourseEthics;
        bool declines = action == HabermasCommunicationAction.KeepHorkheimer;
        if (adopts && !journey.CompletedEdgeIds.Contains(HabermasCommunicationState.CompletionEdgeId)
            && !graph.TryApply(journey, HabermasCommunicationState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(3)
            && !graph.TryDecline(journey, "Question", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HabermasCommunicationState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(HabermasCommunicationState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(HabermasCommunicationState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !adopted && !journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == HabermasCommunicationState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 3
                && journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == HabermasCommunicationState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.ResolvedQuestionActs.Contains(3)
            && journey.CurrentNodeId == HabermasCommunicationState.SourceNodeId;
    }

    public static string DescriptionKey(HabermasCommunicationState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(HabermasCommunicationStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HabermasCommunicationAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
