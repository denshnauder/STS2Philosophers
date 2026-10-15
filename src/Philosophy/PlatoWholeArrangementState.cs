namespace STS2Philosophers;

internal enum PlatoArrangementStage
{
    Invitation,
    Arrangement,
    IndependentResponse,
    CompanionChoice,
    ResultDeclined,
    ResultIndependent,
    ResultChoiceReturned,
    ResultJoint,
    ResultSeparated,
    ResultIncomplete,
}

internal enum PlatoArrangementAction
{
    None,
    AcceptInvitation,
    DeclineInvitation,
    ClaimIndependentPart,
    CommitDependentPart,
    StopArrangement,
    KeepSeparateWork,
    ReturnChoiceAfterInspection,
    HonorDependentCommitment,
    WithdrawAfterCompanionChoice,
    Leave,
}

internal sealed class PlatoWholeArrangementState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "SOCRATES__VIRTUE_AND_HAPPINESS__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_009";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public PlatoArrangementStage Stage { get; set; }
    public PlatoArrangementAction PendingAction { get; set; }
    public List<PlatoArrangementAction> ConfirmedActions { get; set; } = [];
    public bool InvitationAccepted { get; set; }
    public bool FixedSlotResolved { get; set; }
    public bool PlayerClaimedIndependentPart { get; set; }
    public bool PlayerCommittedDependentPart { get; set; }
    public bool CompanionRefusedJointAttribution { get; set; }
    public bool CompanionSelectedIndependentPart { get; set; }
    public bool ChoiceReturnedAfterInspection { get; set; }
    public bool DependentCommitmentHonored { get; set; }
    public bool PlayerWithdrewAfterCompanionChoice { get; set; }
    public bool PracticeCompleted { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Closed { get; set; }

    public static PlatoWholeArrangementState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = PlatoArrangementStage.Invitation,
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
        && (!EdgeCommitted || PracticeCompleted)
        && (!PracticeCompleted || EdgeCommitted)
        && (!CompanionSelectedIndependentPart || PlayerCommittedDependentPart);
}

internal static class PlatoWholeArrangementFlow
{
    public static bool TryPrepare(PlatoWholeArrangementState state, PlatoArrangementAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != PlatoArrangementAction.None
            || !Options(state.Stage).Contains(action))
        {
            return false;
        }

        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(
        PlatoWholeArrangementState state,
        WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!state.IsUsable() || state.PendingAction == PlatoArrangementAction.None
            || !Options(state.Stage).Contains(state.PendingAction))
        {
            return false;
        }

        PlatoArrangementAction action = state.PendingAction;
        bool resolvesWithoutCompletion = action is
            PlatoArrangementAction.DeclineInvitation or
            PlatoArrangementAction.StopArrangement;
        if (resolvesWithoutCompletion
            && !state.FixedSlotResolved
            && !graph.TryDecline(journey, "Fixed", 2))
        {
            return false;
        }

        bool completes = action is
            PlatoArrangementAction.KeepSeparateWork or
            PlatoArrangementAction.ReturnChoiceAfterInspection or
            PlatoArrangementAction.HonorDependentCommitment or
            PlatoArrangementAction.WithdrawAfterCompanionChoice;
        if (completes && !state.EdgeCommitted
            && !graph.TryApply(journey, PlatoWholeArrangementState.CompletionEdgeId))
        {
            return false;
        }

        Apply(state, action);
        state.ConfirmedActions.Add(action);
        state.PendingAction = PlatoArrangementAction.None;
        return true;
    }

    public static IReadOnlyList<PlatoArrangementAction> Options(PlatoArrangementStage stage) =>
        stage switch
        {
            PlatoArrangementStage.Invitation =>
                [PlatoArrangementAction.AcceptInvitation, PlatoArrangementAction.DeclineInvitation],
            PlatoArrangementStage.Arrangement =>
                [PlatoArrangementAction.ClaimIndependentPart, PlatoArrangementAction.CommitDependentPart, PlatoArrangementAction.StopArrangement],
            PlatoArrangementStage.IndependentResponse =>
                [PlatoArrangementAction.KeepSeparateWork, PlatoArrangementAction.ReturnChoiceAfterInspection],
            PlatoArrangementStage.CompanionChoice =>
                [PlatoArrangementAction.HonorDependentCommitment, PlatoArrangementAction.WithdrawAfterCompanionChoice],
            PlatoArrangementStage.ResultDeclined or
            PlatoArrangementStage.ResultIndependent or
            PlatoArrangementStage.ResultChoiceReturned or
            PlatoArrangementStage.ResultJoint or
            PlatoArrangementStage.ResultSeparated or
            PlatoArrangementStage.ResultIncomplete => [PlatoArrangementAction.Leave],
            _ => [],
        };

    public static string StageKey(PlatoArrangementStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(PlatoArrangementAction action) => ToUpperSnake(action.ToString());

    private static void Apply(PlatoWholeArrangementState state, PlatoArrangementAction action)
    {
        switch (action)
        {
            case PlatoArrangementAction.AcceptInvitation:
                state.InvitationAccepted = true;
                state.Stage = PlatoArrangementStage.Arrangement;
                break;
            case PlatoArrangementAction.DeclineInvitation:
                state.FixedSlotResolved = true;
                state.Stage = PlatoArrangementStage.ResultDeclined;
                break;
            case PlatoArrangementAction.ClaimIndependentPart:
                state.PlayerClaimedIndependentPart = true;
                state.CompanionRefusedJointAttribution = true;
                state.Stage = PlatoArrangementStage.IndependentResponse;
                break;
            case PlatoArrangementAction.CommitDependentPart:
                state.PlayerCommittedDependentPart = true;
                state.CompanionSelectedIndependentPart = true;
                state.Stage = PlatoArrangementStage.CompanionChoice;
                break;
            case PlatoArrangementAction.StopArrangement:
                state.FixedSlotResolved = true;
                state.Stage = PlatoArrangementStage.ResultIncomplete;
                break;
            case PlatoArrangementAction.KeepSeparateWork:
                Complete(state, PlatoArrangementStage.ResultIndependent);
                break;
            case PlatoArrangementAction.ReturnChoiceAfterInspection:
                state.ChoiceReturnedAfterInspection = true;
                Complete(state, PlatoArrangementStage.ResultChoiceReturned);
                break;
            case PlatoArrangementAction.HonorDependentCommitment:
                state.DependentCommitmentHonored = true;
                Complete(state, PlatoArrangementStage.ResultJoint);
                break;
            case PlatoArrangementAction.WithdrawAfterCompanionChoice:
                state.PlayerWithdrewAfterCompanionChoice = true;
                Complete(state, PlatoArrangementStage.ResultSeparated);
                break;
            case PlatoArrangementAction.Leave:
                state.Closed = true;
                break;
        }
    }

    private static void Complete(PlatoWholeArrangementState state, PlatoArrangementStage result)
    {
        state.FixedSlotResolved = true;
        state.PracticeCompleted = true;
        state.EdgeCommitted = true;
        state.Stage = result;
    }

    private static string ToUpperSnake(string value) => string.Concat(
        value.SelectMany((character, index) => index > 0 && char.IsUpper(character)
            ? new[] { '_', character }
            : new[] { char.ToUpperInvariant(character) }));
}
