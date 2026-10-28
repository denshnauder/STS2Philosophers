namespace STS2Philosophers;

internal enum PopperFalsificationStage
{
    Reports,
    Evidence,
    ResultUnfalsifiable,
    ResultCalibrated,
    ResultCounterexample,
    ResultNotConclusive,
}

internal enum PopperFalsificationAction
{
    None,
    SelectBoundaryTest,
    SelectUnfalsifiableTest,
    CalibrateAndReport,
    PublishCounterexample,
    RefuseFinalVerdict,
    Leave,
}

internal enum PopperFalsificationOutcome
{
    None,
    Unfalsifiable,
    Calibrated,
    Counterexample,
    NotConclusive,
}

internal sealed class PopperFalsificationState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_048";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public PopperFalsificationStage Stage { get; set; }
    public PopperFalsificationAction PendingAction { get; set; }
    public List<PopperFalsificationAction> ConfirmedActions { get; set; } = [];
    public bool FalsifiableTest { get; set; }
    public bool InstrumentRiskKnown { get; set; }
    public bool CalibrationPaid { get; set; }
    public bool AuxiliaryUnknown { get; set; }
    public bool TheoryNotKilled { get; set; }
    public PopperFalsificationOutcome Outcome { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public bool Closed { get; set; }

    public static PopperFalsificationState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = PopperFalsificationStage.Reports,
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
            || (CostAttempted && Outcome == PopperFalsificationOutcome.None)
            || (CostPaid && !CostAttempted)
            || FalsifiableTest != (Stage is PopperFalsificationStage.Evidence
                or PopperFalsificationStage.ResultCalibrated
                or PopperFalsificationStage.ResultCounterexample
                or PopperFalsificationStage.ResultNotConclusive)
            || InstrumentRiskKnown != (Outcome == PopperFalsificationOutcome.Calibrated)
            || CalibrationPaid != (Outcome == PopperFalsificationOutcome.Calibrated)
            || AuxiliaryUnknown != (Outcome == PopperFalsificationOutcome.Counterexample)
            || TheoryNotKilled != (Outcome == PopperFalsificationOutcome.NotConclusive)
            || Outcome switch
            {
                PopperFalsificationOutcome.None => Stage is not (PopperFalsificationStage.Reports
                    or PopperFalsificationStage.Evidence),
                PopperFalsificationOutcome.Unfalsifiable =>
                    Stage != PopperFalsificationStage.ResultUnfalsifiable,
                PopperFalsificationOutcome.Calibrated =>
                    Stage != PopperFalsificationStage.ResultCalibrated,
                PopperFalsificationOutcome.Counterexample =>
                    Stage != PopperFalsificationStage.ResultCounterexample,
                PopperFalsificationOutcome.NotConclusive =>
                    Stage != PopperFalsificationStage.ResultNotConclusive,
                _ => true,
            }) return false;

        PopperFalsificationStage replay = PopperFalsificationStage.Reports;
        bool closed = false;
        foreach (PopperFalsificationAction action in ConfirmedActions)
        {
            if (closed || !PopperFalsificationFlow.Options(replay).Contains(action)) return false;
            replay = PopperFalsificationFlow.Next(replay, action);
            closed = action == PopperFalsificationAction.Leave;
        }
        return replay == Stage && closed == Closed
            && (PendingAction == PopperFalsificationAction.None
                || !Closed && PopperFalsificationFlow.Options(Stage).Contains(PendingAction));
    }

    public decimal Cost => Outcome == PopperFalsificationOutcome.Calibrated ? 12 : 0;
}

