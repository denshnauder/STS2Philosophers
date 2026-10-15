namespace STS2Philosophers;

internal enum EpicurusMarginStage
{
    Invitation,
    Commitment,
    MutualRequest,
    SeparateDisclosure,
    ResultDeclined,
    ResultStopped,
    ResultHonored,
    ResultRefused,
    ResultAdjusted,
    ResultSeparate,
}

internal enum EpicurusMarginAction
{
    None,
    AcceptInvitation,
    DeclineInvitation,
    OfferMutualGuarantee,
    KeepSeparateMargins,
    StopPractice,
    HonorRequest,
    RefuseRequest,
    AdjustVoluntarily,
    KeepPlansSeparate,
    Leave,
}

internal sealed class EpicurusSharedMarginState
{
    public const int CurrentVersion = 1;
    public const string RequiredNodeId = "PLATO__VIRTUE_AND_HAPPINESS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_009";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public EpicurusMarginStage Stage { get; set; }
    public EpicurusMarginAction PendingAction { get; set; }
    public List<EpicurusMarginAction> ConfirmedActions { get; set; } = [];
    public bool InvitationAccepted { get; set; }
    public bool MutualGuaranteeOffered { get; set; }
    public bool SeparateMarginsKept { get; set; }
    public bool CompanionRequestedAdjustment { get; set; }
    public bool CompanionDisclosedAdjustment { get; set; }
    public bool RequestHonored { get; set; }
    public bool RequestRefused { get; set; }
    public bool VoluntaryAdjustmentMade { get; set; }
    public bool PlansKeptSeparate { get; set; }
    public bool PracticeCompleted { get; set; }
    public bool Closed { get; set; }

    public static EpicurusSharedMarginState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = EpicurusMarginStage.Invitation,
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
        && ConfirmedActions.All(Enum.IsDefined)
        && (!CompanionRequestedAdjustment || MutualGuaranteeOffered)
        && (!CompanionDisclosedAdjustment || SeparateMarginsKept)
        && (!PracticeCompleted || Stage is EpicurusMarginStage.ResultHonored
            or EpicurusMarginStage.ResultRefused
            or EpicurusMarginStage.ResultAdjusted
            or EpicurusMarginStage.ResultSeparate);
}

internal static class EpicurusSharedMarginFlow
{
    public static bool TryPrepare(EpicurusSharedMarginState state, EpicurusMarginAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != EpicurusMarginAction.None
            || !Options(state.Stage).Contains(action))
        {
            return false;
        }

        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(EpicurusSharedMarginState state)
    {
        if (!state.IsUsable() || state.PendingAction == EpicurusMarginAction.None
            || !Options(state.Stage).Contains(state.PendingAction))
        {
            return false;
        }

        EpicurusMarginAction action = state.PendingAction;
        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = EpicurusMarginAction.None;
        return true;
    }

    public static IReadOnlyList<EpicurusMarginAction> Options(EpicurusMarginStage stage) => stage switch
    {
        EpicurusMarginStage.Invitation =>
            [EpicurusMarginAction.AcceptInvitation, EpicurusMarginAction.DeclineInvitation],
        EpicurusMarginStage.Commitment =>
            [EpicurusMarginAction.OfferMutualGuarantee, EpicurusMarginAction.KeepSeparateMargins, EpicurusMarginAction.StopPractice],
        EpicurusMarginStage.MutualRequest =>
            [EpicurusMarginAction.HonorRequest, EpicurusMarginAction.RefuseRequest],
        EpicurusMarginStage.SeparateDisclosure =>
            [EpicurusMarginAction.AdjustVoluntarily, EpicurusMarginAction.KeepPlansSeparate],
        EpicurusMarginStage.ResultDeclined or
        EpicurusMarginStage.ResultStopped or
        EpicurusMarginStage.ResultHonored or
        EpicurusMarginStage.ResultRefused or
        EpicurusMarginStage.ResultAdjusted or
        EpicurusMarginStage.ResultSeparate => [EpicurusMarginAction.Leave],
        _ => [],
    };

    public static string StageKey(EpicurusMarginStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(EpicurusMarginAction action) => ToUpperSnake(action.ToString());

    private static void Apply(EpicurusSharedMarginState state, EpicurusMarginAction action)
    {
        switch (action)
        {
            case EpicurusMarginAction.AcceptInvitation:
                state.InvitationAccepted = true;
                state.Stage = EpicurusMarginStage.Commitment;
                break;
            case EpicurusMarginAction.DeclineInvitation:
                state.Stage = EpicurusMarginStage.ResultDeclined;
                break;
            case EpicurusMarginAction.OfferMutualGuarantee:
                state.MutualGuaranteeOffered = true;
                state.CompanionRequestedAdjustment = true;
                state.Stage = EpicurusMarginStage.MutualRequest;
                break;
            case EpicurusMarginAction.KeepSeparateMargins:
                state.SeparateMarginsKept = true;
                state.CompanionDisclosedAdjustment = true;
                state.Stage = EpicurusMarginStage.SeparateDisclosure;
                break;
            case EpicurusMarginAction.StopPractice:
                state.Stage = EpicurusMarginStage.ResultStopped;
                break;
            case EpicurusMarginAction.HonorRequest:
                state.RequestHonored = true;
                Complete(state, EpicurusMarginStage.ResultHonored);
                break;
            case EpicurusMarginAction.RefuseRequest:
                state.RequestRefused = true;
                Complete(state, EpicurusMarginStage.ResultRefused);
                break;
            case EpicurusMarginAction.AdjustVoluntarily:
                state.VoluntaryAdjustmentMade = true;
                Complete(state, EpicurusMarginStage.ResultAdjusted);
                break;
            case EpicurusMarginAction.KeepPlansSeparate:
                state.PlansKeptSeparate = true;
                Complete(state, EpicurusMarginStage.ResultSeparate);
                break;
            case EpicurusMarginAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static void Complete(EpicurusSharedMarginState state, EpicurusMarginStage stage)
    {
        state.PracticeCompleted = true;
        state.Stage = stage;
    }

    private static string ToUpperSnake(string value) => string.Concat(
        value.SelectMany((character, index) => index > 0 && char.IsUpper(character)
            ? new[] { '_', character }
            : new[] { char.ToUpperInvariant(character) }));
}
