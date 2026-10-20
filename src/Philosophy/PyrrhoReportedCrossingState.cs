using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum PyrrhoReportedCrossingStage
{
    Invitation, ReportCautious, ReportOverstated,
    ResultCautiousInspect, ResultCautiousGuide, ResultOverstatedInspect,
    ResultOverstatedGuide, ResultDeclined,
}

internal enum PyrrhoReportedCrossingAction
{
    None, CiteConflict, ClaimDirectRule, Decline,
    InspectAndCross, AskGuideAndCross, Leave,
}

internal sealed class PyrrhoReportedCrossingState : IJsonOnDeserialized
{
    public const int QuestionAct = 2;
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string SextusInstanceId { get; set; } = string.Empty;
    public string DescartesInstanceId { get; set; } = string.Empty;
    public bool QuestionSlotReserved { get; set; }
    public PyrrhoReportedCrossingStage Stage { get; set; } = (PyrrhoReportedCrossingStage)(-1);
    public PyrrhoReportedCrossingAction PendingAction { get; set; } = (PyrrhoReportedCrossingAction)(-1);
    public List<PyrrhoReportedCrossingAction> ConfirmedActions { get; set; } = null!;
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static PyrrhoReportedCrossingState Create(string instanceId,
        SextusOpposedAppearancesState sextus, DescartesMethodicReconstructionState descartes) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        SextusInstanceId = sextus.InstanceId,
        DescartesInstanceId = descartes.InstanceId,
        QuestionSlotReserved = true,
        Stage = PyrrhoReportedCrossingStage.Invitation,
        PendingAction = PyrrhoReportedCrossingAction.None,
        ConfirmedActions = [],
        Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        SextusInstanceId ??= string.Empty;
        DescartesInstanceId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(SextusInstanceId)
            || string.IsNullOrWhiteSpace(DescartesInstanceId) || !QuestionSlotReserved || !_closed.HasValue || !Enum.IsDefined(Stage)
            || !Enum.IsDefined(PendingAction) || ConfirmedActions is null
            || ConfirmedActions.Count > 3) return false;
        PyrrhoReportedCrossingStage replay = PyrrhoReportedCrossingStage.Invitation;
        bool closed = false;
        foreach (PyrrhoReportedCrossingAction action in ConfirmedActions)
        {
            if (closed || !PyrrhoReportedCrossingFlow.Options(replay).Contains(action)) return false;
            closed = action == PyrrhoReportedCrossingAction.Leave;
            replay = PyrrhoReportedCrossingFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == PyrrhoReportedCrossingAction.None
                || !Closed && PyrrhoReportedCrossingFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class PyrrhoReportedCrossingFlow
{
    public static IReadOnlyList<PyrrhoReportedCrossingAction> Options(PyrrhoReportedCrossingStage stage) => stage switch
    {
        PyrrhoReportedCrossingStage.Invitation =>
            [PyrrhoReportedCrossingAction.CiteConflict,
                PyrrhoReportedCrossingAction.ClaimDirectRule, PyrrhoReportedCrossingAction.Decline],
        PyrrhoReportedCrossingStage.ReportCautious or PyrrhoReportedCrossingStage.ReportOverstated =>
            [PyrrhoReportedCrossingAction.InspectAndCross, PyrrhoReportedCrossingAction.AskGuideAndCross],
        PyrrhoReportedCrossingStage.ResultCautiousInspect or PyrrhoReportedCrossingStage.ResultCautiousGuide
            or PyrrhoReportedCrossingStage.ResultOverstatedInspect or PyrrhoReportedCrossingStage.ResultOverstatedGuide
            or PyrrhoReportedCrossingStage.ResultDeclined => [PyrrhoReportedCrossingAction.Leave],
        _ => [],
    };

    public static PyrrhoReportedCrossingStage Next(PyrrhoReportedCrossingStage stage,
        PyrrhoReportedCrossingAction action) => (stage, action) switch
    {
        (PyrrhoReportedCrossingStage.Invitation, PyrrhoReportedCrossingAction.CiteConflict) =>
            PyrrhoReportedCrossingStage.ReportCautious,
        (PyrrhoReportedCrossingStage.Invitation, PyrrhoReportedCrossingAction.ClaimDirectRule) =>
            PyrrhoReportedCrossingStage.ReportOverstated,
        (PyrrhoReportedCrossingStage.Invitation, PyrrhoReportedCrossingAction.Decline) =>
            PyrrhoReportedCrossingStage.ResultDeclined,
        (PyrrhoReportedCrossingStage.ReportCautious, PyrrhoReportedCrossingAction.InspectAndCross) =>
            PyrrhoReportedCrossingStage.ResultCautiousInspect,
        (PyrrhoReportedCrossingStage.ReportCautious, PyrrhoReportedCrossingAction.AskGuideAndCross) =>
            PyrrhoReportedCrossingStage.ResultCautiousGuide,
        (PyrrhoReportedCrossingStage.ReportOverstated, PyrrhoReportedCrossingAction.InspectAndCross) =>
            PyrrhoReportedCrossingStage.ResultOverstatedInspect,
        (PyrrhoReportedCrossingStage.ReportOverstated, PyrrhoReportedCrossingAction.AskGuideAndCross) =>
            PyrrhoReportedCrossingStage.ResultOverstatedGuide,
        _ => stage,
    };

    public static bool TryPrepare(PyrrhoReportedCrossingState state, PyrrhoReportedCrossingAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != PyrrhoReportedCrossingAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool IsRouteConsistent(PyrrhoReportedCrossingState state, WesternJourneyState journey,
        SextusOpposedAppearancesState sextus, DescartesMethodicReconstructionState descartes) => state.IsUsable()
        && journey.ResolvedQuestionActs.Contains(1) && journey.ResolvedQuestionActs.Contains(2)
        && journey.DeclinedFixedActs.Contains(2) && journey.SeenThinkerIds.Contains("SOCRATES")
        && sextus.IsUsable() && sextus.Closed && sextus.InstanceId == state.SextusInstanceId
        && descartes.IsUsable() && descartes.Closed
        && descartes.Stage == DescartesReconstructionStage.ResultDeclined
        && descartes.InstanceId == state.DescartesInstanceId;

    public static bool TryCommitPending(PyrrhoReportedCrossingState state, WesternJourneyState journey,
        SextusOpposedAppearancesState sextus, DescartesMethodicReconstructionState descartes)
    {
        if (!IsRouteConsistent(state, journey, sextus, descartes) || state.Closed
            || state.PendingAction == PyrrhoReportedCrossingAction.None) return false;
        PyrrhoReportedCrossingAction action = state.PendingAction;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == PyrrhoReportedCrossingAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = PyrrhoReportedCrossingAction.None;
        return true;
    }

    public static string StageKey(PyrrhoReportedCrossingStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(PyrrhoReportedCrossingAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
