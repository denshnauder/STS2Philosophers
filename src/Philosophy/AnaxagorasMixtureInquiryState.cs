using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum AnaxagorasMixtureInquiryStage
{
    Invitation, Mixture, Predominance, PureDemand, MindStart, MindEverywhere,
    ResultRecorded, ResultKept, ResultStopped, ResultDeclined,
}

internal enum AnaxagorasMixtureInquiryAction
{
    None, AcceptInvitation, DeclineInvitation, FollowPredominance, DemandPurePart,
    LimitMindToStart, RequireMindEverywhere, RecordLimitedQuestion, KeepParmenides,
    StopInquiry, Leave,
}

internal sealed class AnaxagorasMixtureInquiryState : IJsonOnDeserialized
{
    public const string RequiredNodeId = "PARMENIDES__BEING_AND_CHANGE__CORE";
    public const string PrerequisiteEdgeId = "WESTERN_EDGE_001";
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public AnaxagorasMixtureInquiryStage Stage { get; set; } = (AnaxagorasMixtureInquiryStage)(-1);
    public AnaxagorasMixtureInquiryAction PendingAction { get; set; } = (AnaxagorasMixtureInquiryAction)(-1);
    public List<AnaxagorasMixtureInquiryAction> ConfirmedActions { get; set; } = null!;
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static AnaxagorasMixtureInquiryState Create(string instanceId) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        Stage = AnaxagorasMixtureInquiryStage.Invitation,
        PendingAction = AnaxagorasMixtureInquiryAction.None,
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
        AnaxagorasMixtureInquiryStage replay = AnaxagorasMixtureInquiryStage.Invitation;
        bool closed = false;
        foreach (var action in ConfirmedActions)
        {
            if (closed || !AnaxagorasMixtureInquiryFlow.Options(replay).Contains(action)) return false;
            closed = action == AnaxagorasMixtureInquiryAction.Leave;
            replay = AnaxagorasMixtureInquiryFlow.Next(replay, action);
        }
        return replay == Stage && closed == Closed
            && (PendingAction == AnaxagorasMixtureInquiryAction.None
                || !Closed && AnaxagorasMixtureInquiryFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class AnaxagorasMixtureInquiryFlow
{
    public static IReadOnlyList<AnaxagorasMixtureInquiryAction> Options(AnaxagorasMixtureInquiryStage stage) => stage switch
    {
        AnaxagorasMixtureInquiryStage.Invitation =>
            [AnaxagorasMixtureInquiryAction.AcceptInvitation, AnaxagorasMixtureInquiryAction.DeclineInvitation],
        AnaxagorasMixtureInquiryStage.Mixture =>
            [AnaxagorasMixtureInquiryAction.FollowPredominance, AnaxagorasMixtureInquiryAction.DemandPurePart,
                AnaxagorasMixtureInquiryAction.StopInquiry],
        AnaxagorasMixtureInquiryStage.Predominance or AnaxagorasMixtureInquiryStage.PureDemand =>
            [AnaxagorasMixtureInquiryAction.LimitMindToStart, AnaxagorasMixtureInquiryAction.RequireMindEverywhere],
        AnaxagorasMixtureInquiryStage.MindStart or AnaxagorasMixtureInquiryStage.MindEverywhere =>
            [AnaxagorasMixtureInquiryAction.RecordLimitedQuestion, AnaxagorasMixtureInquiryAction.KeepParmenides],
        AnaxagorasMixtureInquiryStage.ResultRecorded or AnaxagorasMixtureInquiryStage.ResultKept
            or AnaxagorasMixtureInquiryStage.ResultStopped or AnaxagorasMixtureInquiryStage.ResultDeclined =>
            [AnaxagorasMixtureInquiryAction.Leave],
        _ => [],
    };

    public static AnaxagorasMixtureInquiryStage Next(
        AnaxagorasMixtureInquiryStage stage, AnaxagorasMixtureInquiryAction action) => action switch
    {
        AnaxagorasMixtureInquiryAction.AcceptInvitation => AnaxagorasMixtureInquiryStage.Mixture,
        AnaxagorasMixtureInquiryAction.DeclineInvitation => AnaxagorasMixtureInquiryStage.ResultDeclined,
        AnaxagorasMixtureInquiryAction.FollowPredominance => AnaxagorasMixtureInquiryStage.Predominance,
        AnaxagorasMixtureInquiryAction.DemandPurePart => AnaxagorasMixtureInquiryStage.PureDemand,
        AnaxagorasMixtureInquiryAction.LimitMindToStart => AnaxagorasMixtureInquiryStage.MindStart,
        AnaxagorasMixtureInquiryAction.RequireMindEverywhere => AnaxagorasMixtureInquiryStage.MindEverywhere,
        AnaxagorasMixtureInquiryAction.RecordLimitedQuestion => AnaxagorasMixtureInquiryStage.ResultRecorded,
        AnaxagorasMixtureInquiryAction.KeepParmenides => AnaxagorasMixtureInquiryStage.ResultKept,
        AnaxagorasMixtureInquiryAction.StopInquiry => AnaxagorasMixtureInquiryStage.ResultStopped,
        _ => stage,
    };

    public static bool TryPrepare(AnaxagorasMixtureInquiryState state, AnaxagorasMixtureInquiryAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != AnaxagorasMixtureInquiryAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool IsJourneyConsistent(AnaxagorasMixtureInquiryState state, WesternJourneyState journey)
    {
        return state.IsUsable() && journey.LastFixedAct == 2
            && journey.CurrentNodeId == AnaxagorasMixtureInquiryState.RequiredNodeId
            && journey.CompletedEdgeIds.Contains(AnaxagorasMixtureInquiryState.PrerequisiteEdgeId)
            && journey.SeenThinkerIds.Contains("PARMENIDES")
            && journey.DeclinedFixedActs.Contains(2)
            && journey.ResolvedQuestionActs.Contains(2)
            && !journey.CompletedEdgeIds.Contains(DemocritusArrangementState.EdgeId)
            && !journey.RetainedEnding;
    }

    public static bool TryCommitPending(AnaxagorasMixtureInquiryState state, WesternJourneyState journey)
    {
        if (!IsJourneyConsistent(state, journey) || state.Closed
            || state.PendingAction == AnaxagorasMixtureInquiryAction.None) return false;
        AnaxagorasMixtureInquiryAction action = state.PendingAction;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == AnaxagorasMixtureInquiryAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = AnaxagorasMixtureInquiryAction.None;
        return true;
    }

    public static string StageKey(AnaxagorasMixtureInquiryStage stage) => ToUpperSnake(stage.ToString());
    public static string ActionKey(AnaxagorasMixtureInquiryAction action) => ToUpperSnake(action.ToString());
    private static string ToUpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
