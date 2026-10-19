using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum GorgiasSpeechInquiryStage
{
    Invitation, Speech, StrongClaim, CautiousClaim, ProofLeap, BeliefOnly,
    ResultRecorded, ResultKept, ResultStopped, ResultDeclined,
}

internal enum GorgiasSpeechInquiryAction
{
    None, AcceptInvitation, DeclineInvitation, AssertContents, StateUncertainty,
    TakeAssentAsProof, TakeDelayAsProof, SeparateBeliefFromProof, RecordBoundary, KeepCategories,
    StopInquiry, Leave,
}

internal sealed class GorgiasSpeechInquiryState : IJsonOnDeserialized
{
    public const string RequiredNodeId = "ARISTOTLE__LANGUAGE_AND_MEANING__CORE";
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public GorgiasSpeechInquiryStage Stage { get; set; } = (GorgiasSpeechInquiryStage)(-1);
    public GorgiasSpeechInquiryAction PendingAction { get; set; } = (GorgiasSpeechInquiryAction)(-1);
    public List<GorgiasSpeechInquiryAction> ConfirmedActions { get; set; } = null!;
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static GorgiasSpeechInquiryState Create(string instanceId) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        Stage = GorgiasSpeechInquiryStage.Invitation,
        PendingAction = GorgiasSpeechInquiryAction.None,
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
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || !_closed.HasValue
            || ConfirmedActions is null || ConfirmedActions.Count > 5
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        GorgiasSpeechInquiryStage replay = GorgiasSpeechInquiryStage.Invitation;
        bool closed = false;
        foreach (var action in ConfirmedActions)
        {
            if (closed || !GorgiasSpeechInquiryFlow.Options(replay).Contains(action)) return false;
            closed = action == GorgiasSpeechInquiryAction.Leave;
            replay = GorgiasSpeechInquiryFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == GorgiasSpeechInquiryAction.None
                || !Closed && GorgiasSpeechInquiryFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class GorgiasSpeechInquiryFlow
{
    public static IReadOnlyList<GorgiasSpeechInquiryAction> Options(GorgiasSpeechInquiryStage stage) => stage switch
    {
        GorgiasSpeechInquiryStage.Invitation =>
            [GorgiasSpeechInquiryAction.AcceptInvitation, GorgiasSpeechInquiryAction.DeclineInvitation],
        GorgiasSpeechInquiryStage.Speech =>
            [GorgiasSpeechInquiryAction.AssertContents, GorgiasSpeechInquiryAction.StateUncertainty,
                GorgiasSpeechInquiryAction.StopInquiry],
        GorgiasSpeechInquiryStage.StrongClaim =>
            [GorgiasSpeechInquiryAction.TakeAssentAsProof, GorgiasSpeechInquiryAction.SeparateBeliefFromProof],
        GorgiasSpeechInquiryStage.CautiousClaim =>
            [GorgiasSpeechInquiryAction.TakeDelayAsProof, GorgiasSpeechInquiryAction.SeparateBeliefFromProof],
        GorgiasSpeechInquiryStage.ProofLeap or GorgiasSpeechInquiryStage.BeliefOnly =>
            [GorgiasSpeechInquiryAction.RecordBoundary, GorgiasSpeechInquiryAction.KeepCategories],
        GorgiasSpeechInquiryStage.ResultRecorded or GorgiasSpeechInquiryStage.ResultKept
            or GorgiasSpeechInquiryStage.ResultStopped or GorgiasSpeechInquiryStage.ResultDeclined =>
            [GorgiasSpeechInquiryAction.Leave],
        _ => [],
    };

    public static GorgiasSpeechInquiryStage Next(
        GorgiasSpeechInquiryStage stage, GorgiasSpeechInquiryAction action) => action switch
    {
        GorgiasSpeechInquiryAction.AcceptInvitation => GorgiasSpeechInquiryStage.Speech,
        GorgiasSpeechInquiryAction.DeclineInvitation => GorgiasSpeechInquiryStage.ResultDeclined,
        GorgiasSpeechInquiryAction.AssertContents => GorgiasSpeechInquiryStage.StrongClaim,
        GorgiasSpeechInquiryAction.StateUncertainty => GorgiasSpeechInquiryStage.CautiousClaim,
        GorgiasSpeechInquiryAction.TakeAssentAsProof => GorgiasSpeechInquiryStage.ProofLeap,
        GorgiasSpeechInquiryAction.TakeDelayAsProof => GorgiasSpeechInquiryStage.ProofLeap,
        GorgiasSpeechInquiryAction.SeparateBeliefFromProof => GorgiasSpeechInquiryStage.BeliefOnly,
        GorgiasSpeechInquiryAction.RecordBoundary => GorgiasSpeechInquiryStage.ResultRecorded,
        GorgiasSpeechInquiryAction.KeepCategories => GorgiasSpeechInquiryStage.ResultKept,
        GorgiasSpeechInquiryAction.StopInquiry => GorgiasSpeechInquiryStage.ResultStopped,
        _ => stage,
    };

    public static bool TryPrepare(GorgiasSpeechInquiryState state, GorgiasSpeechInquiryAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != GorgiasSpeechInquiryAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool IsJourneyConsistent(GorgiasSpeechInquiryState state, WesternJourneyState journey) =>
        state.IsUsable() && journey.LastFixedAct == 1
        && journey.CurrentNodeId == GorgiasSpeechInquiryState.RequiredNodeId
        && journey.SeenThinkerIds.Contains("ARISTOTLE")
        && journey.ResolvedQuestionActs.Contains(1) && !journey.RetainedEnding;

    public static bool TryCommitPending(GorgiasSpeechInquiryState state, WesternJourneyState journey)
    {
        if (!IsJourneyConsistent(state, journey) || state.Closed
            || state.PendingAction == GorgiasSpeechInquiryAction.None) return false;
        GorgiasSpeechInquiryAction action = state.PendingAction;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == GorgiasSpeechInquiryAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = GorgiasSpeechInquiryAction.None;
        return true;
    }

    public static string StageKey(GorgiasSpeechInquiryStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(GorgiasSpeechInquiryAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
