using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum DemocritusArrangementStage
{
    Invitation, Inquiry, MotionModel, QualityModel,
    VacantMotion, PackedMotion, RearrangedQuality, ChangedObserver,
    ResultAdopted, ResultKept, ResultStopped, ResultDeclined,
}

internal enum DemocritusArrangementAction
{
    None, AcceptInvitation, DeclineInvitation, StudyMotion, StudyQuality,
    LeaveGap, PackFull, RearrangeUnits, ChangeObserver,
    AdoptAtoms, KeepParmenides, StopInquiry, Leave,
}

internal sealed class DemocritusArrangementState : IJsonOnDeserialized
{
    public const string RequiredNodeId = "PARMENIDES__BEING_AND_CHANGE__CORE";
    public const string AdoptedNodeId = "DEMOCRITUS__BEING_AND_CHANGE__CORE";
    public const string EdgeId = "WESTERN_EDGE_005";
    public const string PrerequisiteEdgeId = "WESTERN_EDGE_001";

    // Missing saved fields remain invalid; only Create establishes a fresh invitation.
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public DemocritusArrangementStage Stage { get; set; } = (DemocritusArrangementStage)(-1);
    public DemocritusArrangementAction PendingAction { get; set; } = (DemocritusArrangementAction)(-1);
    public List<DemocritusArrangementAction> ConfirmedActions { get; set; } = null!;
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static DemocritusArrangementState Create(string instanceId) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        Stage = DemocritusArrangementStage.Invitation,
        PendingAction = DemocritusArrangementAction.None,
        ConfirmedActions = [],
        Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        // A missing false-valued field must not become valid after the next shared save.
        if (!_closed.HasValue) Version = 0;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || !_closed.HasValue
            || ConfirmedActions is null || ConfirmedActions.Count > 5
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        var replay = DemocritusArrangementStage.Invitation;
        bool closed = false;
        foreach (var action in ConfirmedActions)
        {
            if (closed || !DemocritusArrangementFlow.Options(replay).Contains(action)) return false;
            closed = action == DemocritusArrangementAction.Leave;
            replay = DemocritusArrangementFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == DemocritusArrangementAction.None
                || (!Closed && DemocritusArrangementFlow.Options(Stage).Contains(PendingAction)));
    }
}

internal static class DemocritusArrangementFlow
{
    public static IReadOnlyList<DemocritusArrangementAction> Options(DemocritusArrangementStage stage) => stage switch
    {
        DemocritusArrangementStage.Invitation =>
            [DemocritusArrangementAction.AcceptInvitation, DemocritusArrangementAction.DeclineInvitation],
        DemocritusArrangementStage.Inquiry =>
            [DemocritusArrangementAction.StudyMotion, DemocritusArrangementAction.StudyQuality, DemocritusArrangementAction.StopInquiry],
        DemocritusArrangementStage.MotionModel =>
            [DemocritusArrangementAction.LeaveGap, DemocritusArrangementAction.PackFull, DemocritusArrangementAction.StopInquiry],
        DemocritusArrangementStage.QualityModel =>
            [DemocritusArrangementAction.RearrangeUnits, DemocritusArrangementAction.ChangeObserver, DemocritusArrangementAction.StopInquiry],
        DemocritusArrangementStage.VacantMotion or DemocritusArrangementStage.PackedMotion
            or DemocritusArrangementStage.RearrangedQuality or DemocritusArrangementStage.ChangedObserver =>
            [DemocritusArrangementAction.AdoptAtoms, DemocritusArrangementAction.KeepParmenides],
        DemocritusArrangementStage.ResultAdopted or DemocritusArrangementStage.ResultKept
            or DemocritusArrangementStage.ResultStopped or DemocritusArrangementStage.ResultDeclined =>
            [DemocritusArrangementAction.Leave],
        _ => [],
    };

