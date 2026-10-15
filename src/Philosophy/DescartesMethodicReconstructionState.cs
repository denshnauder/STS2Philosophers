namespace STS2Philosophers;

internal enum DescartesReconstructionStage
{
    Invitation,
    SharedBasis,
    Verification,
    LandmarkReconstruction,
    TracksReconstruction,
    ResultDeclined,
    ResultKeptBasis,
    ResultStopped,
    ResultLandmarkRebuilt,
    ResultLandmarkWithheld,
    ResultTracksRebuilt,
    ResultTracksWithheld,
}

internal enum DescartesReconstructionAction
{
    None,
    AcceptInvitation,
    DeclineInvitation,
    WithdrawSharedBasis,
    KeepBasisProvisional,
    StopMethod,
    InspectCurrentLandmark,
    CompareIndependentTracks,
    RebuildCoveredJudgment,
    KeepUnverifiedClaimsWithheld,
    Leave,
}

internal sealed class DescartesMethodicReconstructionState
{
    public const int CurrentVersion = 1;
    public const string RequiredNodeId = "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public DescartesReconstructionStage Stage { get; set; }
    public DescartesReconstructionAction PendingAction { get; set; }
    public List<DescartesReconstructionAction> ConfirmedActions { get; set; } = [];
    public bool FixedSlotResolved { get; set; }
    public bool InvitationAccepted { get; set; }
    public bool SharedBasisWithdrawn { get; set; }
    public bool OldCopyKeptProvisionally { get; set; }
    public DescartesReconstructionAction VerificationChoice { get; set; }
    public bool LimitedJudgmentRebuilt { get; set; }
    public bool UnverifiedClaimsWithheld { get; set; }
    public bool MethodCompleted { get; set; }
    public bool NativeForkPreserved { get; set; }
    public bool Closed { get; set; }

    public static DescartesMethodicReconstructionState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = DescartesReconstructionStage.Invitation,
        FixedSlotResolved = true,
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
        && Enum.IsDefined(VerificationChoice)
        && ConfirmedActions.All(Enum.IsDefined)
        && FixedSlotResolved
        && NativeForkPreserved
        && (!SharedBasisWithdrawn || InvitationAccepted)
        && (!OldCopyKeptProvisionally || InvitationAccepted)
        && (VerificationChoice == DescartesReconstructionAction.None || SharedBasisWithdrawn)
        && (VerificationChoice is DescartesReconstructionAction.None
            or DescartesReconstructionAction.InspectCurrentLandmark
            or DescartesReconstructionAction.CompareIndependentTracks)
        && (!LimitedJudgmentRebuilt || MethodCompleted)
        && (!UnverifiedClaimsWithheld || MethodCompleted)
        && (!MethodCompleted || VerificationChoice != DescartesReconstructionAction.None)
        && !(LimitedJudgmentRebuilt && UnverifiedClaimsWithheld);
}

internal static class DescartesMethodicReconstructionFlow
{
    public static bool TryPrepare(
        DescartesMethodicReconstructionState state,
        DescartesReconstructionAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != DescartesReconstructionAction.None
            || !Options(state.Stage).Contains(action))
        {
            return false;
        }

        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(DescartesMethodicReconstructionState state)
    {
        if (!state.IsUsable() || state.PendingAction == DescartesReconstructionAction.None
            || !Options(state.Stage).Contains(state.PendingAction))
        {
            return false;
        }

        DescartesReconstructionAction action = state.PendingAction;
        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = DescartesReconstructionAction.None;
        return true;
    }

