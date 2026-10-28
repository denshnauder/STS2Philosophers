using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum MillHarmStage { Invitation, Test, ResultConduct, ResultOffense, ResultKept }
internal enum MillHarmAction { None, ApplyHarmTest, ProtectConduct, CountOffense, KeepWollstonecraft, Leave }
internal enum MillHarmStandard { None, Conduct, Offense }

internal sealed class MillHarmState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "MARY_WOLLSTONECRAFT__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_040";
    public const string CompletionEdgeId = "WESTERN_EDGE_041";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public MillHarmStage Stage { get; set; }
    public MillHarmAction PendingAction { get; set; }
    public List<MillHarmAction> ConfirmedActions { get; set; } = [];
    public MillHarmStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static MillHarmState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != MillHarmAction.None
            || PendingAction != MillHarmAction.None
                && !MillHarmFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (MillHarmAction action in ConfirmedActions)
        {
            if (replay.Closed || !MillHarmFlow.Options(replay.Stage).Contains(action)) return false;
            MillHarmFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class MillHarmFlow
{
    public static IReadOnlyList<MillHarmAction> Options(MillHarmStage stage) => stage switch
    {
        MillHarmStage.Invitation => [MillHarmAction.ApplyHarmTest, MillHarmAction.KeepWollstonecraft],
        MillHarmStage.Test => [MillHarmAction.ProtectConduct, MillHarmAction.CountOffense],
        MillHarmStage.ResultConduct or MillHarmStage.ResultOffense or MillHarmStage.ResultKept => [MillHarmAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(MillHarmState state, MillHarmAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != MillHarmAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(MillHarmState state, MillHarmAction action)
    {
        switch (action)
        {
            case MillHarmAction.ApplyHarmTest: state.Stage = MillHarmStage.Test; break;
            case MillHarmAction.ProtectConduct:
                state.Standard = MillHarmStandard.Conduct; state.EdgeCommitted = true;
                state.Stage = MillHarmStage.ResultConduct; break;
            case MillHarmAction.CountOffense:
                state.Standard = MillHarmStandard.Offense; state.EdgeCommitted = true;
                state.Stage = MillHarmStage.ResultOffense; break;
            case MillHarmAction.KeepWollstonecraft:
                state.QuestionDeclined = true; state.Stage = MillHarmStage.ResultKept; break;
            case MillHarmAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = MillHarmAction.None;
    }

    public static bool TryCommitPending(MillHarmState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == MillHarmAction.None) return false;
        MillHarmAction action = state.PendingAction;
        bool adopts = action is MillHarmAction.ProtectConduct or MillHarmAction.CountOffense;
        bool declines = action == MillHarmAction.KeepWollstonecraft;
        if (adopts && !journey.CompletedEdgeIds.Contains(MillHarmState.CompletionEdgeId)
            && !graph.TryApply(journey, MillHarmState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(2)
            && !graph.TryDecline(journey, "Question", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(MillHarmState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(MillHarmState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(MillHarmState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.ResolvedQuestionActs.Contains(2)
                && journey.CurrentNodeId == MillHarmState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 2
                && journey.ResolvedQuestionActs.Contains(2)
                && journey.CurrentNodeId == MillHarmState.SourceNodeId;
        return !adopted && journey.LastFixedAct == 2
            && journey.CurrentNodeId == MillHarmState.SourceNodeId
            && journey.ResolvedQuestionActs.Contains(2);
    }

    public static string DescriptionKey(MillHarmState state) => state.Stage switch
    {
        MillHarmStage.Invitation => "INVITATION",
        MillHarmStage.Test => "TEST",
        MillHarmStage.ResultConduct => "RESULT_CONDUCT",
        MillHarmStage.ResultOffense => "RESULT_OFFENSE",
        MillHarmStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(MillHarmStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(MillHarmAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
