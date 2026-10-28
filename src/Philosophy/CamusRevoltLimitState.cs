namespace STS2Philosophers;

internal enum CamusRevoltLimitStage
{
    Situation,
    Proposal,
    ResultRestrained,
    ResultWithdrawn,
    ResultEscalated,
}

internal enum CamusRevoltLimitAction
{
    None,
    AcknowledgeLevy,
    QuestionLevy,
    RefuseHostage,
    WithdrawAid,
    AllowEscalation,
    Leave,
}

internal enum CamusRevoltLimitOutcome
{
    None,
    Restrained,
    Withdrawn,
    Escalated,
}

internal sealed class CamusRevoltLimitState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ZENO_OF_CITIUM__VIRTUE_AND_HAPPINESS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_011";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public CamusRevoltLimitStage Stage { get; set; }
    public CamusRevoltLimitAction PendingAction { get; set; }
    public List<CamusRevoltLimitAction> ConfirmedActions { get; set; } = [];
    public bool LevyAcknowledged { get; set; }
    public bool LevyQuestioned { get; set; }
    public CamusRevoltLimitOutcome Outcome { get; set; }
    public bool ThirdPartyHarmed { get; set; }
    public bool QuestionReserved { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public bool Closed { get; set; }

    public static CamusRevoltLimitState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = CamusRevoltLimitStage.Situation,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Outcome) || ConfirmedActions is null
            || ConfirmedActions.Count > 3
            || (CostAttempted && Outcome == CamusRevoltLimitOutcome.None)
            || (CostPaid && !CostAttempted)
            || (LevyAcknowledged && LevyQuestioned)
            || (LevyAcknowledged || LevyQuestioned) != (Stage != CamusRevoltLimitStage.Situation)
            || QuestionReserved != (Stage != CamusRevoltLimitStage.Situation)
            || ThirdPartyHarmed != (Outcome == CamusRevoltLimitOutcome.Escalated)
            || Outcome switch
            {
                CamusRevoltLimitOutcome.None => Stage is not (CamusRevoltLimitStage.Situation
                    or CamusRevoltLimitStage.Proposal),
                CamusRevoltLimitOutcome.Restrained => Stage != CamusRevoltLimitStage.ResultRestrained,
                CamusRevoltLimitOutcome.Withdrawn => Stage != CamusRevoltLimitStage.ResultWithdrawn,
                CamusRevoltLimitOutcome.Escalated => Stage != CamusRevoltLimitStage.ResultEscalated,
                _ => true,
            }) return false;

        CamusRevoltLimitStage replay = CamusRevoltLimitStage.Situation;
        bool replayAcknowledged = false, replayQuestioned = false, closed = false;
        foreach (CamusRevoltLimitAction action in ConfirmedActions)
        {
            if (closed || !CamusRevoltLimitFlow.Options(replay).Contains(action)) return false;
            replayAcknowledged |= action == CamusRevoltLimitAction.AcknowledgeLevy;
            replayQuestioned |= action == CamusRevoltLimitAction.QuestionLevy;
            replay = CamusRevoltLimitFlow.Next(replay, action);
            closed = action == CamusRevoltLimitAction.Leave;
        }
        return replay == Stage && closed == Closed
            && replayAcknowledged == LevyAcknowledged && replayQuestioned == LevyQuestioned
            && (PendingAction == CamusRevoltLimitAction.None
                || !Closed && CamusRevoltLimitFlow.Options(Stage).Contains(PendingAction));
    }

    public decimal Cost => Outcome switch
    {
        CamusRevoltLimitOutcome.Restrained => 6,
        CamusRevoltLimitOutcome.Withdrawn => 4,
        _ => 0,
    };
}

