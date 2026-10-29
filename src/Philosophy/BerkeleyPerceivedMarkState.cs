using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum BerkeleyMarkStage { Invitation, Observed, ResultReject, ResultWitness, ResultScope }
internal enum BerkeleyMarkAction { None, CheckTouch, CheckSound, CheckReceipt, RejectMaterialSubstance, RequestSecondObserver, KeepPerceptionScope, Leave }
internal enum BerkeleyChannel { None, Touch, Sound, Receipt }
internal enum BerkeleyConclusion { None, RejectMaterialSubstance, SecondObserver, PerceptionScope }

/// <summary>
/// Saved state for Berkeley's in-batch observation scene. It records only what the
/// player actually observed (one channel), the hidden second object that broke the
/// first explanation, and the narrow conclusion. It deliberately does not own a map
/// edge: the hosting opportunity is decided by the route, not by this state.
/// </summary>
internal sealed class BerkeleyPerceivedMarkState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string HiddenObjectId = "SEALED_SECOND_CRATE";
    public const string SourceNodeId = "DESCARTES__KNOWLEDGE_AND_DOUBT__CORE";
    public const string EdgeId = "WESTERN_EDGE_014";
    public const int DeclineAct = 2;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public BerkeleyMarkStage Stage { get; set; }
    public BerkeleyMarkAction PendingAction { get; set; }
    public List<BerkeleyMarkAction> ConfirmedActions { get; set; } = [];
    public BerkeleyChannel Channel { get; set; }
    public string? HiddenObject { get; set; }
    public BerkeleyConclusion Conclusion { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static BerkeleyPerceivedMarkState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
        HiddenObject ??= null;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Channel) || !Enum.IsDefined(Conclusion)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != BerkeleyMarkAction.None
            || PendingAction != BerkeleyMarkAction.None
                && !BerkeleyPerceivedMarkFlow.Options(Stage).Contains(PendingAction)
            || HiddenObject is not null && HiddenObject != HiddenObjectId) return false;

        var replay = Create(InstanceId);
        foreach (BerkeleyMarkAction action in ConfirmedActions)
        {
            if (replay.Closed || !BerkeleyPerceivedMarkFlow.Options(replay.Stage).Contains(action)) return false;
            BerkeleyPerceivedMarkFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Channel == replay.Channel && HiddenObject == replay.HiddenObject
            && Conclusion == replay.Conclusion && QuestionDeclined == replay.QuestionDeclined
            && Closed == replay.Closed;
    }
}

internal static class BerkeleyPerceivedMarkFlow
{
    public static IReadOnlyList<BerkeleyMarkAction> Options(BerkeleyMarkStage stage) => stage switch
    {
        BerkeleyMarkStage.Invitation =>
            [BerkeleyMarkAction.CheckTouch, BerkeleyMarkAction.CheckSound, BerkeleyMarkAction.CheckReceipt],
        BerkeleyMarkStage.Observed =>
            [BerkeleyMarkAction.RejectMaterialSubstance, BerkeleyMarkAction.RequestSecondObserver,
                BerkeleyMarkAction.KeepPerceptionScope],
        BerkeleyMarkStage.ResultReject or BerkeleyMarkStage.ResultWitness or BerkeleyMarkStage.ResultScope =>
            [BerkeleyMarkAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(BerkeleyPerceivedMarkState state, BerkeleyMarkAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != BerkeleyMarkAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(BerkeleyPerceivedMarkState state, BerkeleyMarkAction action)
    {
        switch (action)
        {
            case BerkeleyMarkAction.CheckTouch:
                state.Channel = BerkeleyChannel.Touch;
                state.HiddenObject = BerkeleyPerceivedMarkState.HiddenObjectId;
                state.Stage = BerkeleyMarkStage.Observed;
                break;
            case BerkeleyMarkAction.CheckSound:
                state.Channel = BerkeleyChannel.Sound;
                state.HiddenObject = BerkeleyPerceivedMarkState.HiddenObjectId;
                state.Stage = BerkeleyMarkStage.Observed;
                break;
            case BerkeleyMarkAction.CheckReceipt:
                state.Channel = BerkeleyChannel.Receipt;
                state.HiddenObject = BerkeleyPerceivedMarkState.HiddenObjectId;
                state.Stage = BerkeleyMarkStage.Observed;
                break;
            case BerkeleyMarkAction.RejectMaterialSubstance:
                state.Conclusion = BerkeleyConclusion.RejectMaterialSubstance;
                state.Stage = BerkeleyMarkStage.ResultReject;
                state.QuestionDeclined = true;
                break;
            case BerkeleyMarkAction.RequestSecondObserver:
                state.Conclusion = BerkeleyConclusion.SecondObserver;
                state.Stage = BerkeleyMarkStage.ResultWitness;
                state.QuestionDeclined = true;
                break;
            case BerkeleyMarkAction.KeepPerceptionScope:
                state.Conclusion = BerkeleyConclusion.PerceptionScope;
                state.Stage = BerkeleyMarkStage.ResultScope;
                state.QuestionDeclined = true;
                break;
            case BerkeleyMarkAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = BerkeleyMarkAction.None;
    }

    public static bool TryCommitPending(BerkeleyPerceivedMarkState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == BerkeleyMarkAction.None) return false;
        BerkeleyMarkAction action = state.PendingAction;
        bool concludes = action is BerkeleyMarkAction.RejectMaterialSubstance
            or BerkeleyMarkAction.RequestSecondObserver or BerkeleyMarkAction.KeepPerceptionScope;
        if (concludes && !journey.ResolvedQuestionActs.Contains(BerkeleyPerceivedMarkState.DeclineAct)
            && !graph.TryDecline(journey, "Question", BerkeleyPerceivedMarkState.DeclineAct)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(BerkeleyPerceivedMarkState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || journey.CurrentNodeId != BerkeleyPerceivedMarkState.SourceNodeId) return false;
        bool declined = journey.ResolvedQuestionActs.Contains(BerkeleyPerceivedMarkState.DeclineAct);
        return state.QuestionDeclined == declined;
    }

    public static string DescriptionKey(BerkeleyPerceivedMarkState state) => state.Stage switch
    {
        BerkeleyMarkStage.Invitation => "INVITATION",
        BerkeleyMarkStage.Observed => "OBSERVED",
        BerkeleyMarkStage.ResultReject => "RESULT_REJECT",
        BerkeleyMarkStage.ResultWitness => "RESULT_WITNESS",
        BerkeleyMarkStage.ResultScope => "RESULT_SCOPE",
        _ => "INVITATION",
    };
}
