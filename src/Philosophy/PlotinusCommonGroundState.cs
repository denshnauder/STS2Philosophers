namespace STS2Philosophers;

internal enum PlotinusGroundStage
{
    Invitation,
    Work,
    SharedEvidence,
    LocalEvidence,
    ResultDeclined,
    ResultQualifiedMap,
    ResultHandedOff,
    ResultLocalRevision,
    ResultPreservedVariants,
}

internal enum PlotinusGroundAction
{
    None,
    AcceptInvitation,
    DeclineInvitation,
    ExamineCommon,
    RepairLocal,
    DeliverQualifiedMap,
    HandOffLocalGap,
    DeliverLocalRevision,
    PreserveVariants,
    Leave,
}

internal sealed class PlotinusCommonGroundState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "PLATO__VIRTUE_AND_HAPPINESS__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_044";
    public const string RequiredEdgeId = "WESTERN_EDGE_009";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public PlotinusGroundStage Stage { get; set; }
    public PlotinusGroundAction PendingAction { get; set; }
    public List<PlotinusGroundAction> ConfirmedActions { get; set; } = [];
    public PlotinusGroundAction FirstWork { get; set; }
    public bool FixedSlotResolved { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Closed { get; set; }

    public static PlotinusCommonGroundState Create(string instanceId) => new()
    {
        InstanceId = instanceId,
        Stage = PlotinusGroundStage.Invitation,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(FirstWork) || ConfirmedActions is null
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || (Closed && PendingAction != PlotinusGroundAction.None)
            || (PendingAction != PlotinusGroundAction.None && !PlotinusCommonGroundFlow.Options(Stage).Contains(PendingAction))) return false;

        PlotinusGroundAction[] history = Closed
            ? ConfirmedActions.Take(ConfirmedActions.Count - 1).ToArray()
            : ConfirmedActions.ToArray();
        if (Closed && (ConfirmedActions.Count == 0 || ConfirmedActions[^1] != PlotinusGroundAction.Leave)) return false;
        PlotinusGroundAction[] expected = Stage switch
        {
            PlotinusGroundStage.Invitation => [],
            PlotinusGroundStage.Work => [PlotinusGroundAction.AcceptInvitation],
            PlotinusGroundStage.SharedEvidence => [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.ExamineCommon],
            PlotinusGroundStage.LocalEvidence => [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.RepairLocal],
            PlotinusGroundStage.ResultDeclined => [PlotinusGroundAction.DeclineInvitation],
            PlotinusGroundStage.ResultQualifiedMap => [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.ExamineCommon, PlotinusGroundAction.DeliverQualifiedMap],
            PlotinusGroundStage.ResultHandedOff => [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.ExamineCommon, PlotinusGroundAction.HandOffLocalGap],
            PlotinusGroundStage.ResultLocalRevision => [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.RepairLocal, PlotinusGroundAction.DeliverLocalRevision],
            PlotinusGroundStage.ResultPreservedVariants => [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.RepairLocal, PlotinusGroundAction.PreserveVariants],
            _ => [],
        };
        bool result = Stage is >= PlotinusGroundStage.ResultDeclined;
        bool adopted = Stage is PlotinusGroundStage.ResultQualifiedMap or PlotinusGroundStage.ResultPreservedVariants;
        PlotinusGroundAction first = Stage switch
        {
            PlotinusGroundStage.SharedEvidence or PlotinusGroundStage.ResultQualifiedMap or PlotinusGroundStage.ResultHandedOff => PlotinusGroundAction.ExamineCommon,
            PlotinusGroundStage.LocalEvidence or PlotinusGroundStage.ResultLocalRevision or PlotinusGroundStage.ResultPreservedVariants => PlotinusGroundAction.RepairLocal,
            _ => PlotinusGroundAction.None,
        };
        return history.SequenceEqual(expected) && FixedSlotResolved == result
            && EdgeCommitted == adopted && FirstWork == first
            && (!Closed || result);
    }
}

