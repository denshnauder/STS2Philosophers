using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum MachiavelliEmergencyMandateStage
{
    Invitation, MandateChoice, EmergencyChange, CouncilChange,
    ReturnedMandate, ExtendedMandate, ReopenedObjections, EnforcedDecision,
    ResultCompleted, ResultStopped, ResultDeclined,
}

internal enum MachiavelliEmergencyMandateAction
{
    None, AcceptInvitation, DeclineInvitation, AcceptMandate, ChooseCouncil,
    ReturnMandate, ExtendMandate, ReopenObjections, EnforceDecision,
    CompleteChallenge, StopInquiry, Leave,
}

internal sealed class MachiavelliEmergencyMandateState : IJsonOnDeserialized
{
    public const string RequiredNodeId = "ARISTOTLE__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string EdgeId = "WESTERN_EDGE_017";
    public const string PrerequisiteEdgeId = "WESTERN_EDGE_016";

    // Only Create establishes a new invitation; absent saved fields stay unusable.
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public MachiavelliEmergencyMandateStage Stage { get; set; } = (MachiavelliEmergencyMandateStage)(-1);
    public MachiavelliEmergencyMandateAction PendingAction { get; set; } = (MachiavelliEmergencyMandateAction)(-1);
    public List<MachiavelliEmergencyMandateAction> ConfirmedActions { get; set; } = null!;
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static MachiavelliEmergencyMandateState Create(string instanceId) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        Stage = MachiavelliEmergencyMandateStage.Invitation,
        PendingAction = MachiavelliEmergencyMandateAction.None,
        ConfirmedActions = [],
        Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        // Preserve a missing false-valued field as invalid across subsequent saves.
        if (!_closed.HasValue) Version = 0;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || !_closed.HasValue
            || ConfirmedActions is null || ConfirmedActions.Count > 5
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        var replay = MachiavelliEmergencyMandateStage.Invitation;
        bool closed = false;
        foreach (var action in ConfirmedActions)
        {
            if (closed || !MachiavelliEmergencyMandateFlow.Options(replay).Contains(action)) return false;
            closed = action == MachiavelliEmergencyMandateAction.Leave;
            replay = MachiavelliEmergencyMandateFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == MachiavelliEmergencyMandateAction.None
                || (!Closed && MachiavelliEmergencyMandateFlow.Options(Stage).Contains(PendingAction)));
    }
}

internal static class MachiavelliEmergencyMandateFlow
{
    public static IReadOnlyList<MachiavelliEmergencyMandateAction> Options(MachiavelliEmergencyMandateStage stage) => stage switch
    {
        MachiavelliEmergencyMandateStage.Invitation =>
            [MachiavelliEmergencyMandateAction.AcceptInvitation, MachiavelliEmergencyMandateAction.DeclineInvitation],
        MachiavelliEmergencyMandateStage.MandateChoice =>
            [MachiavelliEmergencyMandateAction.AcceptMandate, MachiavelliEmergencyMandateAction.ChooseCouncil, MachiavelliEmergencyMandateAction.StopInquiry],
        MachiavelliEmergencyMandateStage.EmergencyChange =>
            [MachiavelliEmergencyMandateAction.ReturnMandate, MachiavelliEmergencyMandateAction.ExtendMandate, MachiavelliEmergencyMandateAction.StopInquiry],
        MachiavelliEmergencyMandateStage.CouncilChange =>
            [MachiavelliEmergencyMandateAction.ReopenObjections, MachiavelliEmergencyMandateAction.EnforceDecision, MachiavelliEmergencyMandateAction.StopInquiry],
        MachiavelliEmergencyMandateStage.ReturnedMandate or MachiavelliEmergencyMandateStage.ExtendedMandate
            or MachiavelliEmergencyMandateStage.ReopenedObjections or MachiavelliEmergencyMandateStage.EnforcedDecision =>
            [MachiavelliEmergencyMandateAction.CompleteChallenge, MachiavelliEmergencyMandateAction.StopInquiry],
        MachiavelliEmergencyMandateStage.ResultCompleted or MachiavelliEmergencyMandateStage.ResultStopped
            or MachiavelliEmergencyMandateStage.ResultDeclined => [MachiavelliEmergencyMandateAction.Leave],
        _ => [],
    };

