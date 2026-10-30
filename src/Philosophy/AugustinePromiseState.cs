namespace STS2Philosophers;

internal enum AugustinePromiseStage
{
    Invitation,
    Promise,
    NewFact,
    ResultDeclined,
    ResultPublic,
    ResultPrivate,
    ResultUnused,
}

internal enum AugustinePromiseAction
{
    None,
    AcceptInvitation,
    DeclineInvitation,
    PromisePublic,
    PromisePrivate,
    RemainUndeclared,
    RepairPublic,
    RepairPrivate,
    UseNoCrew,
    Leave,
}

internal enum AugustinePledge { None, Public, Private, Undeclared }
internal enum AugustineWork { None, Public, Private, Unused }

internal sealed class AugustinePromiseState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "PLATO__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string AdoptedNodeId = "AUGUSTINE_OF_HIPPO__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_019";
    public const string CompletionEdgeId = "WESTERN_EDGE_020";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public AugustinePromiseStage Stage { get; set; }
    public AugustinePromiseAction PendingAction { get; set; }
    public List<AugustinePromiseAction> ConfirmedActions { get; set; } = [];
    public AugustinePledge Pledge { get; set; }
    public AugustineWork Work { get; set; }
    public bool FixedSlotResolved { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Closed { get; set; }

    public static AugustinePromiseState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Pledge) || !Enum.IsDefined(Work)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != AugustinePromiseAction.None
            || PendingAction != AugustinePromiseAction.None
                && !AugustinePromiseFlow.Options(Stage).Contains(PendingAction)) return false;

        AugustinePromiseAction[] history = Closed
            ? ConfirmedActions.Take(ConfirmedActions.Count - 1).ToArray()
            : ConfirmedActions.ToArray();
        if (Closed && (ConfirmedActions.Count == 0 || ConfirmedActions[^1] != AugustinePromiseAction.Leave))
            return false;
        AugustinePromiseAction pledgeAction = Pledge switch
        {
            AugustinePledge.Public => AugustinePromiseAction.PromisePublic,
            AugustinePledge.Private => AugustinePromiseAction.PromisePrivate,
            AugustinePledge.Undeclared => AugustinePromiseAction.RemainUndeclared,
            _ => AugustinePromiseAction.None,
        };
        AugustinePromiseAction[] expected = Stage switch
        {
            AugustinePromiseStage.Invitation => [],
            AugustinePromiseStage.Promise => [AugustinePromiseAction.AcceptInvitation],
            AugustinePromiseStage.NewFact when pledgeAction != AugustinePromiseAction.None =>
                [AugustinePromiseAction.AcceptInvitation, pledgeAction],
            AugustinePromiseStage.ResultDeclined => [AugustinePromiseAction.DeclineInvitation],
            AugustinePromiseStage.ResultPublic when pledgeAction != AugustinePromiseAction.None =>
                [AugustinePromiseAction.AcceptInvitation, pledgeAction, AugustinePromiseAction.RepairPublic],
            AugustinePromiseStage.ResultPrivate when pledgeAction != AugustinePromiseAction.None =>
                [AugustinePromiseAction.AcceptInvitation, pledgeAction, AugustinePromiseAction.RepairPrivate],
            AugustinePromiseStage.ResultUnused when pledgeAction != AugustinePromiseAction.None =>
                [AugustinePromiseAction.AcceptInvitation, pledgeAction, AugustinePromiseAction.UseNoCrew],
            _ => [AugustinePromiseAction.None],
        };
        bool result = Stage >= AugustinePromiseStage.ResultDeclined;
        bool adopted = Stage is AugustinePromiseStage.ResultPublic or AugustinePromiseStage.ResultPrivate;
        return history.SequenceEqual(expected) && FixedSlotResolved == result
            && EdgeCommitted == adopted
            && Work == (result && Stage != AugustinePromiseStage.ResultDeclined
                ? Stage switch
                {
                    AugustinePromiseStage.ResultPublic => AugustineWork.Public,
                    AugustinePromiseStage.ResultPrivate => AugustineWork.Private,
                    _ => AugustineWork.Unused,
                }
                : AugustineWork.None)
            && (Stage is not AugustinePromiseStage.Invitation and not AugustinePromiseStage.Promise
                || Pledge == AugustinePledge.None)
            && (Stage != AugustinePromiseStage.ResultDeclined || Pledge == AugustinePledge.None)
            && (!Closed || result);
    }
}

