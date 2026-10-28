using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum LeibnizMonadComparisonStage { Invitation, Records, ResultSubstance, ResultMonads, ResultKept }
internal enum LeibnizMonadComparisonAction { None, CompareRecords, KeepSubstance, MarkMonads, KeepSpinoza, Leave }

internal sealed class LeibnizMonadComparisonState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_023";
    public const string CompletionEdgeId = "WESTERN_EDGE_024";
    public const string SeenThinkerId = "GOTTFRIED_WILHELM_LEIBNIZ";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public LeibnizMonadComparisonStage Stage { get; set; }
    public LeibnizMonadComparisonAction PendingAction { get; set; }
    public List<LeibnizMonadComparisonAction> ConfirmedActions { get; set; } = [];
    public bool RecordsCompared { get; set; }
    public bool MonadsRecorded { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static LeibnizMonadComparisonState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != LeibnizMonadComparisonAction.None
            || PendingAction != LeibnizMonadComparisonAction.None
                && !LeibnizMonadComparisonFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = Create(InstanceId);
        foreach (LeibnizMonadComparisonAction action in ConfirmedActions)
        {
            if (replay.Closed || !LeibnizMonadComparisonFlow.Options(replay.Stage).Contains(action)) return false;
            LeibnizMonadComparisonFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && RecordsCompared == replay.RecordsCompared
            && MonadsRecorded == replay.MonadsRecorded && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class LeibnizMonadComparisonFlow
{
    public static IReadOnlyList<LeibnizMonadComparisonAction> Options(LeibnizMonadComparisonStage stage) => stage switch
    {
        LeibnizMonadComparisonStage.Invitation =>
            [LeibnizMonadComparisonAction.CompareRecords, LeibnizMonadComparisonAction.KeepSpinoza],
        LeibnizMonadComparisonStage.Records =>
            [LeibnizMonadComparisonAction.KeepSubstance, LeibnizMonadComparisonAction.MarkMonads],
        LeibnizMonadComparisonStage.ResultSubstance or LeibnizMonadComparisonStage.ResultMonads
            or LeibnizMonadComparisonStage.ResultKept => [LeibnizMonadComparisonAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(LeibnizMonadComparisonState state, LeibnizMonadComparisonAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != LeibnizMonadComparisonAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(LeibnizMonadComparisonState state, LeibnizMonadComparisonAction action)
    {
        switch (action)
        {
            case LeibnizMonadComparisonAction.CompareRecords:
                state.RecordsCompared = true;
                state.Stage = LeibnizMonadComparisonStage.Records;
                break;
            case LeibnizMonadComparisonAction.KeepSubstance:
                state.EdgeCommitted = true;
                state.Stage = LeibnizMonadComparisonStage.ResultSubstance;
                break;
            case LeibnizMonadComparisonAction.MarkMonads:
                state.MonadsRecorded = true;
                state.EdgeCommitted = true;
                state.Stage = LeibnizMonadComparisonStage.ResultMonads;
                break;
            case LeibnizMonadComparisonAction.KeepSpinoza:
                state.QuestionDeclined = true;
                state.Stage = LeibnizMonadComparisonStage.ResultKept;
                break;
            case LeibnizMonadComparisonAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = LeibnizMonadComparisonAction.None;
    }

    public static bool TryCommitPending(LeibnizMonadComparisonState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == LeibnizMonadComparisonAction.None) return false;
        LeibnizMonadComparisonAction action = state.PendingAction;
        bool adopts = action is LeibnizMonadComparisonAction.KeepSubstance
            or LeibnizMonadComparisonAction.MarkMonads;
        bool declines = action == LeibnizMonadComparisonAction.KeepSpinoza;
        if (adopts && !journey.CompletedEdgeIds.Contains(LeibnizMonadComparisonState.CompletionEdgeId)
            && !graph.TryApply(journey, LeibnizMonadComparisonState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(2)
            && !graph.TryDecline(journey, "Question", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(LeibnizMonadComparisonState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(LeibnizMonadComparisonState.RequiredEdgeId)
            || journey.LastFixedAct != 2
            || journey.CurrentNodeId != LeibnizMonadComparisonState.SourceNodeId) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(LeibnizMonadComparisonState.CompletionEdgeId);
        bool resolved = journey.ResolvedQuestionActs.Contains(2);
        if (!state.EdgeCommitted && !state.QuestionDeclined) return !adopted && !resolved;
        if (state.EdgeCommitted) return !state.QuestionDeclined && adopted && resolved;
        return !adopted && resolved;
    }

    public static string DescriptionKey(LeibnizMonadComparisonState state) => state.Stage switch
    {
        LeibnizMonadComparisonStage.Invitation => "INVITATION",
        LeibnizMonadComparisonStage.Records => "RECORDS",
        LeibnizMonadComparisonStage.ResultSubstance => "RESULT_SUBSTANCE",
        LeibnizMonadComparisonStage.ResultMonads => "RESULT_MONADS",
        LeibnizMonadComparisonStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(LeibnizMonadComparisonStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(LeibnizMonadComparisonAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