    internal static DemocritusArrangementStage Next(DemocritusArrangementStage stage, DemocritusArrangementAction action)
    {
        if (!Options(stage).Contains(action)) return stage;
        return action switch
        {
            DemocritusArrangementAction.AcceptInvitation => DemocritusArrangementStage.Inquiry,
            DemocritusArrangementAction.DeclineInvitation => DemocritusArrangementStage.ResultDeclined,
            DemocritusArrangementAction.StudyMotion => DemocritusArrangementStage.MotionModel,
            DemocritusArrangementAction.StudyQuality => DemocritusArrangementStage.QualityModel,
            DemocritusArrangementAction.LeaveGap => DemocritusArrangementStage.VacantMotion,
            DemocritusArrangementAction.PackFull => DemocritusArrangementStage.PackedMotion,
            DemocritusArrangementAction.RearrangeUnits => DemocritusArrangementStage.RearrangedQuality,
            DemocritusArrangementAction.ChangeObserver => DemocritusArrangementStage.ChangedObserver,
            DemocritusArrangementAction.AdoptAtoms => DemocritusArrangementStage.ResultAdopted,
            DemocritusArrangementAction.KeepParmenides => DemocritusArrangementStage.ResultKept,
            DemocritusArrangementAction.StopInquiry => DemocritusArrangementStage.ResultStopped,
            _ => stage,
        };
    }

    public static bool TryPrepare(DemocritusArrangementState state, DemocritusArrangementAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != DemocritusArrangementAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    private static bool ResolvesFixed(DemocritusArrangementAction action) => action is
        DemocritusArrangementAction.AdoptAtoms or DemocritusArrangementAction.KeepParmenides
        or DemocritusArrangementAction.StopInquiry or DemocritusArrangementAction.DeclineInvitation;

    private static bool ResolvedMatches(WesternJourneyState journey, bool adopted) =>
        !journey.RetainedEnding && journey.LastFixedAct == 2
        && journey.CompletedEdgeIds.Contains(DemocritusArrangementState.PrerequisiteEdgeId)
        && (adopted
            ? journey.CurrentNodeId == DemocritusArrangementState.AdoptedNodeId
                && journey.CompletedEdgeIds.Contains(DemocritusArrangementState.EdgeId)
                && !journey.DeclinedFixedActs.Contains(2)
            : journey.CurrentNodeId == DemocritusArrangementState.RequiredNodeId
                && journey.DeclinedFixedActs.Contains(2)
                && !journey.CompletedEdgeIds.Contains(DemocritusArrangementState.EdgeId));

    private static bool AwaitingFixed(WesternJourneyState journey, WesternRouteGraph graph) =>
        journey.LastFixedAct is 1 or 2 && journey.CurrentNodeId == DemocritusArrangementState.RequiredNodeId
        && !journey.RetainedEnding && journey.CompletedEdgeIds.Contains(DemocritusArrangementState.PrerequisiteEdgeId)
        && !journey.CompletedEdgeIds.Contains(DemocritusArrangementState.EdgeId)
        && !journey.DeclinedFixedActs.Contains(2)
        && graph.GetEligibleEdges(journey, "Fixed").Any(edge => edge.EdgeId == DemocritusArrangementState.EdgeId);

    public static bool IsJourneyConsistent(DemocritusArrangementState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!state.IsUsable()) return false;
        if (state.ConfirmedActions.Any(ResolvesFixed))
            return ResolvedMatches(journey, state.ConfirmedActions.Contains(DemocritusArrangementAction.AdoptAtoms));
        return AwaitingFixed(journey, graph)
            || (ResolvesFixed(state.PendingAction)
                && ResolvedMatches(journey, state.PendingAction == DemocritusArrangementAction.AdoptAtoms));
    }

    public static bool TryCommitPending(DemocritusArrangementState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (state.Closed || state.PendingAction == DemocritusArrangementAction.None
            || !IsJourneyConsistent(state, journey, graph)) return false;
        var action = state.PendingAction;
        bool wasResolved = state.ConfirmedActions.Any(ResolvesFixed);
        if (!wasResolved && ResolvesFixed(action)
            && !ResolvedMatches(journey, action == DemocritusArrangementAction.AdoptAtoms)
            && !(action == DemocritusArrangementAction.AdoptAtoms
                ? graph.TryApply(journey, DemocritusArrangementState.EdgeId) : graph.TryDecline(journey, "Fixed", 2))) return false;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == DemocritusArrangementAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = DemocritusArrangementAction.None;
        return true;
    }

    public static string StageKey(DemocritusArrangementStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(DemocritusArrangementAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