internal static class CamusRevoltLimitFlow
{
    public static bool TryPrepare(CamusRevoltLimitState state, CamusRevoltLimitAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != CamusRevoltLimitAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(CamusRevoltLimitState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey)
            || state.PendingAction == CamusRevoltLimitAction.None
            || !Options(state.Stage).Contains(state.PendingAction)) return false;
        if (!state.QuestionReserved)
        {
            if (!graph.TryReserveQuestion(journey, 3)) return false;
            state.QuestionReserved = true;
        }
        CamusRevoltLimitAction action = state.PendingAction;
        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = CamusRevoltLimitAction.None;
        return true;
    }

    public static IReadOnlyList<CamusRevoltLimitAction> Options(CamusRevoltLimitStage stage) =>
        stage switch
        {
            CamusRevoltLimitStage.Situation =>
                [CamusRevoltLimitAction.AcknowledgeLevy, CamusRevoltLimitAction.QuestionLevy],
            CamusRevoltLimitStage.Proposal =>
                [CamusRevoltLimitAction.RefuseHostage, CamusRevoltLimitAction.WithdrawAid,
                    CamusRevoltLimitAction.AllowEscalation],
            CamusRevoltLimitStage.ResultRestrained or
            CamusRevoltLimitStage.ResultWithdrawn or
            CamusRevoltLimitStage.ResultEscalated => [CamusRevoltLimitAction.Leave],
            _ => [],
        };

    public static CamusRevoltLimitStage Next(CamusRevoltLimitStage stage, CamusRevoltLimitAction action) =>
        (stage, action) switch
        {
            (CamusRevoltLimitStage.Situation, CamusRevoltLimitAction.AcknowledgeLevy)
                or (CamusRevoltLimitStage.Situation, CamusRevoltLimitAction.QuestionLevy) =>
                CamusRevoltLimitStage.Proposal,
            (CamusRevoltLimitStage.Proposal, CamusRevoltLimitAction.RefuseHostage) =>
                CamusRevoltLimitStage.ResultRestrained,
            (CamusRevoltLimitStage.Proposal, CamusRevoltLimitAction.WithdrawAid) =>
                CamusRevoltLimitStage.ResultWithdrawn,
            (CamusRevoltLimitStage.Proposal, CamusRevoltLimitAction.AllowEscalation) =>
                CamusRevoltLimitStage.ResultEscalated,
            (CamusRevoltLimitStage.ResultRestrained or CamusRevoltLimitStage.ResultWithdrawn
                or CamusRevoltLimitStage.ResultEscalated, CamusRevoltLimitAction.Leave) => stage,
            _ => throw new InvalidOperationException($"Invalid Camus transition {stage}/{action}."),
        };

    public static bool IsRouteConsistent(CamusRevoltLimitState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || journey.CurrentNodeId != CamusRevoltLimitState.SourceNodeId
            || journey.LastFixedAct != 3
            || !journey.CompletedEdgeIds.Contains(CamusRevoltLimitState.RequiredEdgeId)
            || journey.ResolvedQuestionActs.Contains(3) != state.QuestionReserved
            || (!state.QuestionReserved && state.Outcome != CamusRevoltLimitOutcome.None)) return false;
        return state.Outcome == CamusRevoltLimitOutcome.None
            || state.ConfirmedActions.Contains(state.Outcome switch
            {
                CamusRevoltLimitOutcome.Restrained => CamusRevoltLimitAction.RefuseHostage,
                CamusRevoltLimitOutcome.Withdrawn => CamusRevoltLimitAction.WithdrawAid,
                CamusRevoltLimitOutcome.Escalated => CamusRevoltLimitAction.AllowEscalation,
                _ => CamusRevoltLimitAction.None,
            });
    }

    public static string StageKey(CamusRevoltLimitStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(CamusRevoltLimitAction action) => ToUpperSnake(action.ToString());
    public static string DescriptionKey(CamusRevoltLimitState state) => state.Stage switch
    {
        CamusRevoltLimitStage.Situation => "SITUATION",
        CamusRevoltLimitStage.Proposal => "PROPOSAL",
        CamusRevoltLimitStage.ResultRestrained => "RESULT_RESTRAINED",
        CamusRevoltLimitStage.ResultWithdrawn => "RESULT_WITHDRAWN",
        CamusRevoltLimitStage.ResultEscalated => "RESULT_ESCALATED",
        _ => "SITUATION",
    };

    private static void Apply(CamusRevoltLimitState state, CamusRevoltLimitAction action)
    {
        switch (action)
        {
            case CamusRevoltLimitAction.AcknowledgeLevy:
                state.LevyAcknowledged = true;
                state.Stage = CamusRevoltLimitStage.Proposal;
                break;
            case CamusRevoltLimitAction.QuestionLevy:
                state.LevyQuestioned = true;
                state.Stage = CamusRevoltLimitStage.Proposal;
                break;
            case CamusRevoltLimitAction.RefuseHostage:
                state.Outcome = CamusRevoltLimitOutcome.Restrained;
                state.Stage = CamusRevoltLimitStage.ResultRestrained;
                break;
            case CamusRevoltLimitAction.WithdrawAid:
                state.Outcome = CamusRevoltLimitOutcome.Withdrawn;
                state.Stage = CamusRevoltLimitStage.ResultWithdrawn;
                break;
            case CamusRevoltLimitAction.AllowEscalation:
                state.Outcome = CamusRevoltLimitOutcome.Escalated;
                state.ThirdPartyHarmed = true;
                state.Stage = CamusRevoltLimitStage.ResultEscalated;
                break;
            case CamusRevoltLimitAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
