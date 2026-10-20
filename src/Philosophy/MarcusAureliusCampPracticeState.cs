using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum MarcusAureliusCampPracticeStage
{
    Invitation, ResponseAsked, ResponseStated,
    ResultAskedWork, ResultAskedOrganize, ResultStatedWork,
    ResultStatedOrganize, ResultDeclined,
}

internal enum MarcusAureliusCampPracticeAction
{
    None, AskNeed, StatePlan, Decline,
    WorkTogether, OrganizeAndMark, Leave,
}

internal sealed class MarcusAureliusCampPracticeState : IJsonOnDeserialized
{
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public ZenoAssentOutcome ZenoOutcome { get; set; } = (ZenoAssentOutcome)(-1);
    public string EpictetusInstanceId { get; set; } = string.Empty;
    public MarcusAureliusCampPracticeStage Stage { get; set; } = (MarcusAureliusCampPracticeStage)(-1);
    public MarcusAureliusCampPracticeAction PendingAction { get; set; } = (MarcusAureliusCampPracticeAction)(-1);
    public List<MarcusAureliusCampPracticeAction> ConfirmedActions { get; set; } = null!;
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static MarcusAureliusCampPracticeState Create(string instanceId, ZenoRouteState route,
        EpictetusGuidePracticeState epictetus) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        RunId = route.RunId,
        ZenoOutcome = route.Outcome!.Value,
        EpictetusInstanceId = epictetus.InstanceId,
        Stage = MarcusAureliusCampPracticeStage.Invitation,
        PendingAction = MarcusAureliusCampPracticeAction.None,
        ConfirmedActions = [],
        Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        RunId ??= string.Empty;
        EpictetusInstanceId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(RunId)
            || string.IsNullOrWhiteSpace(EpictetusInstanceId) || !_closed.HasValue
            || !Enum.IsDefined(ZenoOutcome) || !Enum.IsDefined(Stage)
            || !Enum.IsDefined(PendingAction) || ConfirmedActions is null
            || ConfirmedActions.Count > 3) return false;
        MarcusAureliusCampPracticeStage replay = MarcusAureliusCampPracticeStage.Invitation;
        bool closed = false;
        foreach (MarcusAureliusCampPracticeAction action in ConfirmedActions)
        {
            if (closed || !MarcusAureliusCampPracticeFlow.Options(replay).Contains(action)) return false;
            closed = action == MarcusAureliusCampPracticeAction.Leave;
            replay = MarcusAureliusCampPracticeFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == MarcusAureliusCampPracticeAction.None
                || !Closed && MarcusAureliusCampPracticeFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class MarcusAureliusCampPracticeFlow
{
    public static IReadOnlyList<MarcusAureliusCampPracticeAction> Options(MarcusAureliusCampPracticeStage stage) => stage switch
    {
        MarcusAureliusCampPracticeStage.Invitation =>
            [MarcusAureliusCampPracticeAction.AskNeed,
                MarcusAureliusCampPracticeAction.StatePlan, MarcusAureliusCampPracticeAction.Decline],
        MarcusAureliusCampPracticeStage.ResponseAsked or MarcusAureliusCampPracticeStage.ResponseStated =>
            [MarcusAureliusCampPracticeAction.WorkTogether, MarcusAureliusCampPracticeAction.OrganizeAndMark],
        MarcusAureliusCampPracticeStage.ResultAskedWork or MarcusAureliusCampPracticeStage.ResultAskedOrganize
            or MarcusAureliusCampPracticeStage.ResultStatedWork or MarcusAureliusCampPracticeStage.ResultStatedOrganize
            or MarcusAureliusCampPracticeStage.ResultDeclined => [MarcusAureliusCampPracticeAction.Leave],
        _ => [],
    };

    public static MarcusAureliusCampPracticeStage Next(MarcusAureliusCampPracticeStage stage,
        MarcusAureliusCampPracticeAction action) => (stage, action) switch
    {
        (MarcusAureliusCampPracticeStage.Invitation, MarcusAureliusCampPracticeAction.AskNeed) =>
            MarcusAureliusCampPracticeStage.ResponseAsked,
        (MarcusAureliusCampPracticeStage.Invitation, MarcusAureliusCampPracticeAction.StatePlan) =>
            MarcusAureliusCampPracticeStage.ResponseStated,
        (MarcusAureliusCampPracticeStage.Invitation, MarcusAureliusCampPracticeAction.Decline) =>
            MarcusAureliusCampPracticeStage.ResultDeclined,
        (MarcusAureliusCampPracticeStage.ResponseAsked, MarcusAureliusCampPracticeAction.WorkTogether) =>
            MarcusAureliusCampPracticeStage.ResultAskedWork,
        (MarcusAureliusCampPracticeStage.ResponseAsked, MarcusAureliusCampPracticeAction.OrganizeAndMark) =>
            MarcusAureliusCampPracticeStage.ResultAskedOrganize,
        (MarcusAureliusCampPracticeStage.ResponseStated, MarcusAureliusCampPracticeAction.WorkTogether) =>
            MarcusAureliusCampPracticeStage.ResultStatedWork,
        (MarcusAureliusCampPracticeStage.ResponseStated, MarcusAureliusCampPracticeAction.OrganizeAndMark) =>
            MarcusAureliusCampPracticeStage.ResultStatedOrganize,
        _ => stage,
    };

    public static bool TryPrepare(MarcusAureliusCampPracticeState state, MarcusAureliusCampPracticeAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != MarcusAureliusCampPracticeAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool IsRouteConsistent(MarcusAureliusCampPracticeState state, ZenoRouteState route,
        EpictetusGuidePracticeState epictetus) => state.IsUsable()
        && route.Stage == ZenoRouteStage.ZenoClosed && route.Outcome == state.ZenoOutcome
        && route.RunId == state.RunId && epictetus.RunId == state.RunId
        && epictetus.Closed && epictetus.InstanceId == state.EpictetusInstanceId;

    public static bool TryCommitPending(MarcusAureliusCampPracticeState state, ZenoRouteState route,
        EpictetusGuidePracticeState epictetus)
    {
        if (!IsRouteConsistent(state, route, epictetus) || state.Closed
            || state.PendingAction == MarcusAureliusCampPracticeAction.None) return false;
        MarcusAureliusCampPracticeAction action = state.PendingAction;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == MarcusAureliusCampPracticeAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = MarcusAureliusCampPracticeAction.None;
        return true;
    }

    public static string StageKey(MarcusAureliusCampPracticeStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(MarcusAureliusCampPracticeAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
