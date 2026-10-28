using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum KantKnowledgeSynthesisStage
{
    Invitation,
    Evidence,
    ResultSynthetic,
    ResultLimit,
    ResultKept,
}

internal enum KantKnowledgeSynthesisAction
{
    None,
    InspectHume,
    Synthesize,
    DrawLimit,
    KeepHume,
    Leave,
}

internal enum KantKnowledgeJudgment
{
    None,
    Synthetic,
    Limit,
}

internal sealed class KantKnowledgeSynthesisState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "DESCARTES__KNOWLEDGE_AND_DOUBT__CORE";
    public const string AdoptedNodeId = "IMMANUEL_KANT__KNOWLEDGE_AND_DOUBT__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_014";
    public const string CompletionEdgeId = "WESTERN_EDGE_015";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public KantKnowledgeSynthesisStage Stage { get; set; }
    public KantKnowledgeSynthesisAction PendingAction { get; set; }
    public List<KantKnowledgeSynthesisAction> ConfirmedActions { get; set; } = [];
    public bool EvidenceChecked { get; set; }
    public KantKnowledgeJudgment Judgment { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static KantKnowledgeSynthesisState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || !Enum.IsDefined(Judgment) || ConfirmedActions is null
            || ConfirmedActions.Count > 4 || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != KantKnowledgeSynthesisAction.None
            || PendingAction != KantKnowledgeSynthesisAction.None
                && !KantKnowledgeSynthesisFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = Create(InstanceId);
        foreach (KantKnowledgeSynthesisAction action in ConfirmedActions)
        {
            if (replay.Closed || !KantKnowledgeSynthesisFlow.Options(replay.Stage).Contains(action)) return false;
            KantKnowledgeSynthesisFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && EvidenceChecked == replay.EvidenceChecked
            && Judgment == replay.Judgment && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class KantKnowledgeSynthesisFlow
{
    public static IReadOnlyList<KantKnowledgeSynthesisAction> Options(KantKnowledgeSynthesisStage stage) => stage switch
    {
        KantKnowledgeSynthesisStage.Invitation =>
            [KantKnowledgeSynthesisAction.InspectHume, KantKnowledgeSynthesisAction.KeepHume],
        KantKnowledgeSynthesisStage.Evidence =>
            [KantKnowledgeSynthesisAction.Synthesize, KantKnowledgeSynthesisAction.DrawLimit],
        KantKnowledgeSynthesisStage.ResultSynthetic or KantKnowledgeSynthesisStage.ResultLimit
            or KantKnowledgeSynthesisStage.ResultKept => [KantKnowledgeSynthesisAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(KantKnowledgeSynthesisState state, KantKnowledgeSynthesisAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != KantKnowledgeSynthesisAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(KantKnowledgeSynthesisState state, KantKnowledgeSynthesisAction action)
    {
        switch (action)
        {
            case KantKnowledgeSynthesisAction.InspectHume:
                state.EvidenceChecked = true;
                state.Stage = KantKnowledgeSynthesisStage.Evidence;
                break;
            case KantKnowledgeSynthesisAction.Synthesize:
                state.Judgment = KantKnowledgeJudgment.Synthetic;
                state.EdgeCommitted = true;
                state.Stage = KantKnowledgeSynthesisStage.ResultSynthetic;
                break;
            case KantKnowledgeSynthesisAction.DrawLimit:
                state.Judgment = KantKnowledgeJudgment.Limit;
                state.EdgeCommitted = true;
                state.Stage = KantKnowledgeSynthesisStage.ResultLimit;
                break;
            case KantKnowledgeSynthesisAction.KeepHume:
                state.FixedDeclined = true;
                state.Stage = KantKnowledgeSynthesisStage.ResultKept;
                break;
            case KantKnowledgeSynthesisAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = KantKnowledgeSynthesisAction.None;
    }

    public static bool TryCommitPending(KantKnowledgeSynthesisState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == KantKnowledgeSynthesisAction.None)
            return false;
        KantKnowledgeSynthesisAction action = state.PendingAction;
        bool adopts = action is KantKnowledgeSynthesisAction.Synthesize or KantKnowledgeSynthesisAction.DrawLimit;
        bool declines = action == KantKnowledgeSynthesisAction.KeepHume;
        if (adopts && !journey.CompletedEdgeIds.Contains(KantKnowledgeSynthesisState.CompletionEdgeId)
            && !graph.TryApply(journey, KantKnowledgeSynthesisState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(KantKnowledgeSynthesisState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(KantKnowledgeSynthesisState.RequiredEdgeId)
            || !journey.ResolvedQuestionActs.Contains(2)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(KantKnowledgeSynthesisState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted
                && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == KantKnowledgeSynthesisState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == KantKnowledgeSynthesisState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.CurrentNodeId == KantKnowledgeSynthesisState.SourceNodeId
            && journey.DeclinedFixedActs.Contains(3);
    }

    public static string DescriptionKey(KantKnowledgeSynthesisState state) => state.Stage switch
    {
        KantKnowledgeSynthesisStage.Invitation => "INVITATION",
        KantKnowledgeSynthesisStage.Evidence => "EVIDENCE",
        KantKnowledgeSynthesisStage.ResultSynthetic => "RESULT_SYNTHETIC",
        KantKnowledgeSynthesisStage.ResultLimit => "RESULT_LIMIT",
        KantKnowledgeSynthesisStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(KantKnowledgeSynthesisStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(KantKnowledgeSynthesisAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
