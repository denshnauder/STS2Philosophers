namespace STS2Philosophers;

internal enum ZenoCivicStage
{
    Invitation,
    Rule,
    PublicEvidence,
    ConditionalEvidence,
    ResultDeclined,
    ResultPublicObjection,
    ResultConditionalObjection,
    ResultWithdrawnPublic,
    ResultWithdrawnConditional,
}

internal enum ZenoCivicAction
{
    None,
    AcceptInvitation,
    DeclineInvitation,
    DemandPublicRule,
    ProposeConditionalException,
    PublishPublicObjection,
    PublishConditionalObjection,
    WithdrawObjection,
    Leave,
}

internal sealed class ZenoCivicChallengeState
{
    public const int CurrentVersion = 1;
    public const int QuestionAct = 1;
    public const string SourceNodeId = "PLATO__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string EdgeId = "WESTERN_EDGE_019";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public ZenoCivicStage Stage { get; set; }
    public ZenoCivicAction PendingAction { get; set; }
    public List<ZenoCivicAction> ConfirmedActions { get; set; } = [];
    public bool QuestionReserved { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Closed { get; set; }

    public static ZenoCivicChallengeState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        QuestionReserved = true,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !QuestionReserved || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != ZenoCivicAction.None
            || PendingAction != ZenoCivicAction.None
                && !ZenoCivicChallengeFlow.Options(Stage).Contains(PendingAction)) return false;

        ZenoCivicAction[] history = Closed
            ? ConfirmedActions.Take(ConfirmedActions.Count - 1).ToArray()
            : ConfirmedActions.ToArray();
        if (Closed && (ConfirmedActions.Count == 0 || ConfirmedActions[^1] != ZenoCivicAction.Leave)) return false;
        ZenoCivicAction[] expected = Stage switch
        {
            ZenoCivicStage.Invitation => [],
            ZenoCivicStage.Rule => [ZenoCivicAction.AcceptInvitation],
            ZenoCivicStage.PublicEvidence => [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.DemandPublicRule],
            ZenoCivicStage.ConditionalEvidence => [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.ProposeConditionalException],
            ZenoCivicStage.ResultDeclined => [ZenoCivicAction.DeclineInvitation],
            ZenoCivicStage.ResultPublicObjection => [ZenoCivicAction.AcceptInvitation,
                ZenoCivicAction.DemandPublicRule, ZenoCivicAction.PublishPublicObjection],
            ZenoCivicStage.ResultConditionalObjection => [ZenoCivicAction.AcceptInvitation,
                ZenoCivicAction.ProposeConditionalException, ZenoCivicAction.PublishConditionalObjection],
            ZenoCivicStage.ResultWithdrawnPublic => [ZenoCivicAction.AcceptInvitation,
                ZenoCivicAction.DemandPublicRule, ZenoCivicAction.WithdrawObjection],
            ZenoCivicStage.ResultWithdrawnConditional => [ZenoCivicAction.AcceptInvitation,
                ZenoCivicAction.ProposeConditionalException, ZenoCivicAction.WithdrawObjection],
            _ => [],
        };
        bool adopted = Stage is ZenoCivicStage.ResultPublicObjection or ZenoCivicStage.ResultConditionalObjection;
        return history.SequenceEqual(expected) && EdgeCommitted == adopted
            && (!Closed || Stage >= ZenoCivicStage.ResultDeclined);
    }
}

internal static class ZenoCivicChallengeFlow
{
    public static IReadOnlyList<ZenoCivicAction> Options(ZenoCivicStage stage) => stage switch
    {
        ZenoCivicStage.Invitation => [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.DeclineInvitation],
        ZenoCivicStage.Rule => [ZenoCivicAction.DemandPublicRule, ZenoCivicAction.ProposeConditionalException],
        ZenoCivicStage.PublicEvidence => [ZenoCivicAction.PublishPublicObjection, ZenoCivicAction.WithdrawObjection],
        ZenoCivicStage.ConditionalEvidence => [ZenoCivicAction.PublishConditionalObjection, ZenoCivicAction.WithdrawObjection],
        >= ZenoCivicStage.ResultDeclined and <= ZenoCivicStage.ResultWithdrawnConditional => [ZenoCivicAction.Leave],
        _ => [],
    };

    public static bool IsRouteConsistent(ZenoCivicChallengeState state, WesternJourneyState journey) =>
        state.IsUsable() && journey.CurrentNodeId == ZenoCivicChallengeState.SourceNodeId
        && journey.LastFixedAct == ZenoCivicChallengeState.QuestionAct
        && journey.ResolvedQuestionActs.Contains(ZenoCivicChallengeState.QuestionAct)
        && !journey.RetainedEnding
        && journey.CompletedEdgeIds.Contains(ZenoCivicChallengeState.EdgeId) == state.EdgeCommitted;

    public static bool TryPrepare(ZenoCivicChallengeState state, ZenoCivicAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != ZenoCivicAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(ZenoCivicChallengeState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == ZenoCivicAction.None) return false;
        ZenoCivicAction action = state.PendingAction;
        bool publishes = action is ZenoCivicAction.PublishPublicObjection
            or ZenoCivicAction.PublishConditionalObjection;
        if (publishes && !graph.TryCompleteReservedQuestion(journey, ZenoCivicChallengeState.EdgeId)) return false;
        state.Stage = action switch
        {
            ZenoCivicAction.AcceptInvitation => ZenoCivicStage.Rule,
            ZenoCivicAction.DeclineInvitation => ZenoCivicStage.ResultDeclined,
            ZenoCivicAction.DemandPublicRule => ZenoCivicStage.PublicEvidence,
            ZenoCivicAction.ProposeConditionalException => ZenoCivicStage.ConditionalEvidence,
            ZenoCivicAction.PublishPublicObjection => ZenoCivicStage.ResultPublicObjection,
            ZenoCivicAction.PublishConditionalObjection => ZenoCivicStage.ResultConditionalObjection,
            ZenoCivicAction.WithdrawObjection when state.Stage == ZenoCivicStage.PublicEvidence =>
                ZenoCivicStage.ResultWithdrawnPublic,
            ZenoCivicAction.WithdrawObjection when state.Stage == ZenoCivicStage.ConditionalEvidence =>
                ZenoCivicStage.ResultWithdrawnConditional,
            ZenoCivicAction.Leave => state.Stage,
            _ => throw new InvalidOperationException("Invalid civic challenge action."),
        };
        state.EdgeCommitted |= publishes;
        state.Closed = action == ZenoCivicAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = ZenoCivicAction.None;
        return IsRouteConsistent(state, journey);
    }

    public static string StageKey(ZenoCivicStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(ZenoCivicAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
