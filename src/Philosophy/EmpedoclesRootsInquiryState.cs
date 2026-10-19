using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum EmpedoclesRootsInquiryStage
{
    Invitation, Burden, Composition, Separation,
    Mixed, Separate, Sorted, Held,
    ResultRecorded, ResultKept, ResultStopped, ResultDeclined,
}

internal enum EmpedoclesRootsInquiryAction
{
    None, AcceptInvitation, DeclineInvitation, StudyComposition, StudySeparation,
    CombineDifferent, KeepSeparated, SortLike, KeepCombined,
    RecordComparison, KeepAtoms, StopInquiry, Leave,
}

internal sealed class EmpedoclesRootsInquiryState : IJsonOnDeserialized
{
    public const string RequiredNodeId = "DEMOCRITUS__BEING_AND_CHANGE__CORE";
    public const string EdgeId = "WESTERN_EDGE_006";
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public EmpedoclesRootsInquiryStage Stage { get; set; } = (EmpedoclesRootsInquiryStage)(-1);
    public EmpedoclesRootsInquiryAction PendingAction { get; set; } = (EmpedoclesRootsInquiryAction)(-1);
    public List<EmpedoclesRootsInquiryAction> ConfirmedActions { get; set; } = null!;
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static EmpedoclesRootsInquiryState Create(string instanceId) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        Stage = EmpedoclesRootsInquiryStage.Invitation,
        PendingAction = EmpedoclesRootsInquiryAction.None,
        ConfirmedActions = [],
        Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || !_closed.HasValue || ConfirmedActions is null
            || ConfirmedActions.Count > 6 || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        var replay = EmpedoclesRootsInquiryStage.Invitation;
        bool closed = false;
        foreach (var action in ConfirmedActions)
        {
            if (closed || !EmpedoclesRootsInquiryFlow.Options(replay).Contains(action)) return false;
            closed = action == EmpedoclesRootsInquiryAction.Leave;
            replay = EmpedoclesRootsInquiryFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == EmpedoclesRootsInquiryAction.None
                || !Closed && EmpedoclesRootsInquiryFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class EmpedoclesRootsInquiryFlow
{
    public static IReadOnlyList<EmpedoclesRootsInquiryAction> Options(EmpedoclesRootsInquiryStage stage) => stage switch
    {
        EmpedoclesRootsInquiryStage.Invitation =>
            [EmpedoclesRootsInquiryAction.AcceptInvitation, EmpedoclesRootsInquiryAction.DeclineInvitation],
        EmpedoclesRootsInquiryStage.Burden =>
            [EmpedoclesRootsInquiryAction.StudyComposition, EmpedoclesRootsInquiryAction.StudySeparation,
                EmpedoclesRootsInquiryAction.StopInquiry],
        EmpedoclesRootsInquiryStage.Composition =>
            [EmpedoclesRootsInquiryAction.CombineDifferent, EmpedoclesRootsInquiryAction.KeepSeparated],
        EmpedoclesRootsInquiryStage.Separation =>
            [EmpedoclesRootsInquiryAction.SortLike, EmpedoclesRootsInquiryAction.KeepCombined],
        EmpedoclesRootsInquiryStage.Mixed or EmpedoclesRootsInquiryStage.Separate
            or EmpedoclesRootsInquiryStage.Sorted or EmpedoclesRootsInquiryStage.Held =>
            [EmpedoclesRootsInquiryAction.RecordComparison, EmpedoclesRootsInquiryAction.KeepAtoms],
        EmpedoclesRootsInquiryStage.ResultRecorded or EmpedoclesRootsInquiryStage.ResultKept
            or EmpedoclesRootsInquiryStage.ResultStopped or EmpedoclesRootsInquiryStage.ResultDeclined =>
            [EmpedoclesRootsInquiryAction.Leave],
        _ => [],
    };

    public static EmpedoclesRootsInquiryStage Next(
        EmpedoclesRootsInquiryStage stage, EmpedoclesRootsInquiryAction action) => action switch
    {
        EmpedoclesRootsInquiryAction.AcceptInvitation => EmpedoclesRootsInquiryStage.Burden,
        EmpedoclesRootsInquiryAction.DeclineInvitation => EmpedoclesRootsInquiryStage.ResultDeclined,
        EmpedoclesRootsInquiryAction.StudyComposition => EmpedoclesRootsInquiryStage.Composition,
        EmpedoclesRootsInquiryAction.StudySeparation => EmpedoclesRootsInquiryStage.Separation,
        EmpedoclesRootsInquiryAction.CombineDifferent => EmpedoclesRootsInquiryStage.Mixed,
        EmpedoclesRootsInquiryAction.KeepSeparated => EmpedoclesRootsInquiryStage.Separate,
        EmpedoclesRootsInquiryAction.SortLike => EmpedoclesRootsInquiryStage.Sorted,
        EmpedoclesRootsInquiryAction.KeepCombined => EmpedoclesRootsInquiryStage.Held,
        EmpedoclesRootsInquiryAction.RecordComparison => EmpedoclesRootsInquiryStage.ResultRecorded,
        EmpedoclesRootsInquiryAction.KeepAtoms => EmpedoclesRootsInquiryStage.ResultKept,
        EmpedoclesRootsInquiryAction.StopInquiry => EmpedoclesRootsInquiryStage.ResultStopped,
        _ => stage,
    };

    public static bool TryPrepare(EmpedoclesRootsInquiryState state, EmpedoclesRootsInquiryAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != EmpedoclesRootsInquiryAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool IsJourneyConsistent(
        EmpedoclesRootsInquiryState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!state.IsUsable() || journey.LastFixedAct != 2 || journey.CurrentNodeId != EmpedoclesRootsInquiryState.RequiredNodeId
            || !journey.CompletedEdgeIds.Contains(DemocritusArrangementState.EdgeId)
            || !journey.SeenThinkerIds.Contains("DEMOCRITUS") || journey.DeclinedFixedActs.Contains(2)
            || !journey.ResolvedQuestionActs.Contains(2) || journey.RetainedEnding) return false;
        bool recorded = state.ConfirmedActions.Contains(EmpedoclesRootsInquiryAction.RecordComparison);
        bool edge = journey.CompletedEdgeIds.Contains(EmpedoclesRootsInquiryState.EdgeId);
        bool seen = journey.SeenThinkerIds.Contains("EMPEDOCLES");
        if (edge != seen || (recorded && !edge)) return false;
        return !edge || recorded || state.PendingAction == EmpedoclesRootsInquiryAction.RecordComparison;
    }

    public static bool TryCommitPending(
        EmpedoclesRootsInquiryState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsJourneyConsistent(state, journey, graph) || state.Closed
            || state.PendingAction == EmpedoclesRootsInquiryAction.None) return false;
        EmpedoclesRootsInquiryAction action = state.PendingAction;
        if (action == EmpedoclesRootsInquiryAction.RecordComparison
            && !graph.TryCompleteReservedQuestion(journey, EmpedoclesRootsInquiryState.EdgeId)) return false;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == EmpedoclesRootsInquiryAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = EmpedoclesRootsInquiryAction.None;
        return true;
    }

    public static string StageKey(EmpedoclesRootsInquiryStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(EmpedoclesRootsInquiryAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
