namespace STS2Philosophers;

internal enum ProtagorasDeliberationStage
{
    Invitation,
    Demonstration,
    Boundary,
    PersonalResponse,
    Negotiation,
    RevisionConfirmation,
    ActiveHosting,
    ResultDeclined,
    ResultIncomplete,
    ResultPersonalHost,
    ResultSeparateStatements,
    ResultNoHandoff,
    ResultCompanionHost,
    ResultHandoffStopped,
}

internal enum ProtagorasDeliberationAction
{
    None,
    AcceptInvitation,
    DeclineInvitation,
    DemonstrateOriginalMethod,
    StopPractice,
    ChoosePersonalHost,
    ChooseNegotiation,
    HearFullReason,
    WithdrawRepresentationRequest,
    RestateWithoutAuthorization,
    AuthorizeCompanion,
    ProposeSpecificSentence,
    EndWithoutResponse,
    AuthorizeNarrowedScope,
    RestateAfterRevision,
    StopAfterProposal,
    ConfirmLimitedAuthorization,
    StopFutureDelegation,
    Leave,
}

internal enum ProtagorasAuthorizationScope
{
    None,
    ImmediatePause,
    SpecificSentence,
}

internal sealed class ProtagorasSharedDeliberationState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "SOCRATES__VIRTUE_AND_HAPPINESS__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_008";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int SourceAct { get; set; } = 1;
    public string SourceNode { get; set; } = SourceNodeId;
    public ProtagorasDeliberationStage Stage { get; set; }
    public ProtagorasDeliberationAction PendingAction { get; set; }
    public List<ProtagorasDeliberationAction> ConfirmedActions { get; set; } = [];
    public ProtagorasAuthorizationScope AuthorizationScope { get; set; }
    public bool QuestionReserved { get; set; }
    public bool OriginalMethodProposed { get; set; }
    public bool CompanionRestatedOriginal { get; set; }
    public bool TeacherRecordSuggestionPresented { get; set; }
    public bool CompanionRefusedRepresentation { get; set; }
    public bool CompanionRequestPresented { get; set; }
    public bool PlayerSpecificSentenceProposed { get; set; }
    public bool CompanionAcceptedSpecificSentence { get; set; }
    public bool ImmediatePausePerformed { get; set; }
    public bool FutureDelegationStopped { get; set; }
    public bool PracticeCompleted { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Closed { get; set; }

    public static ProtagorasSharedDeliberationState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = ProtagorasDeliberationStage.Invitation,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        SourceNode ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable() =>
        Version == CurrentVersion
        && !string.IsNullOrWhiteSpace(InstanceId)
        && SourceAct == 1
        && SourceNode == SourceNodeId
        && Enum.IsDefined(Stage)
        && Enum.IsDefined(PendingAction)
        && Enum.IsDefined(AuthorizationScope)
        && ConfirmedActions.All(Enum.IsDefined)
        && (!PracticeCompleted || EdgeCommitted)
        && (!EdgeCommitted || QuestionReserved)
        && (!ImmediatePausePerformed || AuthorizationScope != ProtagorasAuthorizationScope.None);
}

internal static class ProtagorasSharedDeliberationFlow
{
    public static bool TryPrepare(
        ProtagorasSharedDeliberationState state,
        ProtagorasDeliberationAction action)
    {
        if (!state.IsUsable()
            || state.Closed
            || state.PendingAction != ProtagorasDeliberationAction.None
            || !IsLegal(state.Stage, action))
        {
            return false;
        }

        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(
        ProtagorasSharedDeliberationState state,
        WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!state.IsUsable() || state.PendingAction == ProtagorasDeliberationAction.None)
        {
            return false;
        }

        ProtagorasDeliberationAction action = state.PendingAction;
        if (!IsLegal(state.Stage, action))
        {
            return false;
        }

        if (action == ProtagorasDeliberationAction.AcceptInvitation
            && !state.QuestionReserved
            && !graph.TryReserveQuestion(journey, state.SourceAct))
        {
            return false;
        }

        bool completes = action is
            ProtagorasDeliberationAction.HearFullReason or
            ProtagorasDeliberationAction.WithdrawRepresentationRequest or
            ProtagorasDeliberationAction.RestateWithoutAuthorization or
            ProtagorasDeliberationAction.RestateAfterRevision or
            ProtagorasDeliberationAction.ConfirmLimitedAuthorization or
            ProtagorasDeliberationAction.StopFutureDelegation;
        if (completes
            && !state.EdgeCommitted
            && !graph.TryCompleteReservedQuestion(journey, ProtagorasSharedDeliberationState.CompletionEdgeId))
        {
            return false;
        }

        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = ProtagorasDeliberationAction.None;
        return true;
    }

