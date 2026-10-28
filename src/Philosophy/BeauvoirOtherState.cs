using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum BeauvoirOtherStage { Invitation, Testimony, ResultSubject, ResultAmbiguity, ResultKept }
internal enum BeauvoirOtherAction { None, HearTestimony, RecognizeSubject, RecordAmbiguity, KeepWollstonecraft, Leave }
internal enum BeauvoirJudgment { None, Subject, Ambiguity }

internal sealed class BeauvoirOtherState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "MARY_WOLLSTONECRAFT__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string AdoptedNodeId = "SIMONE_DE_BEAUVOIR__SELF_AND_OTHER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_041";
    public const string CompletionEdgeId = "WESTERN_EDGE_042";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public BeauvoirOtherStage Stage { get; set; }
    public BeauvoirOtherAction PendingAction { get; set; }
    public List<BeauvoirOtherAction> ConfirmedActions { get; set; } = [];
    public BeauvoirJudgment Judgment { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static BeauvoirOtherState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad() { InstanceId ??= string.Empty; ConfirmedActions ??= []; }
    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Judgment)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != BeauvoirOtherAction.None
            || PendingAction != BeauvoirOtherAction.None
                && !BeauvoirOtherFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (BeauvoirOtherAction action in ConfirmedActions)
        {
            if (replay.Closed || !BeauvoirOtherFlow.Options(replay.Stage).Contains(action)) return false;
            BeauvoirOtherFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Judgment == replay.Judgment && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class BeauvoirOtherFlow
{
    public static IReadOnlyList<BeauvoirOtherAction> Options(BeauvoirOtherStage stage) => stage switch
    {
        BeauvoirOtherStage.Invitation => [BeauvoirOtherAction.HearTestimony, BeauvoirOtherAction.KeepWollstonecraft],
        BeauvoirOtherStage.Testimony => [BeauvoirOtherAction.RecognizeSubject, BeauvoirOtherAction.RecordAmbiguity],
        BeauvoirOtherStage.ResultSubject or BeauvoirOtherStage.ResultAmbiguity or BeauvoirOtherStage.ResultKept =>
            [BeauvoirOtherAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(BeauvoirOtherState state, BeauvoirOtherAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != BeauvoirOtherAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(BeauvoirOtherState state, BeauvoirOtherAction action)
    {
        switch (action)
        {
            case BeauvoirOtherAction.HearTestimony: state.Stage = BeauvoirOtherStage.Testimony; break;
            case BeauvoirOtherAction.RecognizeSubject:
                state.Judgment = BeauvoirJudgment.Subject; state.EdgeCommitted = true;
                state.Stage = BeauvoirOtherStage.ResultSubject; break;
            case BeauvoirOtherAction.RecordAmbiguity:
                state.Judgment = BeauvoirJudgment.Ambiguity; state.EdgeCommitted = true;
                state.Stage = BeauvoirOtherStage.ResultAmbiguity; break;
            case BeauvoirOtherAction.KeepWollstonecraft:
                state.FixedDeclined = true; state.Stage = BeauvoirOtherStage.ResultKept; break;
            case BeauvoirOtherAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = BeauvoirOtherAction.None;
    }

    public static bool TryCommitPending(BeauvoirOtherState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == BeauvoirOtherAction.None) return false;
        BeauvoirOtherAction action = state.PendingAction;
        bool adopts = action is BeauvoirOtherAction.RecognizeSubject or BeauvoirOtherAction.RecordAmbiguity;
        bool declines = action == BeauvoirOtherAction.KeepWollstonecraft;
        if (adopts && !journey.CompletedEdgeIds.Contains(BeauvoirOtherState.CompletionEdgeId)
            && !graph.TryApply(journey, BeauvoirOtherState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(BeauvoirOtherState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(BeauvoirOtherState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(BeauvoirOtherState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == BeauvoirOtherState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == BeauvoirOtherState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.CurrentNodeId == BeauvoirOtherState.SourceNodeId
            && journey.DeclinedFixedActs.Contains(3);
    }

    public static string DescriptionKey(BeauvoirOtherState state) => state.Stage switch
    {
        BeauvoirOtherStage.Invitation => "INVITATION",
        BeauvoirOtherStage.Testimony => "TESTIMONY",
        BeauvoirOtherStage.ResultSubject => "RESULT_SUBJECT",
        BeauvoirOtherStage.ResultAmbiguity => "RESULT_AMBIGUITY",
        BeauvoirOtherStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(BeauvoirOtherStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(BeauvoirOtherAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
