namespace STS2Philosophers;

internal enum SextusAppearancesStage
{
    Invitation,
    Comparison,
    ResultDeclined,
    ResultAgreeLampA,
    ResultAgreeLampB,
    ResultSuspend,
    ResultContinueInquiry,
    ResultRejectProposal,
}

internal enum SextusAppearancesAction
{
    None,
    AcceptInvitation,
    DeclineInvitation,
    AgreeLampA,
    AgreeLampB,
    SuspendAssent,
    ContinueInquiry,
    RejectProposal,
    Leave,
}

internal sealed class SextusOpposedAppearancesState
{
    public const int CurrentVersion = 1;
    public const string RequiredNodeId = "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public SextusAppearancesStage Stage { get; set; }
    public SextusAppearancesAction PendingAction { get; set; }
    public List<SextusAppearancesAction> ConfirmedActions { get; set; } = [];
    public bool QuestionSlotReserved { get; set; }
    public bool InvitationAccepted { get; set; }
    public bool JudgmentSubmitted { get; set; }
    public SextusAppearancesAction SubmittedJudgment { get; set; }
    public bool NativeForkPreserved { get; set; }
    public bool Closed { get; set; }

    public static SextusOpposedAppearancesState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = SextusAppearancesStage.Invitation,
        QuestionSlotReserved = true,
        NativeForkPreserved = true,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable() =>
        Version == CurrentVersion
        && !string.IsNullOrWhiteSpace(InstanceId)
        && Enum.IsDefined(Stage)
        && Enum.IsDefined(PendingAction)
        && Enum.IsDefined(SubmittedJudgment)
        && ConfirmedActions.All(Enum.IsDefined)
        && QuestionSlotReserved
        && NativeForkPreserved
        && (!JudgmentSubmitted || SubmittedJudgment is
            SextusAppearancesAction.AgreeLampA or
            SextusAppearancesAction.AgreeLampB or
            SextusAppearancesAction.SuspendAssent or
            SextusAppearancesAction.ContinueInquiry or
            SextusAppearancesAction.RejectProposal)
        && (JudgmentSubmitted || SubmittedJudgment == SextusAppearancesAction.None);
}

internal static class SextusOpposedAppearancesFlow
{
    public static bool TryPrepare(SextusOpposedAppearancesState state, SextusAppearancesAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != SextusAppearancesAction.None
            || !Options(state.Stage).Contains(action))
        {
            return false;
        }

        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(SextusOpposedAppearancesState state)
    {
        if (!state.IsUsable() || state.PendingAction == SextusAppearancesAction.None
            || !Options(state.Stage).Contains(state.PendingAction))
        {
            return false;
        }

        SextusAppearancesAction action = state.PendingAction;
        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = SextusAppearancesAction.None;
        return true;
    }

    public static IReadOnlyList<SextusAppearancesAction> Options(SextusAppearancesStage stage) => stage switch
    {
        SextusAppearancesStage.Invitation =>
            [SextusAppearancesAction.AcceptInvitation, SextusAppearancesAction.DeclineInvitation],
        SextusAppearancesStage.Comparison =>
            [
                SextusAppearancesAction.AgreeLampA,
                SextusAppearancesAction.AgreeLampB,
                SextusAppearancesAction.SuspendAssent,
                SextusAppearancesAction.ContinueInquiry,
                SextusAppearancesAction.RejectProposal,
            ],
        SextusAppearancesStage.ResultDeclined or
        SextusAppearancesStage.ResultAgreeLampA or
        SextusAppearancesStage.ResultAgreeLampB or
        SextusAppearancesStage.ResultSuspend or
        SextusAppearancesStage.ResultContinueInquiry or
        SextusAppearancesStage.ResultRejectProposal => [SextusAppearancesAction.Leave],
        _ => [],
    };

    public static string StageKey(SextusAppearancesStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(SextusAppearancesAction action) => ToUpperSnake(action.ToString());

    private static void Apply(SextusOpposedAppearancesState state, SextusAppearancesAction action)
    {
        switch (action)
        {
            case SextusAppearancesAction.AcceptInvitation:
                state.InvitationAccepted = true;
                state.Stage = SextusAppearancesStage.Comparison;
                break;
            case SextusAppearancesAction.DeclineInvitation:
                state.Stage = SextusAppearancesStage.ResultDeclined;
                break;
            case SextusAppearancesAction.AgreeLampA:
                Submit(state, action, SextusAppearancesStage.ResultAgreeLampA);
                break;
            case SextusAppearancesAction.AgreeLampB:
                Submit(state, action, SextusAppearancesStage.ResultAgreeLampB);
                break;
            case SextusAppearancesAction.SuspendAssent:
                Submit(state, action, SextusAppearancesStage.ResultSuspend);
                break;
            case SextusAppearancesAction.ContinueInquiry:
                Submit(state, action, SextusAppearancesStage.ResultContinueInquiry);
                break;
            case SextusAppearancesAction.RejectProposal:
                Submit(state, action, SextusAppearancesStage.ResultRejectProposal);
                break;
            case SextusAppearancesAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static void Submit(
        SextusOpposedAppearancesState state,
        SextusAppearancesAction judgment,
        SextusAppearancesStage result)
    {
        state.JudgmentSubmitted = true;
        state.SubmittedJudgment = judgment;
        state.Stage = result;
    }

    private static string ToUpperSnake(string value) => string.Concat(
        value.SelectMany((character, index) => index > 0 && char.IsUpper(character)
            ? new[] { '_', character }
            : new[] { char.ToUpperInvariant(character) }));
}