    internal static MachiavelliEmergencyMandateStage Next(MachiavelliEmergencyMandateStage stage, MachiavelliEmergencyMandateAction action)
    {
        if (!Options(stage).Contains(action)) return stage;
        return action switch
        {
            MachiavelliEmergencyMandateAction.AcceptInvitation => MachiavelliEmergencyMandateStage.MandateChoice,
            MachiavelliEmergencyMandateAction.DeclineInvitation => MachiavelliEmergencyMandateStage.ResultDeclined,
            MachiavelliEmergencyMandateAction.AcceptMandate => MachiavelliEmergencyMandateStage.EmergencyChange,
            MachiavelliEmergencyMandateAction.ChooseCouncil => MachiavelliEmergencyMandateStage.CouncilChange,
            MachiavelliEmergencyMandateAction.ReturnMandate => MachiavelliEmergencyMandateStage.ReturnedMandate,
            MachiavelliEmergencyMandateAction.ExtendMandate => MachiavelliEmergencyMandateStage.ExtendedMandate,
            MachiavelliEmergencyMandateAction.ReopenObjections => MachiavelliEmergencyMandateStage.ReopenedObjections,
            MachiavelliEmergencyMandateAction.EnforceDecision => MachiavelliEmergencyMandateStage.EnforcedDecision,
            MachiavelliEmergencyMandateAction.CompleteChallenge => MachiavelliEmergencyMandateStage.ResultCompleted,
            MachiavelliEmergencyMandateAction.StopInquiry => MachiavelliEmergencyMandateStage.ResultStopped,
            _ => stage,
        };
    }

    public static bool TryPrepare(MachiavelliEmergencyMandateState state, MachiavelliEmergencyMandateAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != MachiavelliEmergencyMandateAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool IsJourneyConsistent(MachiavelliEmergencyMandateState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!state.IsUsable() || journey.LastFixedAct != 2 || journey.RetainedEnding
            || journey.CurrentNodeId != MachiavelliEmergencyMandateState.RequiredNodeId
            || !journey.CompletedEdgeIds.Contains(MachiavelliEmergencyMandateState.PrerequisiteEdgeId)
            || journey.DeclinedFixedActs.Contains(2)
            || !journey.ResolvedQuestionActs.Contains(2)) return false;
        bool completed = journey.CompletedEdgeIds.Contains(MachiavelliEmergencyMandateState.EdgeId);
        string thinker = graph.GetNode(graph.GetEdge(MachiavelliEmergencyMandateState.EdgeId).ToNodeId).ThinkerId;
        if (completed != journey.SeenThinkerIds.Contains(thinker)) return false;
        if (state.ConfirmedActions.Contains(MachiavelliEmergencyMandateAction.CompleteChallenge)) return completed;
        return !completed || state.PendingAction == MachiavelliEmergencyMandateAction.CompleteChallenge;
    }

    public static bool TryCommitPending(MachiavelliEmergencyMandateState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (state.Closed || state.PendingAction == MachiavelliEmergencyMandateAction.None
            || !IsJourneyConsistent(state, journey, graph)) return false;
        var action = state.PendingAction;
        if (action == MachiavelliEmergencyMandateAction.CompleteChallenge
            && !journey.CompletedEdgeIds.Contains(MachiavelliEmergencyMandateState.EdgeId)
            && !graph.TryCompleteReservedQuestion(journey, MachiavelliEmergencyMandateState.EdgeId)) return false;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == MachiavelliEmergencyMandateAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = MachiavelliEmergencyMandateAction.None;
        return true;
    }

    public static string StageKey(MachiavelliEmergencyMandateStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(MachiavelliEmergencyMandateAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
