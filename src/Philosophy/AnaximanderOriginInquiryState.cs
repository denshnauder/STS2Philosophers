namespace STS2Philosophers;

internal enum AnaximanderOriginInquiryStage
{
    Invitation, Observation, DeterminateChallenge, IndefiniteChallenge,
    ResultRevised, ResultKept, ResultStopped, ResultDeclined,
}

internal enum AnaximanderOriginInquiryAction
{
    None, AcceptInvitation, DeclineInvitation, NameVisibleOrigin, WithholdPriority,
    RecordLimitedQuestion, KeepObservedTension, StopInquiry, Leave,
}

internal sealed class AnaximanderOriginInquiryState
{
    public const string RequiredNodeId = "HERACLITUS__BEING_AND_CHANGE__CORE";
    public const string QuestionedNodeId = "ANAXIMANDER__BEING_AND_CHANGE__CORE";
    public const string EdgeId = "WESTERN_EDGE_043";
    public int Version { get; set; } = 1;
    public string InstanceId { get; set; } = string.Empty;
    public AnaximanderOriginInquiryStage Stage { get; set; }
    public AnaximanderOriginInquiryAction PendingAction { get; set; }
    public List<AnaximanderOriginInquiryAction> ConfirmedActions { get; set; } = [];
    public bool Closed { get; set; }

    public static AnaximanderOriginInquiryState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || ConfirmedActions is null
            || ConfirmedActions.Count > 5 || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        var replay = AnaximanderOriginInquiryStage.Invitation;
        bool closed = false;
        foreach (var action in ConfirmedActions)
        {
            if (closed || !AnaximanderOriginInquiryFlow.Options(replay).Contains(action)) return false;
            closed = action == AnaximanderOriginInquiryAction.Leave;
            replay = AnaximanderOriginInquiryFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == AnaximanderOriginInquiryAction.None
                || (!Closed && AnaximanderOriginInquiryFlow.Options(Stage).Contains(PendingAction)));
    }
}

internal static class AnaximanderOriginInquiryFlow
{
    public static IReadOnlyList<AnaximanderOriginInquiryAction> Options(AnaximanderOriginInquiryStage stage) => stage switch
    {
        AnaximanderOriginInquiryStage.Invitation => [AnaximanderOriginInquiryAction.AcceptInvitation, AnaximanderOriginInquiryAction.DeclineInvitation],
        AnaximanderOriginInquiryStage.Observation => [AnaximanderOriginInquiryAction.NameVisibleOrigin,
            AnaximanderOriginInquiryAction.WithholdPriority, AnaximanderOriginInquiryAction.StopInquiry],
        AnaximanderOriginInquiryStage.DeterminateChallenge or AnaximanderOriginInquiryStage.IndefiniteChallenge =>
            [AnaximanderOriginInquiryAction.RecordLimitedQuestion, AnaximanderOriginInquiryAction.KeepObservedTension,
                AnaximanderOriginInquiryAction.StopInquiry],
        AnaximanderOriginInquiryStage.ResultRevised or AnaximanderOriginInquiryStage.ResultKept
            or AnaximanderOriginInquiryStage.ResultStopped or AnaximanderOriginInquiryStage.ResultDeclined =>
            [AnaximanderOriginInquiryAction.Leave],
        _ => [],
    };

    internal static AnaximanderOriginInquiryStage Next(
        AnaximanderOriginInquiryStage stage, AnaximanderOriginInquiryAction action) => action switch
    {
        AnaximanderOriginInquiryAction.AcceptInvitation => AnaximanderOriginInquiryStage.Observation,
        AnaximanderOriginInquiryAction.DeclineInvitation => AnaximanderOriginInquiryStage.ResultDeclined,
        AnaximanderOriginInquiryAction.NameVisibleOrigin => AnaximanderOriginInquiryStage.DeterminateChallenge,
        AnaximanderOriginInquiryAction.WithholdPriority => AnaximanderOriginInquiryStage.IndefiniteChallenge,
        AnaximanderOriginInquiryAction.RecordLimitedQuestion => AnaximanderOriginInquiryStage.ResultRevised,
        AnaximanderOriginInquiryAction.KeepObservedTension => AnaximanderOriginInquiryStage.ResultKept,
        AnaximanderOriginInquiryAction.StopInquiry => AnaximanderOriginInquiryStage.ResultStopped,
        _ => stage,
    };

    public static bool TryPrepare(AnaximanderOriginInquiryState state, AnaximanderOriginInquiryAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != AnaximanderOriginInquiryAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(
        AnaximanderOriginInquiryState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction == AnaximanderOriginInquiryAction.None
            || journey.LastFixedAct != 2 || journey.CurrentNodeId != AnaximanderOriginInquiryState.RequiredNodeId
            || !journey.DeclinedFixedActs.Contains(2) || !journey.ResolvedQuestionActs.Contains(1)
            || !journey.ResolvedQuestionActs.Contains(2)) return false;

        bool revised = state.ConfirmedActions.Contains(AnaximanderOriginInquiryAction.RecordLimitedQuestion);
        bool alreadyRecorded = journey.CompletedEdgeIds.Contains(AnaximanderOriginInquiryState.EdgeId)
            && journey.SeenThinkerIds.Contains("ANAXIMANDER");
        var action = state.PendingAction;
        if (revised ? !alreadyRecorded : alreadyRecorded && action != AnaximanderOriginInquiryAction.RecordLimitedQuestion)
            return false;
        if (action == AnaximanderOriginInquiryAction.RecordLimitedQuestion && !alreadyRecorded
            && !graph.TryCompleteReservedQuestion(journey, AnaximanderOriginInquiryState.EdgeId)) return false;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == AnaximanderOriginInquiryAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = AnaximanderOriginInquiryAction.None;
        return true;
    }

    public static string StageKey(AnaximanderOriginInquiryStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(AnaximanderOriginInquiryAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
