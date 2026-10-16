namespace STS2Philosophers;

internal enum ParmenidesInquiryStage
{
    Invitation, Referent, WholeChallenge, CupChallenge, Boundary,
    ResultAdopted, ResultKept, ResultStopped, ResultDeclined,
}

internal enum ParmenidesInquiryAction
{
    None, AcceptInvitation, DeclineInvitation, SpeakOfBeing, SpeakOfCup,
    DistinguishNegation, AdoptBoundary, KeepChange, StopInquiry, Leave,
}

internal sealed class ParmenidesInquiryState
{
    public const string RequiredNodeId = "HERACLITUS__BEING_AND_CHANGE__CORE";
    public const string AdoptedNodeId = "PARMENIDES__BEING_AND_CHANGE__CORE";
    public const string EdgeId = "WESTERN_EDGE_001";
    public int Version { get; set; } = 1;
    public string InstanceId { get; set; } = string.Empty;
    public ParmenidesInquiryStage Stage { get; set; }
    public ParmenidesInquiryAction PendingAction { get; set; }
    public List<ParmenidesInquiryAction> ConfirmedActions { get; set; } = [];
    public bool Closed { get; set; }

    public static ParmenidesInquiryState Create(string instanceId) => new() { InstanceId = instanceId };
    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }
    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || ConfirmedActions is null
            || ConfirmedActions.Count > 6 || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        var replay = ParmenidesInquiryStage.Invitation;
        bool closed = false;
        foreach (var action in ConfirmedActions)
        {
            if (closed || !ParmenidesInquiryFlow.Options(replay).Contains(action)) return false;
            closed = action == ParmenidesInquiryAction.Leave;
            replay = ParmenidesInquiryFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == ParmenidesInquiryAction.None
                || (!Closed && ParmenidesInquiryFlow.Options(Stage).Contains(PendingAction)));
    }
}

internal static class ParmenidesInquiryFlow
{
    public static IReadOnlyList<ParmenidesInquiryAction> Options(ParmenidesInquiryStage stage) => stage switch
    {
        ParmenidesInquiryStage.Invitation => [ParmenidesInquiryAction.AcceptInvitation, ParmenidesInquiryAction.DeclineInvitation],
        ParmenidesInquiryStage.Referent => [ParmenidesInquiryAction.SpeakOfBeing, ParmenidesInquiryAction.SpeakOfCup, ParmenidesInquiryAction.StopInquiry],
        ParmenidesInquiryStage.WholeChallenge or ParmenidesInquiryStage.Boundary =>
            [ParmenidesInquiryAction.AdoptBoundary, ParmenidesInquiryAction.KeepChange, ParmenidesInquiryAction.StopInquiry],
        ParmenidesInquiryStage.CupChallenge =>
            [ParmenidesInquiryAction.DistinguishNegation, ParmenidesInquiryAction.AdoptBoundary, ParmenidesInquiryAction.StopInquiry],
        ParmenidesInquiryStage.ResultAdopted or ParmenidesInquiryStage.ResultKept
            or ParmenidesInquiryStage.ResultStopped or ParmenidesInquiryStage.ResultDeclined => [ParmenidesInquiryAction.Leave],
        _ => [],
    };
    internal static ParmenidesInquiryStage Next(ParmenidesInquiryStage stage, ParmenidesInquiryAction action) => action switch
    {
        ParmenidesInquiryAction.AcceptInvitation => ParmenidesInquiryStage.Referent,
        ParmenidesInquiryAction.DeclineInvitation => ParmenidesInquiryStage.ResultDeclined,
        ParmenidesInquiryAction.SpeakOfBeing => ParmenidesInquiryStage.WholeChallenge,
        ParmenidesInquiryAction.SpeakOfCup => ParmenidesInquiryStage.CupChallenge,
        ParmenidesInquiryAction.DistinguishNegation => ParmenidesInquiryStage.Boundary,
        ParmenidesInquiryAction.AdoptBoundary => ParmenidesInquiryStage.ResultAdopted,
        ParmenidesInquiryAction.KeepChange => ParmenidesInquiryStage.ResultKept,
        ParmenidesInquiryAction.StopInquiry => ParmenidesInquiryStage.ResultStopped,
        _ => stage,
    };
    public static bool TryPrepare(ParmenidesInquiryState state, ParmenidesInquiryAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != ParmenidesInquiryAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }
    public static bool TryCommitPending(ParmenidesInquiryState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction == ParmenidesInquiryAction.None
            || journey.LastFixedAct != 1 || !journey.ResolvedQuestionActs.Contains(1)) return false;
        bool adopted = state.ConfirmedActions.Contains(ParmenidesInquiryAction.AdoptBoundary);
        bool alreadyAdopted = journey.CurrentNodeId == ParmenidesInquiryState.AdoptedNodeId
            && journey.CompletedEdgeIds.Contains(ParmenidesInquiryState.EdgeId);
        var action = state.PendingAction;
        if (adopted ? !alreadyAdopted : journey.CurrentNodeId != ParmenidesInquiryState.RequiredNodeId
            && !(action == ParmenidesInquiryAction.AdoptBoundary && alreadyAdopted)) return false;
        if (action == ParmenidesInquiryAction.AdoptBoundary && !alreadyAdopted
            && !graph.TryCompleteReservedQuestion(journey, ParmenidesInquiryState.EdgeId)) return false;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == ParmenidesInquiryAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = ParmenidesInquiryAction.None;
        return true;
    }
    public static string StageKey(ParmenidesInquiryStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(ParmenidesInquiryAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