    public static IReadOnlyList<DescartesReconstructionAction> Options(DescartesReconstructionStage stage) => stage switch
    {
        DescartesReconstructionStage.Invitation =>
            [DescartesReconstructionAction.AcceptInvitation, DescartesReconstructionAction.DeclineInvitation],
        DescartesReconstructionStage.SharedBasis =>
            [
                DescartesReconstructionAction.WithdrawSharedBasis,
                DescartesReconstructionAction.KeepBasisProvisional,
                DescartesReconstructionAction.StopMethod,
            ],
        DescartesReconstructionStage.Verification =>
            [
                DescartesReconstructionAction.InspectCurrentLandmark,
                DescartesReconstructionAction.CompareIndependentTracks,
                DescartesReconstructionAction.StopMethod,
            ],
        DescartesReconstructionStage.LandmarkReconstruction or
        DescartesReconstructionStage.TracksReconstruction =>
            [
                DescartesReconstructionAction.RebuildCoveredJudgment,
                DescartesReconstructionAction.KeepUnverifiedClaimsWithheld,
            ],
        DescartesReconstructionStage.ResultDeclined or
        DescartesReconstructionStage.ResultKeptBasis or
        DescartesReconstructionStage.ResultStopped or
        DescartesReconstructionStage.ResultLandmarkRebuilt or
        DescartesReconstructionStage.ResultLandmarkWithheld or
        DescartesReconstructionStage.ResultTracksRebuilt or
        DescartesReconstructionStage.ResultTracksWithheld => [DescartesReconstructionAction.Leave],
        _ => [],
    };

    public static string StageKey(DescartesReconstructionStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(DescartesReconstructionAction action) => ToUpperSnake(action.ToString());

    private static void Apply(
        DescartesMethodicReconstructionState state,
        DescartesReconstructionAction action)
    {
        switch (action)
        {
            case DescartesReconstructionAction.AcceptInvitation:
                state.InvitationAccepted = true;
                state.Stage = DescartesReconstructionStage.SharedBasis;
                break;
            case DescartesReconstructionAction.DeclineInvitation:
                state.Stage = DescartesReconstructionStage.ResultDeclined;
                break;
            case DescartesReconstructionAction.WithdrawSharedBasis:
                state.SharedBasisWithdrawn = true;
                state.Stage = DescartesReconstructionStage.Verification;
                break;
            case DescartesReconstructionAction.KeepBasisProvisional:
                state.OldCopyKeptProvisionally = true;
                state.Stage = DescartesReconstructionStage.ResultKeptBasis;
                break;
            case DescartesReconstructionAction.StopMethod:
                state.Stage = DescartesReconstructionStage.ResultStopped;
                break;
            case DescartesReconstructionAction.InspectCurrentLandmark:
                state.VerificationChoice = action;
                state.Stage = DescartesReconstructionStage.LandmarkReconstruction;
                break;
            case DescartesReconstructionAction.CompareIndependentTracks:
                state.VerificationChoice = action;
                state.Stage = DescartesReconstructionStage.TracksReconstruction;
                break;
            case DescartesReconstructionAction.RebuildCoveredJudgment:
                state.LimitedJudgmentRebuilt = true;
                Complete(state, state.Stage == DescartesReconstructionStage.LandmarkReconstruction
                    ? DescartesReconstructionStage.ResultLandmarkRebuilt
                    : DescartesReconstructionStage.ResultTracksRebuilt);
                break;
            case DescartesReconstructionAction.KeepUnverifiedClaimsWithheld:
                state.UnverifiedClaimsWithheld = true;
                Complete(state, state.Stage == DescartesReconstructionStage.LandmarkReconstruction
                    ? DescartesReconstructionStage.ResultLandmarkWithheld
                    : DescartesReconstructionStage.ResultTracksWithheld);
                break;
            case DescartesReconstructionAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static void Complete(
        DescartesMethodicReconstructionState state,
        DescartesReconstructionStage result)
    {
        state.MethodCompleted = true;
        state.Stage = result;
    }

    private static string ToUpperSnake(string value) => string.Concat(
        value.SelectMany((character, index) => index > 0 && char.IsUpper(character)
            ? new[] { '_', character }
            : new[] { char.ToUpperInvariant(character) }));
}