internal static class PlotinusCommonGroundFlow
{
    public static IReadOnlyList<PlotinusGroundAction> Options(PlotinusGroundStage stage) => stage switch
    {
        PlotinusGroundStage.Invitation => [PlotinusGroundAction.AcceptInvitation, PlotinusGroundAction.DeclineInvitation],
        PlotinusGroundStage.Work => [PlotinusGroundAction.ExamineCommon, PlotinusGroundAction.RepairLocal],
        PlotinusGroundStage.SharedEvidence => [PlotinusGroundAction.DeliverQualifiedMap, PlotinusGroundAction.HandOffLocalGap],
        PlotinusGroundStage.LocalEvidence => [PlotinusGroundAction.DeliverLocalRevision, PlotinusGroundAction.PreserveVariants],
        PlotinusGroundStage.ResultDeclined or PlotinusGroundStage.ResultQualifiedMap or PlotinusGroundStage.ResultHandedOff
            or PlotinusGroundStage.ResultLocalRevision or PlotinusGroundStage.ResultPreservedVariants => [PlotinusGroundAction.Leave],
        _ => [],
    };

    public static bool IsRouteConsistent(PlotinusCommonGroundState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || !journey.CompletedEdgeIds.Contains(PlotinusCommonGroundState.RequiredEdgeId)) return false;
        if (!state.FixedSlotResolved)
            return journey.LastFixedAct == 2 && journey.CurrentNodeId == PlotinusCommonGroundState.SourceNodeId
                && !journey.RetainedEnding && !journey.DeclinedFixedActs.Contains(3)
                && !journey.CompletedEdgeIds.Contains(PlotinusCommonGroundState.CompletionEdgeId);
        return journey.LastFixedAct == 3 && (state.EdgeCommitted
            ? journey.CurrentNodeId == "PLOTINUS__VIRTUE_AND_HAPPINESS__CORE"
                && journey.CompletedEdgeIds.Contains(PlotinusCommonGroundState.CompletionEdgeId)
            : journey.CurrentNodeId == PlotinusCommonGroundState.SourceNodeId
                && journey.DeclinedFixedActs.Contains(3)
                && !journey.CompletedEdgeIds.Contains(PlotinusCommonGroundState.CompletionEdgeId));
    }

    public static bool TryPrepare(PlotinusCommonGroundState state, PlotinusGroundAction action) =>
        state.IsUsable() && !state.Closed && state.PendingAction == PlotinusGroundAction.None
        && Options(state.Stage).Contains(action) && Prepare(state, action);

    private static bool Prepare(PlotinusCommonGroundState state, PlotinusGroundAction action)
    {
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(PlotinusCommonGroundState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == PlotinusGroundAction.None) return false;
        PlotinusGroundAction action = state.PendingAction;
        bool adopts = action is PlotinusGroundAction.DeliverQualifiedMap or PlotinusGroundAction.PreserveVariants;
        bool declines = action is PlotinusGroundAction.DeclineInvitation or PlotinusGroundAction.HandOffLocalGap
            or PlotinusGroundAction.DeliverLocalRevision;
        if (adopts && !graph.TryApply(journey, PlotinusCommonGroundState.CompletionEdgeId)
            || declines && !graph.TryDecline(journey, "Fixed", 3)) return false;
        switch (action)
        {
            case PlotinusGroundAction.AcceptInvitation:
                state.Stage = PlotinusGroundStage.Work;
                break;
            case PlotinusGroundAction.DeclineInvitation:
                state.Stage = PlotinusGroundStage.ResultDeclined;
                break;
            case PlotinusGroundAction.ExamineCommon:
                state.FirstWork = action;
                state.Stage = PlotinusGroundStage.SharedEvidence;
                break;
            case PlotinusGroundAction.RepairLocal:
                state.FirstWork = action;
                state.Stage = PlotinusGroundStage.LocalEvidence;
                break;
            case PlotinusGroundAction.DeliverQualifiedMap:
                state.Stage = PlotinusGroundStage.ResultQualifiedMap;
                break;
            case PlotinusGroundAction.HandOffLocalGap:
                state.Stage = PlotinusGroundStage.ResultHandedOff;
                break;
            case PlotinusGroundAction.DeliverLocalRevision:
                state.Stage = PlotinusGroundStage.ResultLocalRevision;
                break;
            case PlotinusGroundAction.PreserveVariants:
                state.Stage = PlotinusGroundStage.ResultPreservedVariants;
                break;
            case PlotinusGroundAction.Leave:
                state.Closed = true;
                break;
            default:
                return false;
        }
        state.FixedSlotResolved |= adopts || declines;
        state.EdgeCommitted |= adopts;
        state.ConfirmedActions.Add(action);
        state.PendingAction = PlotinusGroundAction.None;
        return IsRouteConsistent(state, journey);
    }

    public static string StageKey(PlotinusGroundStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(PlotinusGroundAction action) => UpperSnake(action.ToString());

    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