    public static IReadOnlyList<ProtagorasDeliberationAction> Options(
        ProtagorasDeliberationStage stage) => stage switch
    {
        ProtagorasDeliberationStage.Invitation =>
            [ProtagorasDeliberationAction.AcceptInvitation, ProtagorasDeliberationAction.DeclineInvitation],
        ProtagorasDeliberationStage.Demonstration =>
            [ProtagorasDeliberationAction.DemonstrateOriginalMethod, ProtagorasDeliberationAction.StopPractice],
        ProtagorasDeliberationStage.Boundary =>
            [ProtagorasDeliberationAction.ChoosePersonalHost, ProtagorasDeliberationAction.ChooseNegotiation, ProtagorasDeliberationAction.StopPractice],
        ProtagorasDeliberationStage.PersonalResponse =>
            [ProtagorasDeliberationAction.HearFullReason, ProtagorasDeliberationAction.WithdrawRepresentationRequest],
        ProtagorasDeliberationStage.Negotiation =>
            [ProtagorasDeliberationAction.RestateWithoutAuthorization, ProtagorasDeliberationAction.AuthorizeCompanion, ProtagorasDeliberationAction.ProposeSpecificSentence, ProtagorasDeliberationAction.EndWithoutResponse],
        ProtagorasDeliberationStage.RevisionConfirmation =>
            [ProtagorasDeliberationAction.AuthorizeNarrowedScope, ProtagorasDeliberationAction.RestateAfterRevision, ProtagorasDeliberationAction.StopAfterProposal],
        ProtagorasDeliberationStage.ActiveHosting =>
            [ProtagorasDeliberationAction.ConfirmLimitedAuthorization, ProtagorasDeliberationAction.StopFutureDelegation],
        ProtagorasDeliberationStage.ResultDeclined or
        ProtagorasDeliberationStage.ResultIncomplete or
        ProtagorasDeliberationStage.ResultPersonalHost or
        ProtagorasDeliberationStage.ResultSeparateStatements or
        ProtagorasDeliberationStage.ResultNoHandoff or
        ProtagorasDeliberationStage.ResultCompanionHost or
        ProtagorasDeliberationStage.ResultHandoffStopped => [ProtagorasDeliberationAction.Leave],
        _ => [],
    };

    public static string StageKey(ProtagorasDeliberationStage stage) =>
        ToUpperSnake(stage.ToString());

    public static string ActionKey(ProtagorasDeliberationAction action) =>
        ToUpperSnake(action.ToString());

    private static bool IsLegal(
        ProtagorasDeliberationStage stage,
        ProtagorasDeliberationAction action) => Options(stage).Contains(action);

    private static void Apply(
        ProtagorasSharedDeliberationState state,
        ProtagorasDeliberationAction action)
    {
        switch (action)
        {
            case ProtagorasDeliberationAction.AcceptInvitation:
                state.QuestionReserved = true;
                state.Stage = ProtagorasDeliberationStage.Demonstration;
                break;
            case ProtagorasDeliberationAction.DeclineInvitation:
                state.Stage = ProtagorasDeliberationStage.ResultDeclined;
                break;
            case ProtagorasDeliberationAction.DemonstrateOriginalMethod:
                state.OriginalMethodProposed = true;
                state.CompanionRestatedOriginal = true;
                state.TeacherRecordSuggestionPresented = true;
                state.CompanionRefusedRepresentation = true;
                state.Stage = ProtagorasDeliberationStage.Boundary;
                break;
            case ProtagorasDeliberationAction.StopPractice:
            case ProtagorasDeliberationAction.EndWithoutResponse:
            case ProtagorasDeliberationAction.StopAfterProposal:
                state.Stage = ProtagorasDeliberationStage.ResultIncomplete;
                break;
            case ProtagorasDeliberationAction.ChoosePersonalHost:
                state.Stage = ProtagorasDeliberationStage.PersonalResponse;
                break;
            case ProtagorasDeliberationAction.ChooseNegotiation:
                state.CompanionRequestPresented = true;
                state.Stage = ProtagorasDeliberationStage.Negotiation;
                break;
            case ProtagorasDeliberationAction.HearFullReason:
                Complete(state, ProtagorasDeliberationStage.ResultPersonalHost);
                break;
            case ProtagorasDeliberationAction.WithdrawRepresentationRequest:
                state.FutureDelegationStopped = true;
                Complete(state, ProtagorasDeliberationStage.ResultSeparateStatements);
                break;
            case ProtagorasDeliberationAction.RestateWithoutAuthorization:
            case ProtagorasDeliberationAction.RestateAfterRevision:
                Complete(state, ProtagorasDeliberationStage.ResultNoHandoff);
                break;
            case ProtagorasDeliberationAction.AuthorizeCompanion:
                state.AuthorizationScope = ProtagorasAuthorizationScope.ImmediatePause;
                state.ImmediatePausePerformed = true;
                state.Stage = ProtagorasDeliberationStage.ActiveHosting;
                break;
            case ProtagorasDeliberationAction.ProposeSpecificSentence:
                state.PlayerSpecificSentenceProposed = true;
                state.CompanionAcceptedSpecificSentence = true;
                state.Stage = ProtagorasDeliberationStage.RevisionConfirmation;
                break;
            case ProtagorasDeliberationAction.AuthorizeNarrowedScope:
                state.AuthorizationScope = ProtagorasAuthorizationScope.SpecificSentence;
                state.ImmediatePausePerformed = true;
                state.Stage = ProtagorasDeliberationStage.ActiveHosting;
                break;
            case ProtagorasDeliberationAction.ConfirmLimitedAuthorization:
                Complete(state, ProtagorasDeliberationStage.ResultCompanionHost);
                break;
            case ProtagorasDeliberationAction.StopFutureDelegation:
                state.FutureDelegationStopped = true;
                Complete(state, ProtagorasDeliberationStage.ResultHandoffStopped);
                break;
            case ProtagorasDeliberationAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static void Complete(
        ProtagorasSharedDeliberationState state,
        ProtagorasDeliberationStage result)
    {
        state.PracticeCompleted = true;
        state.EdgeCommitted = true;
        state.Stage = result;
    }

    private static string ToUpperSnake(string value) => string.Concat(
        value.SelectMany((character, index) =>
            index > 0 && char.IsUpper(character)
                ? new[] { '_', character }
                : new[] { char.ToUpperInvariant(character) }));
}
