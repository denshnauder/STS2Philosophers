using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum SenecaDailyReviewStage
{
    Invitation, Focus, ReviewKeep, ReviewNarrow, ReviewWithdraw, ReviewNoReassent,
    ReviewNoAssent, ReviewAidRepeated, ReviewAidOnce, ReviewAidForegone,
    ReviewAidUnknown, ResultJudgmentKept, ResultJudgmentRevised, ResultAidKept,
    ResultAidRevised, ResultDeclined,
}

internal enum SenecaDailyReviewAction
{
    None, Begin, Decline, ExamineJudgment, ExamineAid, BackToFocus,
    KeepCheck, ReviseCheck, Leave,
}

internal sealed class SenecaDailyReviewState : IJsonOnDeserialized
{
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public ZenoAssentOutcome Outcome { get; set; } = (ZenoAssentOutcome)(-1);
    public DiogenesPracticeSummary? Practice { get; set; }
    public SenecaDailyReviewStage Stage { get; set; } = (SenecaDailyReviewStage)(-1);
    public SenecaDailyReviewAction PendingAction { get; set; } = (SenecaDailyReviewAction)(-1);
    public List<SenecaDailyReviewAction> ConfirmedActions { get; set; } = null!;
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static SenecaDailyReviewState Create(string instanceId, ZenoRouteState route,
        DiogenesStaySwitchEntryRecord entry) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        RunId = route.RunId,
        Outcome = route.Outcome!.Value,
        Practice = entry.Practice,
        Stage = SenecaDailyReviewStage.Invitation,
        PendingAction = SenecaDailyReviewAction.None,
        ConfirmedActions = [],
        Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        RunId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(RunId)
            || !_closed.HasValue || !Enum.IsDefined(Outcome) || !Enum.IsDefined(Stage)
            || !Enum.IsDefined(PendingAction) || Practice is { } practice && !practice.IsValid()
            || ConfirmedActions is null || ConfirmedActions.Count > 6) return false;
        SenecaDailyReviewStage replay = SenecaDailyReviewStage.Invitation;
        bool closed = false;
        bool canBack = true;
        foreach (SenecaDailyReviewAction action in ConfirmedActions)
        {
            if (closed || !SenecaDailyReviewFlow.Options(replay, canBack).Contains(action)) return false;
            closed = action == SenecaDailyReviewAction.Leave;
            if (action == SenecaDailyReviewAction.BackToFocus) canBack = false;
            replay = SenecaDailyReviewFlow.Next(this, replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == SenecaDailyReviewAction.None
                || !Closed && SenecaDailyReviewFlow.Options(this).Contains(PendingAction));
    }
}

internal static class SenecaDailyReviewFlow
{
    public static IReadOnlyList<SenecaDailyReviewAction> Options(SenecaDailyReviewState state) =>
        Options(state.Stage, !state.ConfirmedActions.Contains(SenecaDailyReviewAction.BackToFocus));

    public static IReadOnlyList<SenecaDailyReviewAction> Options(SenecaDailyReviewStage stage) =>
        Options(stage, true);

    public static IReadOnlyList<SenecaDailyReviewAction> Options(
        SenecaDailyReviewStage stage, bool canBack) => stage switch
    {
        SenecaDailyReviewStage.Invitation =>
            [SenecaDailyReviewAction.Begin, SenecaDailyReviewAction.Decline],
        SenecaDailyReviewStage.Focus =>
            [SenecaDailyReviewAction.ExamineJudgment, SenecaDailyReviewAction.ExamineAid],
        SenecaDailyReviewStage.ReviewKeep or SenecaDailyReviewStage.ReviewNarrow
            or SenecaDailyReviewStage.ReviewWithdraw or SenecaDailyReviewStage.ReviewNoReassent
            or SenecaDailyReviewStage.ReviewNoAssent or SenecaDailyReviewStage.ReviewAidRepeated
            or SenecaDailyReviewStage.ReviewAidOnce or SenecaDailyReviewStage.ReviewAidForegone
            or SenecaDailyReviewStage.ReviewAidUnknown =>
            canBack
                ? [SenecaDailyReviewAction.BackToFocus, SenecaDailyReviewAction.KeepCheck,
                    SenecaDailyReviewAction.ReviseCheck]
                : [SenecaDailyReviewAction.KeepCheck, SenecaDailyReviewAction.ReviseCheck],
        SenecaDailyReviewStage.ResultJudgmentKept or SenecaDailyReviewStage.ResultJudgmentRevised
            or SenecaDailyReviewStage.ResultAidKept or SenecaDailyReviewStage.ResultAidRevised
            or SenecaDailyReviewStage.ResultDeclined => [SenecaDailyReviewAction.Leave],
        _ => [],
    };

