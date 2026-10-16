namespace STS2Philosophers;

internal enum AristotleCivicInquiryStage
{
    Invitation, Arrangement, CentralChallenge, RotationChallenge,
    SharedDecision, KeptControl, ExtendedTurn, KeptRoll,
    ResultAdopted, ResultKept, ResultStopped, ResultDeclined,
}

internal enum AristotleCivicInquiryAction
{
    None, AcceptInvitation, DeclineInvitation, Centralize, Rotate,
    ShareDecision, KeepControl, GiveTurn, KeepRoll, AdoptInquiry, KeepPlato, StopInquiry, Leave,
}

internal sealed class AristotleCivicInquiryState
{
    public const string RequiredNodeId = "PLATO__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string AdoptedNodeId = "ARISTOTLE__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string EdgeId = "WESTERN_EDGE_016";
    public int Version { get; set; } = 1;
    public string InstanceId { get; set; } = string.Empty;
    public AristotleCivicInquiryStage Stage { get; set; }
    public AristotleCivicInquiryAction PendingAction { get; set; }
    public List<AristotleCivicInquiryAction> ConfirmedActions { get; set; } = [];
    public bool Closed { get; set; }

    public static AristotleCivicInquiryState Create(string instanceId) => new() { InstanceId = instanceId };
    public void NormalizeAfterLoad() { InstanceId ??= string.Empty; ConfirmedActions ??= []; }
    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || ConfirmedActions is null
            || ConfirmedActions.Count > 6 || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        var replay = AristotleCivicInquiryStage.Invitation;
        bool closed = false;
        foreach (var action in ConfirmedActions)
        {
            if (closed || !AristotleCivicInquiryFlow.Options(replay).Contains(action)) return false;
            closed = action == AristotleCivicInquiryAction.Leave;
            replay = AristotleCivicInquiryFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == AristotleCivicInquiryAction.None
                || (!Closed && AristotleCivicInquiryFlow.Options(Stage).Contains(PendingAction)));
    }
}

internal static class AristotleCivicInquiryFlow
{
    public static IReadOnlyList<AristotleCivicInquiryAction> Options(AristotleCivicInquiryStage stage) => stage switch
    {
        AristotleCivicInquiryStage.Invitation => [AristotleCivicInquiryAction.AcceptInvitation, AristotleCivicInquiryAction.DeclineInvitation],
        AristotleCivicInquiryStage.Arrangement => [AristotleCivicInquiryAction.Centralize, AristotleCivicInquiryAction.Rotate, AristotleCivicInquiryAction.StopInquiry],
        AristotleCivicInquiryStage.CentralChallenge => [AristotleCivicInquiryAction.ShareDecision, AristotleCivicInquiryAction.KeepControl, AristotleCivicInquiryAction.StopInquiry],
        AristotleCivicInquiryStage.RotationChallenge => [AristotleCivicInquiryAction.GiveTurn, AristotleCivicInquiryAction.KeepRoll, AristotleCivicInquiryAction.StopInquiry],
        AristotleCivicInquiryStage.SharedDecision or AristotleCivicInquiryStage.KeptControl
            or AristotleCivicInquiryStage.ExtendedTurn or AristotleCivicInquiryStage.KeptRoll =>
            [AristotleCivicInquiryAction.AdoptInquiry, AristotleCivicInquiryAction.KeepPlato],
        AristotleCivicInquiryStage.ResultAdopted or AristotleCivicInquiryStage.ResultKept
            or AristotleCivicInquiryStage.ResultStopped or AristotleCivicInquiryStage.ResultDeclined => [AristotleCivicInquiryAction.Leave],
        _ => [],
    };
    internal static AristotleCivicInquiryStage Next(AristotleCivicInquiryStage stage, AristotleCivicInquiryAction action) => action switch
    {
        AristotleCivicInquiryAction.AcceptInvitation => AristotleCivicInquiryStage.Arrangement,
        AristotleCivicInquiryAction.DeclineInvitation => AristotleCivicInquiryStage.ResultDeclined,
        AristotleCivicInquiryAction.Centralize => AristotleCivicInquiryStage.CentralChallenge,
        AristotleCivicInquiryAction.Rotate => AristotleCivicInquiryStage.RotationChallenge,
        AristotleCivicInquiryAction.ShareDecision => AristotleCivicInquiryStage.SharedDecision,
        AristotleCivicInquiryAction.KeepControl => AristotleCivicInquiryStage.KeptControl,
        AristotleCivicInquiryAction.GiveTurn => AristotleCivicInquiryStage.ExtendedTurn,
        AristotleCivicInquiryAction.KeepRoll => AristotleCivicInquiryStage.KeptRoll,
        AristotleCivicInquiryAction.AdoptInquiry => AristotleCivicInquiryStage.ResultAdopted,
        AristotleCivicInquiryAction.KeepPlato => AristotleCivicInquiryStage.ResultKept,
        AristotleCivicInquiryAction.StopInquiry => AristotleCivicInquiryStage.ResultStopped,
        _ => stage,
    };
    public static bool TryPrepare(AristotleCivicInquiryState state, AristotleCivicInquiryAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != AristotleCivicInquiryAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }
    private static bool ResolvesFixed(AristotleCivicInquiryAction action) => action is
        AristotleCivicInquiryAction.AdoptInquiry or AristotleCivicInquiryAction.KeepPlato
        or AristotleCivicInquiryAction.StopInquiry or AristotleCivicInquiryAction.DeclineInvitation;
    private static bool ResolvedMatches(WesternJourneyState journey, bool adopted) =>
        journey.LastFixedAct == 2 && (adopted
            ? journey.CurrentNodeId == AristotleCivicInquiryState.AdoptedNodeId
                && journey.CompletedEdgeIds.Contains(AristotleCivicInquiryState.EdgeId)
                && !journey.DeclinedFixedActs.Contains(2)
            : journey.CurrentNodeId == AristotleCivicInquiryState.RequiredNodeId
                && journey.DeclinedFixedActs.Contains(2)
                && !journey.CompletedEdgeIds.Contains(AristotleCivicInquiryState.EdgeId));
    public static bool TryCommitPending(AristotleCivicInquiryState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction == AristotleCivicInquiryAction.None) return false;
        var action = state.PendingAction;
        bool wasResolved = state.ConfirmedActions.Any(ResolvesFixed);
        bool adopted = state.ConfirmedActions.Contains(AristotleCivicInquiryAction.AdoptInquiry);
        if (wasResolved)
        {
            if (!ResolvedMatches(journey, adopted)) return false;
        }
        else if (ResolvesFixed(action) && ResolvedMatches(journey, action == AristotleCivicInquiryAction.AdoptInquiry)) { }
        else
        {
            if (journey.LastFixedAct != 1 || journey.CurrentNodeId != AristotleCivicInquiryState.RequiredNodeId
                || journey.RetainedEnding || journey.CompletedEdgeIds.Contains(AristotleCivicInquiryState.EdgeId)) return false;
            if (ResolvesFixed(action) && !(action == AristotleCivicInquiryAction.AdoptInquiry
                ? graph.TryApply(journey, AristotleCivicInquiryState.EdgeId) : graph.TryDecline(journey, "Fixed", 2))) return false;
        }
        state.Stage = Next(state.Stage, action);
        state.Closed = action == AristotleCivicInquiryAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = AristotleCivicInquiryAction.None;
        return true;
    }
    public static string StageKey(AristotleCivicInquiryStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(AristotleCivicInquiryAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
