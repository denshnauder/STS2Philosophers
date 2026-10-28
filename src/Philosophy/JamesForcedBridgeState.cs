namespace STS2Philosophers;

internal enum JamesForcedBridgeStage
{
    Assessment,
    Evidence,
    ResultCrossed,
    ResultDetoured,
    ResultRefused,
}

internal enum JamesForcedBridgeAction
{
    None,
    CheckConditions,
    AskKeeper,
    CrossBridge,
    TakeDetour,
    RefuseChoice,
    Leave,
}

internal enum JamesForcedBridgeOutcome
{
    None,
    Crossed,
    Detoured,
    Refused,
}

internal sealed class JamesForcedBridgeState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "PLATO__VIRTUE_AND_HAPPINESS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_010";
    public const string RouteEdgeId = "WESTERN_EDGE_011";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public JamesForcedBridgeStage Stage { get; set; }
    public JamesForcedBridgeAction PendingAction { get; set; }
    public List<JamesForcedBridgeAction> ConfirmedActions { get; set; } = [];
    public bool ForcedAssessment { get; set; }
    public bool MomentousAssessment { get; set; }
    public bool BothFeasible { get; set; }
    public bool NewEvidenceSeen { get; set; }
    public JamesForcedBridgeOutcome Outcome { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public bool Closed { get; set; }

    public static JamesForcedBridgeState Create(string instanceId, string runId) => new()
    {
        InstanceId = instanceId,
        RunId = runId,
        Stage = JamesForcedBridgeStage.Assessment,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        RunId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || string.IsNullOrWhiteSpace(RunId) || !Enum.IsDefined(Stage)
            || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Outcome)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || (CostAttempted && Outcome == JamesForcedBridgeOutcome.None)
            || (CostPaid && !CostAttempted)
            || ForcedAssessment != MomentousAssessment
            || BothFeasible != (Stage != JamesForcedBridgeStage.Assessment)
            || (ForcedAssessment && NewEvidenceSeen)
            || (ForcedAssessment && !BothFeasible)
            || (NewEvidenceSeen && !BothFeasible)
            || Outcome switch
            {
                JamesForcedBridgeOutcome.None => Stage is not (JamesForcedBridgeStage.Assessment
                    or JamesForcedBridgeStage.Evidence),
                JamesForcedBridgeOutcome.Crossed => Stage != JamesForcedBridgeStage.ResultCrossed,
                JamesForcedBridgeOutcome.Detoured => Stage != JamesForcedBridgeStage.ResultDetoured,
                JamesForcedBridgeOutcome.Refused => Stage != JamesForcedBridgeStage.ResultRefused,
                _ => true,
            }) return false;

        JamesForcedBridgeStage replay = JamesForcedBridgeStage.Assessment;
        bool closed = false;
        foreach (JamesForcedBridgeAction action in ConfirmedActions)
        {
            if (closed || !JamesForcedBridgeFlow.Options(replay).Contains(action)) return false;
            replay = JamesForcedBridgeFlow.Next(replay, action);
            closed = action == JamesForcedBridgeAction.Leave;
        }
        return replay == Stage && closed == Closed
            && (PendingAction == JamesForcedBridgeAction.None
                || !Closed && JamesForcedBridgeFlow.Options(Stage).Contains(PendingAction));
    }

    public decimal Cost => Outcome == JamesForcedBridgeOutcome.Detoured ? 10 : 0;
}

