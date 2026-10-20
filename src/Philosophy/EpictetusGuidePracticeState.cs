using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum EpictetusGuidePracticeStage
{
    Invitation, GateClosedReserved, GateClosedPromised,
    ResultReservedEscort, ResultReservedMark, ResultPromisedEscort,
    ResultPromisedMark, ResultDeclined,
}

internal enum EpictetusGuidePracticeAction
{
    None, GuideWithReservation, PromisePassage, Decline,
    EscortCompanion, MarkThenReturn, Leave,
}

internal sealed class EpictetusGuidePracticeState : IJsonOnDeserialized
{
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public ZenoAssentOutcome ZenoOutcome { get; set; } = (ZenoAssentOutcome)(-1);
    public string SenecaInstanceId { get; set; } = string.Empty;
    public EpictetusGuidePracticeStage Stage { get; set; } = (EpictetusGuidePracticeStage)(-1);
    public EpictetusGuidePracticeAction PendingAction { get; set; } = (EpictetusGuidePracticeAction)(-1);
    public List<EpictetusGuidePracticeAction> ConfirmedActions { get; set; } = null!;
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static EpictetusGuidePracticeState Create(string instanceId, ZenoRouteState route,
        SenecaDailyReviewState seneca) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        RunId = route.RunId,
        ZenoOutcome = route.Outcome!.Value,
        SenecaInstanceId = seneca.InstanceId,
        Stage = EpictetusGuidePracticeStage.Invitation,
        PendingAction = EpictetusGuidePracticeAction.None,
        ConfirmedActions = [],
        Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        RunId ??= string.Empty;
        SenecaInstanceId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(RunId)
            || string.IsNullOrWhiteSpace(SenecaInstanceId) || !_closed.HasValue
            || !Enum.IsDefined(ZenoOutcome) || !Enum.IsDefined(Stage)
            || !Enum.IsDefined(PendingAction) || ConfirmedActions is null
            || ConfirmedActions.Count > 3) return false;
        EpictetusGuidePracticeStage replay = EpictetusGuidePracticeStage.Invitation;
        bool closed = false;
        foreach (EpictetusGuidePracticeAction action in ConfirmedActions)
        {
            if (closed || !EpictetusGuidePracticeFlow.Options(replay).Contains(action)) return false;
            closed = action == EpictetusGuidePracticeAction.Leave;
            replay = EpictetusGuidePracticeFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == EpictetusGuidePracticeAction.None
                || !Closed && EpictetusGuidePracticeFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class EpictetusGuidePracticeFlow
{
    public static IReadOnlyList<EpictetusGuidePracticeAction> Options(EpictetusGuidePracticeStage stage) => stage switch
    {
        EpictetusGuidePracticeStage.Invitation =>
            [EpictetusGuidePracticeAction.GuideWithReservation,
                EpictetusGuidePracticeAction.PromisePassage, EpictetusGuidePracticeAction.Decline],
        EpictetusGuidePracticeStage.GateClosedReserved or EpictetusGuidePracticeStage.GateClosedPromised =>
            [EpictetusGuidePracticeAction.EscortCompanion, EpictetusGuidePracticeAction.MarkThenReturn],
        EpictetusGuidePracticeStage.ResultReservedEscort or EpictetusGuidePracticeStage.ResultReservedMark
            or EpictetusGuidePracticeStage.ResultPromisedEscort or EpictetusGuidePracticeStage.ResultPromisedMark
            or EpictetusGuidePracticeStage.ResultDeclined => [EpictetusGuidePracticeAction.Leave],
        _ => [],
    };

    public static EpictetusGuidePracticeStage Next(EpictetusGuidePracticeStage stage,
        EpictetusGuidePracticeAction action) => (stage, action) switch
    {
        (EpictetusGuidePracticeStage.Invitation, EpictetusGuidePracticeAction.GuideWithReservation) =>
            EpictetusGuidePracticeStage.GateClosedReserved,
        (EpictetusGuidePracticeStage.Invitation, EpictetusGuidePracticeAction.PromisePassage) =>
            EpictetusGuidePracticeStage.GateClosedPromised,
        (EpictetusGuidePracticeStage.Invitation, EpictetusGuidePracticeAction.Decline) =>
            EpictetusGuidePracticeStage.ResultDeclined,
        (EpictetusGuidePracticeStage.GateClosedReserved, EpictetusGuidePracticeAction.EscortCompanion) =>
            EpictetusGuidePracticeStage.ResultReservedEscort,
        (EpictetusGuidePracticeStage.GateClosedReserved, EpictetusGuidePracticeAction.MarkThenReturn) =>
            EpictetusGuidePracticeStage.ResultReservedMark,
        (EpictetusGuidePracticeStage.GateClosedPromised, EpictetusGuidePracticeAction.EscortCompanion) =>
            EpictetusGuidePracticeStage.ResultPromisedEscort,
        (EpictetusGuidePracticeStage.GateClosedPromised, EpictetusGuidePracticeAction.MarkThenReturn) =>
            EpictetusGuidePracticeStage.ResultPromisedMark,
        _ => stage,
    };

    public static bool TryPrepare(EpictetusGuidePracticeState state, EpictetusGuidePracticeAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != EpictetusGuidePracticeAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool IsRouteConsistent(EpictetusGuidePracticeState state, ZenoRouteState route,
        SenecaDailyReviewState seneca) => state.IsUsable()
        && route.Stage == ZenoRouteStage.ZenoClosed && route.Outcome == state.ZenoOutcome
        && route.RunId == state.RunId && seneca.RunId == state.RunId
        && seneca.Closed && seneca.InstanceId == state.SenecaInstanceId;

    public static bool TryCommitPending(EpictetusGuidePracticeState state, ZenoRouteState route,
        SenecaDailyReviewState seneca)
    {
        if (!IsRouteConsistent(state, route, seneca) || state.Closed
            || state.PendingAction == EpictetusGuidePracticeAction.None) return false;
        EpictetusGuidePracticeAction action = state.PendingAction;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == EpictetusGuidePracticeAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = EpictetusGuidePracticeAction.None;
        return true;
    }

    public static string StageKey(EpictetusGuidePracticeStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(EpictetusGuidePracticeAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