internal static class AugustinePromiseFlow
{
    public static IReadOnlyList<AugustinePromiseAction> Options(AugustinePromiseStage stage) => stage switch
    {
        AugustinePromiseStage.Invitation => [AugustinePromiseAction.AcceptInvitation, AugustinePromiseAction.DeclineInvitation],
        AugustinePromiseStage.Promise => [AugustinePromiseAction.PromisePublic,
            AugustinePromiseAction.PromisePrivate, AugustinePromiseAction.RemainUndeclared],
        AugustinePromiseStage.NewFact => [AugustinePromiseAction.RepairPublic,
            AugustinePromiseAction.RepairPrivate, AugustinePromiseAction.UseNoCrew],
        >= AugustinePromiseStage.ResultDeclined and <= AugustinePromiseStage.ResultUnused => [AugustinePromiseAction.Leave],
        _ => [],
    };

    public static bool IsRouteConsistent(AugustinePromiseState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || !journey.CompletedEdgeIds.Contains(AugustinePromiseState.RequiredEdgeId)
            || !journey.ResolvedQuestionActs.Contains(1) || journey.RetainedEnding) return false;
        if (!state.FixedSlotResolved)
            return journey.LastFixedAct is 1 or 2 && journey.CurrentNodeId == AugustinePromiseState.SourceNodeId
                && !journey.DeclinedFixedActs.Contains(2)
                && !journey.CompletedEdgeIds.Contains(AugustinePromiseState.CompletionEdgeId);
        return journey.LastFixedAct is 2 or 3 && (state.EdgeCommitted
            ? journey.CurrentNodeId == AugustinePromiseState.AdoptedNodeId
                && journey.CompletedEdgeIds.Contains(AugustinePromiseState.CompletionEdgeId)
            : journey.CurrentNodeId == AugustinePromiseState.SourceNodeId
                && journey.DeclinedFixedActs.Contains(2)
                && !journey.CompletedEdgeIds.Contains(AugustinePromiseState.CompletionEdgeId));
    }

    public static bool TryPrepare(AugustinePromiseState state, AugustinePromiseAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != AugustinePromiseAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(AugustinePromiseState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == AugustinePromiseAction.None) return false;
        AugustinePromiseAction action = state.PendingAction;
        bool adopts = action is AugustinePromiseAction.RepairPublic or AugustinePromiseAction.RepairPrivate;
        bool declines = action is AugustinePromiseAction.DeclineInvitation or AugustinePromiseAction.UseNoCrew;
        if (adopts && !graph.TryApply(journey, AugustinePromiseState.CompletionEdgeId)
            || declines && !graph.TryDecline(journey, "Fixed", 2)) return false;
        switch (action)
        {
            case AugustinePromiseAction.AcceptInvitation: state.Stage = AugustinePromiseStage.Promise; break;
            case AugustinePromiseAction.DeclineInvitation: state.Stage = AugustinePromiseStage.ResultDeclined; break;
            case AugustinePromiseAction.PromisePublic:
                state.Pledge = AugustinePledge.Public; state.Stage = AugustinePromiseStage.NewFact; break;
            case AugustinePromiseAction.PromisePrivate:
                state.Pledge = AugustinePledge.Private; state.Stage = AugustinePromiseStage.NewFact; break;
            case AugustinePromiseAction.RemainUndeclared:
                state.Pledge = AugustinePledge.Undeclared; state.Stage = AugustinePromiseStage.NewFact; break;
            case AugustinePromiseAction.RepairPublic:
                state.Work = AugustineWork.Public; state.Stage = AugustinePromiseStage.ResultPublic; break;
            case AugustinePromiseAction.RepairPrivate:
                state.Work = AugustineWork.Private; state.Stage = AugustinePromiseStage.ResultPrivate; break;
            case AugustinePromiseAction.UseNoCrew:
                state.Work = AugustineWork.Unused; state.Stage = AugustinePromiseStage.ResultUnused; break;
            case AugustinePromiseAction.Leave: state.Closed = true; break;
            default: return false;
        }
        state.FixedSlotResolved |= adopts || declines;
        state.EdgeCommitted |= adopts;
        state.ConfirmedActions.Add(action);
        state.PendingAction = AugustinePromiseAction.None;
        return IsRouteConsistent(state, journey);
    }

    public static string StageKey(AugustinePromiseStage stage) => UpperSnake(stage.ToString());

    public static string DescriptionKey(AugustinePromiseState state)
    {
        string stage = StageKey(state.Stage);
        return state.Stage is AugustinePromiseStage.NewFact or AugustinePromiseStage.ResultPublic
            or AugustinePromiseStage.ResultPrivate or AugustinePromiseStage.ResultUnused
            ? $"{stage}_{UpperSnake(state.Pledge.ToString())}" : stage;
    }

    public static string ActionKey(AugustinePromiseAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