internal static class JamesForcedBridgeFlow
{
    public static bool TryPrepare(JamesForcedBridgeState state, JamesForcedBridgeAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != JamesForcedBridgeAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(JamesForcedBridgeState state, WesternJourneyState journey,
        ZenoRouteState route)
    {
        if (!IsRouteConsistent(state, journey, route)
            || state.PendingAction == JamesForcedBridgeAction.None
            || !Options(state.Stage).Contains(state.PendingAction)) return false;
        JamesForcedBridgeAction action = state.PendingAction;
        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = JamesForcedBridgeAction.None;
        return true;
    }

    public static IReadOnlyList<JamesForcedBridgeAction> Options(JamesForcedBridgeStage stage) =>
        stage switch
        {
            JamesForcedBridgeStage.Assessment =>
                [JamesForcedBridgeAction.CheckConditions, JamesForcedBridgeAction.AskKeeper],
            JamesForcedBridgeStage.Evidence =>
                [JamesForcedBridgeAction.CrossBridge, JamesForcedBridgeAction.TakeDetour,
                    JamesForcedBridgeAction.RefuseChoice],
            JamesForcedBridgeStage.ResultCrossed or
            JamesForcedBridgeStage.ResultDetoured or
            JamesForcedBridgeStage.ResultRefused => [JamesForcedBridgeAction.Leave],
            _ => [],
        };

    public static JamesForcedBridgeStage Next(JamesForcedBridgeStage stage, JamesForcedBridgeAction action) =>
        (stage, action) switch
        {
            (JamesForcedBridgeStage.Assessment, JamesForcedBridgeAction.CheckConditions)
                or (JamesForcedBridgeStage.Assessment, JamesForcedBridgeAction.AskKeeper) =>
                JamesForcedBridgeStage.Evidence,
            (JamesForcedBridgeStage.Evidence, JamesForcedBridgeAction.CrossBridge) =>
                JamesForcedBridgeStage.ResultCrossed,
            (JamesForcedBridgeStage.Evidence, JamesForcedBridgeAction.TakeDetour) =>
                JamesForcedBridgeStage.ResultDetoured,
            (JamesForcedBridgeStage.Evidence, JamesForcedBridgeAction.RefuseChoice) =>
                JamesForcedBridgeStage.ResultRefused,
            (JamesForcedBridgeStage.ResultCrossed or JamesForcedBridgeStage.ResultDetoured
                or JamesForcedBridgeStage.ResultRefused, JamesForcedBridgeAction.Leave) => stage,
            _ => throw new InvalidOperationException($"Invalid James bridge transition {stage}/{action}."),
        };

    public static bool IsRouteConsistent(JamesForcedBridgeState state, WesternJourneyState journey,
        ZenoRouteState? route)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || journey.CurrentNodeId != JamesForcedBridgeState.SourceNodeId
            || journey.LastFixedAct != 2
            || !journey.ResolvedQuestionActs.Contains(2)
            || journey.CompletedEdgeIds.Contains(JamesForcedBridgeState.RouteEdgeId)
            || route is not { Stage: ZenoRouteStage.DiogenesClosed }
            || route.RouteEdgeId != JamesForcedBridgeState.RouteEdgeId
            || route.RunId != state.RunId) return false;
        return state.Outcome == JamesForcedBridgeOutcome.None
            || state.ConfirmedActions.Contains(state.Outcome switch
            {
                JamesForcedBridgeOutcome.Crossed => JamesForcedBridgeAction.CrossBridge,
                JamesForcedBridgeOutcome.Detoured => JamesForcedBridgeAction.TakeDetour,
                JamesForcedBridgeOutcome.Refused => JamesForcedBridgeAction.RefuseChoice,
                _ => JamesForcedBridgeAction.None,
            });
    }

    public static string StageKey(JamesForcedBridgeStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(JamesForcedBridgeAction action) => ToUpperSnake(action.ToString());
    public static string DescriptionKey(JamesForcedBridgeState state) => state.Stage switch
    {
        JamesForcedBridgeStage.Assessment => "ASSESSMENT",
        JamesForcedBridgeStage.Evidence => "EVIDENCE",
        JamesForcedBridgeStage.ResultCrossed => "RESULT_CROSSED",
        JamesForcedBridgeStage.ResultDetoured => "RESULT_DETOURED",
        JamesForcedBridgeStage.ResultRefused => "RESULT_REFUSED",
        _ => "ASSESSMENT",
    };

    private static void Apply(JamesForcedBridgeState state, JamesForcedBridgeAction action)
    {
        switch (action)
        {
            case JamesForcedBridgeAction.CheckConditions:
                state.ForcedAssessment = true;
                state.MomentousAssessment = true;
                state.BothFeasible = true;
                state.Stage = JamesForcedBridgeStage.Evidence;
                break;
            case JamesForcedBridgeAction.AskKeeper:
                state.BothFeasible = true;
                state.NewEvidenceSeen = true;
                state.Stage = JamesForcedBridgeStage.Evidence;
                break;
            case JamesForcedBridgeAction.CrossBridge:
                state.Outcome = JamesForcedBridgeOutcome.Crossed;
                state.Stage = JamesForcedBridgeStage.ResultCrossed;
                break;
            case JamesForcedBridgeAction.TakeDetour:
                state.Outcome = JamesForcedBridgeOutcome.Detoured;
                state.Stage = JamesForcedBridgeStage.ResultDetoured;
                break;
            case JamesForcedBridgeAction.RefuseChoice:
                state.Outcome = JamesForcedBridgeOutcome.Refused;
                state.Stage = JamesForcedBridgeStage.ResultRefused;
                break;
            case JamesForcedBridgeAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