internal static class PopperFalsificationFlow
{
    public static bool TryPrepare(PopperFalsificationState state, PopperFalsificationAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != PopperFalsificationAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(PopperFalsificationState state, WesternJourneyState journey)
    {
        if (!IsRouteConsistent(state, journey)
            || state.PendingAction == PopperFalsificationAction.None
            || !Options(state.Stage).Contains(state.PendingAction)) return false;
        PopperFalsificationAction action = state.PendingAction;
        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = PopperFalsificationAction.None;
        return true;
    }

    public static IReadOnlyList<PopperFalsificationAction> Options(PopperFalsificationStage stage) =>
        stage switch
        {
            PopperFalsificationStage.Reports =>
                [PopperFalsificationAction.SelectBoundaryTest,
                    PopperFalsificationAction.SelectUnfalsifiableTest],
            PopperFalsificationStage.Evidence =>
                [PopperFalsificationAction.CalibrateAndReport,
                    PopperFalsificationAction.PublishCounterexample,
                    PopperFalsificationAction.RefuseFinalVerdict],
            PopperFalsificationStage.ResultUnfalsifiable or
            PopperFalsificationStage.ResultCalibrated or
            PopperFalsificationStage.ResultCounterexample or
            PopperFalsificationStage.ResultNotConclusive => [PopperFalsificationAction.Leave],
            _ => [],
        };

    public static PopperFalsificationStage Next(PopperFalsificationStage stage,
        PopperFalsificationAction action) => (stage, action) switch
        {
            (PopperFalsificationStage.Reports, PopperFalsificationAction.SelectBoundaryTest) =>
                PopperFalsificationStage.Evidence,
            (PopperFalsificationStage.Reports, PopperFalsificationAction.SelectUnfalsifiableTest) =>
                PopperFalsificationStage.ResultUnfalsifiable,
            (PopperFalsificationStage.Evidence, PopperFalsificationAction.CalibrateAndReport) =>
                PopperFalsificationStage.ResultCalibrated,
            (PopperFalsificationStage.Evidence, PopperFalsificationAction.PublishCounterexample) =>
                PopperFalsificationStage.ResultCounterexample,
            (PopperFalsificationStage.Evidence, PopperFalsificationAction.RefuseFinalVerdict) =>
                PopperFalsificationStage.ResultNotConclusive,
            (PopperFalsificationStage.ResultUnfalsifiable or PopperFalsificationStage.ResultCalibrated
                or PopperFalsificationStage.ResultCounterexample or PopperFalsificationStage.ResultNotConclusive,
                PopperFalsificationAction.Leave) => stage,
            _ => throw new InvalidOperationException($"Invalid Popper transition {stage}/{action}."),
        };

    public static bool IsRouteConsistent(PopperFalsificationState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || journey.CurrentNodeId != PopperFalsificationState.SourceNodeId
            || journey.LastFixedAct != 3
            || !journey.CompletedEdgeIds.Contains(PopperFalsificationState.RequiredEdgeId)
            || journey.ResolvedQuestionActs.Contains(3)) return false;
        return state.Outcome == PopperFalsificationOutcome.None
            || state.ConfirmedActions.Contains(state.Outcome switch
            {
                PopperFalsificationOutcome.Unfalsifiable => PopperFalsificationAction.SelectUnfalsifiableTest,
                PopperFalsificationOutcome.Calibrated => PopperFalsificationAction.CalibrateAndReport,
                PopperFalsificationOutcome.Counterexample => PopperFalsificationAction.PublishCounterexample,
                PopperFalsificationOutcome.NotConclusive => PopperFalsificationAction.RefuseFinalVerdict,
                _ => PopperFalsificationAction.None,
            });
    }

    public static string StageKey(PopperFalsificationStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(PopperFalsificationAction action) => ToUpperSnake(action.ToString());
    public static string DescriptionKey(PopperFalsificationState state) => state.Stage switch
    {
        PopperFalsificationStage.Reports => "REPORTS",
        PopperFalsificationStage.Evidence => "EVIDENCE",
        PopperFalsificationStage.ResultUnfalsifiable => "RESULT_UNFALSIFIABLE",
        PopperFalsificationStage.ResultCalibrated => "RESULT_CALIBRATED",
        PopperFalsificationStage.ResultCounterexample => "RESULT_COUNTEREXAMPLE",
        PopperFalsificationStage.ResultNotConclusive => "RESULT_NOT_CONCLUSIVE",
        _ => "REPORTS",
    };

    private static void Apply(PopperFalsificationState state, PopperFalsificationAction action)
    {
        switch (action)
        {
            case PopperFalsificationAction.SelectBoundaryTest:
                state.FalsifiableTest = true;
                state.Stage = PopperFalsificationStage.Evidence;
                break;
            case PopperFalsificationAction.SelectUnfalsifiableTest:
                state.FalsifiableTest = false;
                state.Outcome = PopperFalsificationOutcome.Unfalsifiable;
                state.Stage = PopperFalsificationStage.ResultUnfalsifiable;
                break;
            case PopperFalsificationAction.CalibrateAndReport:
                state.InstrumentRiskKnown = true;
                state.CalibrationPaid = true;
                state.Outcome = PopperFalsificationOutcome.Calibrated;
                state.Stage = PopperFalsificationStage.ResultCalibrated;
                break;
            case PopperFalsificationAction.PublishCounterexample:
                state.AuxiliaryUnknown = true;
                state.Outcome = PopperFalsificationOutcome.Counterexample;
                state.Stage = PopperFalsificationStage.ResultCounterexample;
                break;
            case PopperFalsificationAction.RefuseFinalVerdict:
                state.TheoryNotKilled = true;
                state.Outcome = PopperFalsificationOutcome.NotConclusive;
                state.Stage = PopperFalsificationStage.ResultNotConclusive;
                break;
            case PopperFalsificationAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