    public static SenecaDailyReviewStage Next(SenecaDailyReviewState state,
        SenecaDailyReviewStage stage, SenecaDailyReviewAction action) => action switch
    {
        SenecaDailyReviewAction.Begin => SenecaDailyReviewStage.Focus,
        SenecaDailyReviewAction.Decline => SenecaDailyReviewStage.ResultDeclined,
        SenecaDailyReviewAction.ExamineJudgment => state.Outcome switch
        {
            ZenoAssentOutcome.Keep => SenecaDailyReviewStage.ReviewKeep,
            ZenoAssentOutcome.Narrow => SenecaDailyReviewStage.ReviewNarrow,
            ZenoAssentOutcome.Withdraw => SenecaDailyReviewStage.ReviewWithdraw,
            ZenoAssentOutcome.NoReassent => SenecaDailyReviewStage.ReviewNoReassent,
            _ => SenecaDailyReviewStage.ReviewNoAssent,
        },
        SenecaDailyReviewAction.ExamineAid => state.Practice switch
        {
            { Used: >= 2 } => SenecaDailyReviewStage.ReviewAidRepeated,
            { Used: 1 } => SenecaDailyReviewStage.ReviewAidOnce,
            { Eligible: > 0 } => SenecaDailyReviewStage.ReviewAidForegone,
            _ => SenecaDailyReviewStage.ReviewAidUnknown,
        },
        SenecaDailyReviewAction.BackToFocus => SenecaDailyReviewStage.Focus,
        SenecaDailyReviewAction.KeepCheck => IsAid(stage)
            ? SenecaDailyReviewStage.ResultAidKept : SenecaDailyReviewStage.ResultJudgmentKept,
        SenecaDailyReviewAction.ReviseCheck => IsAid(stage)
            ? SenecaDailyReviewStage.ResultAidRevised : SenecaDailyReviewStage.ResultJudgmentRevised,
        _ => stage,
    };

    private static bool IsAid(SenecaDailyReviewStage stage) => stage is
        SenecaDailyReviewStage.ReviewAidRepeated or SenecaDailyReviewStage.ReviewAidOnce
        or SenecaDailyReviewStage.ReviewAidForegone or SenecaDailyReviewStage.ReviewAidUnknown;

    public static bool TryPrepare(SenecaDailyReviewState state, SenecaDailyReviewAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != SenecaDailyReviewAction.None
            || !Options(state).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool IsRouteConsistent(SenecaDailyReviewState state, ZenoRouteState route,
        DiogenesStaySwitchEntryRecord entry) => state.IsUsable()
        && route.Stage == ZenoRouteStage.ZenoClosed && route.Outcome == state.Outcome
        && route.RunId == state.RunId && entry.RunId == state.RunId && entry.Closed
        && Equals(entry.Practice, state.Practice);

    public static bool TryCommitPending(SenecaDailyReviewState state, ZenoRouteState route,
        DiogenesStaySwitchEntryRecord entry)
    {
        if (!IsRouteConsistent(state, route, entry) || state.Closed
            || state.PendingAction == SenecaDailyReviewAction.None) return false;
        SenecaDailyReviewAction action = state.PendingAction;
        state.Stage = Next(state, state.Stage, action);
        state.Closed = action == SenecaDailyReviewAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = SenecaDailyReviewAction.None;
        return true;
    }

    public static string StageKey(SenecaDailyReviewStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(SenecaDailyReviewAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
