using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum FichteSelfPositingStage { Invitation, ResultSelfPosited, ResultCheck, ResultKept }
internal enum FichteSelfPositingAction { None, ReviseAsSelfPosited, RequireCheck, KeepKant, Leave }

internal sealed class FichteSelfPositingState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE";
    /// <summary>The act-two mainline reaches Kant's subject/autonomy face here.</summary>
    public const string ActTwoSourceNodeId = "IMMANUEL_KANT__SELF_AND_OTHER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_022";
    /// <summary>The third-act mainline reaches Kant's freedom/institutions face here.</summary>
    public const string ActThreeRequiredEdgeId = "WESTERN_EDGE_115";
    public const string CompletionEdgeId = "WESTERN_EDGE_037";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    /// <summary>1-based act the event was opened in; old saves normalize to three.</summary>
    public int ActIndex { get; set; } = 3;
    /// <summary>The fixed edge that placed Kant in this act (022 legacy, 095 on the act-two line).</summary>
    public string FixedEdgeId { get; set; } = RequiredEdgeId;
    public FichteSelfPositingStage Stage { get; set; }
    public FichteSelfPositingAction PendingAction { get; set; }
    public List<FichteSelfPositingAction> ConfirmedActions { get; set; } = [];
    public bool SelfPosited { get; set; }
    public bool CheckRequired { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static FichteSelfPositingState Create(string instanceId) =>
        CreateAt(instanceId, 3, RequiredEdgeId);

    public static FichteSelfPositingState CreateAt(string instanceId, int actIndex, string fixedEdgeId) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, FixedEdgeId = fixedEdgeId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        FixedEdgeId ??= RequiredEdgeId;
        if (ActIndex is < 2 or > 3) ActIndex = 3;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || ActIndex is < 2 or > 3 || string.IsNullOrWhiteSpace(FixedEdgeId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != FichteSelfPositingAction.None
            || PendingAction != FichteSelfPositingAction.None
                && !FichteSelfPositingFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (FichteSelfPositingAction action in ConfirmedActions)
        {
            if (replay.Closed || !FichteSelfPositingFlow.Options(replay.Stage).Contains(action)) return false;
            FichteSelfPositingFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && SelfPosited == replay.SelfPosited
            && CheckRequired == replay.CheckRequired && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class FichteSelfPositingFlow
{
    public static IReadOnlyList<FichteSelfPositingAction> Options(FichteSelfPositingStage stage) => stage switch
    {
        FichteSelfPositingStage.Invitation =>
            [FichteSelfPositingAction.ReviseAsSelfPosited, FichteSelfPositingAction.RequireCheck,
                FichteSelfPositingAction.KeepKant],
        FichteSelfPositingStage.ResultSelfPosited or FichteSelfPositingStage.ResultCheck
            or FichteSelfPositingStage.ResultKept => [FichteSelfPositingAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(FichteSelfPositingState state, FichteSelfPositingAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != FichteSelfPositingAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(FichteSelfPositingState state, FichteSelfPositingAction action)
    {
        switch (action)
        {
            case FichteSelfPositingAction.ReviseAsSelfPosited:
                state.SelfPosited = true;
                state.EdgeCommitted = true;
                state.Stage = FichteSelfPositingStage.ResultSelfPosited;
                break;
            case FichteSelfPositingAction.RequireCheck:
                state.CheckRequired = true;
                state.EdgeCommitted = true;
                state.Stage = FichteSelfPositingStage.ResultCheck;
                break;
            case FichteSelfPositingAction.KeepKant:
                state.QuestionDeclined = true;
                state.Stage = FichteSelfPositingStage.ResultKept;
                break;
            case FichteSelfPositingAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = FichteSelfPositingAction.None;
    }

    public static bool TryCommitPending(FichteSelfPositingState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == FichteSelfPositingAction.None)
            return false;
        FichteSelfPositingAction action = state.PendingAction;
        if (state.ActIndex == 3 && state.FixedEdgeId == FichteSelfPositingState.ActThreeRequiredEdgeId)
        {
            // The act-three Kant room treats Fichte as an inner batch: it spends
            // this act's question and keeps the run on Kant, without 037.
            if (!journey.ResolvedQuestionActs.Contains(3)
                && !graph.TryReserveQuestion(journey, 3)) return false;
            Apply(state, action);
            return IsRouteConsistent(state, journey);
        }
        if (state.ActIndex == 2)
        {
            // The act-two line treats this as an inner batch: it spends this
            // act's question and keeps the run on Kant, without a graph edge.
            if (!journey.ResolvedQuestionActs.Contains(2)
                && !graph.TryReserveQuestion(journey, 2)) return false;
            Apply(state, action);
            return IsRouteConsistent(state, journey);
        }
        bool revises = action is FichteSelfPositingAction.ReviseAsSelfPosited
            or FichteSelfPositingAction.RequireCheck;
        bool declines = action == FichteSelfPositingAction.KeepKant;
        if (revises && !journey.CompletedEdgeIds.Contains(FichteSelfPositingState.CompletionEdgeId)
            && !graph.TryApply(journey, FichteSelfPositingState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(2)
            && !graph.TryDecline(journey, "Question", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(FichteSelfPositingState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        if (state.ActIndex == 2)
        {
            if (!journey.CompletedEdgeIds.Contains(state.FixedEdgeId)) return false;
            bool answered = journey.ResolvedQuestionActs.Contains(2);
            if (!state.EdgeCommitted && !state.QuestionDeclined)
                return journey.LastFixedAct >= 2 && !answered
                    && journey.CurrentNodeId == FichteSelfPositingState.ActTwoSourceNodeId;
            if (state.EdgeCommitted)
                return !state.QuestionDeclined && answered && journey.LastFixedAct >= 2
                    && journey.CurrentNodeId == FichteSelfPositingState.ActTwoSourceNodeId;
            return !state.EdgeCommitted && answered && journey.LastFixedAct >= 2
                && journey.CurrentNodeId == FichteSelfPositingState.ActTwoSourceNodeId;
        }
        if (state.FixedEdgeId == FichteSelfPositingState.ActThreeRequiredEdgeId)
        {
            if (!journey.CompletedEdgeIds.Contains(FichteSelfPositingState.ActThreeRequiredEdgeId)) return false;
            bool answeredThree = journey.ResolvedQuestionActs.Contains(3);
            if (!state.EdgeCommitted && !state.QuestionDeclined)
                return journey.LastFixedAct == 3 && !answeredThree
                    && journey.CurrentNodeId == FichteSelfPositingState.SourceNodeId;
            if (state.EdgeCommitted)
                return !state.QuestionDeclined && answeredThree && journey.LastFixedAct == 3
                    && journey.CurrentNodeId == FichteSelfPositingState.SourceNodeId;
            return !state.EdgeCommitted && answeredThree && journey.LastFixedAct == 3
                && journey.CurrentNodeId == FichteSelfPositingState.SourceNodeId;
        }
        if (!journey.CompletedEdgeIds.Contains(FichteSelfPositingState.RequiredEdgeId)) return false;
        bool revised = journey.CompletedEdgeIds.Contains(FichteSelfPositingState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            // 037 is act two's question. It may be answered while the run stands
            // at position three, and it still only spends act two.
            return journey.LastFixedAct == 3 && !revised
                && !journey.ResolvedQuestionActs.Contains(2)
                && journey.CurrentNodeId == FichteSelfPositingState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && revised && journey.LastFixedAct == 3
                && journey.ResolvedQuestionActs.Contains(2)
                && journey.CurrentNodeId == FichteSelfPositingState.SourceNodeId;
        return !revised && journey.LastFixedAct == 3
            && journey.CurrentNodeId == FichteSelfPositingState.SourceNodeId
            && journey.ResolvedQuestionActs.Contains(2);
    }

    public static string DescriptionKey(FichteSelfPositingState state) => state.Stage switch
    {
        FichteSelfPositingStage.Invitation => "INVITATION",
        FichteSelfPositingStage.ResultSelfPosited => "RESULT_SELF_POSITED",
        FichteSelfPositingStage.ResultCheck => "RESULT_CHECK",
        FichteSelfPositingStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(FichteSelfPositingStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(FichteSelfPositingAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
