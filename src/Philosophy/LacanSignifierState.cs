using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum LacanSignifierStage { Request, Interrupted, ResultClarified, ResultClaimed, ResultUnresolved }
internal enum LacanSignifierAction
{
    None, BindKeeper, BindCourier, BindArchivist, Clarify, ClaimNow, RefuseObject, Leave,
}
internal enum LacanSignifierStandard { None, Clarified, Claimed, Unresolved }
internal enum LacanSpeaker { None, Keeper, Courier, Archivist }

/// <summary>
/// Lacan's second-act room on the self-and-other line. The player first binds the
/// word "yours" to one speaker and its actual scene; a new utterance then breaks the
/// chain, and the player either tracks the shift and clarifies the request (losing
/// the claim priority), claims the object at once while one request goes unanswered,
/// or refuses to name an unverifiable missing object and keeps the gap open.
/// </summary>
internal sealed class LacanSignifierState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "SIGMUND_FREUD__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "JACQUES_LACAN__SELF_AND_OTHER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_087";
    public const string CompletionEdgeId = "WESTERN_EDGE_088";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public LacanSignifierStage Stage { get; set; }
    public LacanSignifierAction PendingAction { get; set; }
    public List<LacanSignifierAction> ConfirmedActions { get; set; } = [];
    public LacanSignifierStandard Standard { get; set; }
    public LacanSpeaker Speaker { get; set; }
    public List<LacanSpeaker> UnansweredRequests { get; set; } = [];
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 2;
    public string EdgeId { get; set; } = CompletionEdgeId;

    public static LacanSignifierState Create(string instanceId) => CreateAt(instanceId, 2, CompletionEdgeId);

    public static LacanSignifierState CreateAt(string instanceId, int actIndex, string edgeId) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
        UnansweredRequests ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || !Enum.IsDefined(Speaker)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || UnansweredRequests is null || UnansweredRequests.Count > 3
            || UnansweredRequests.Any(speaker => !Enum.IsDefined(speaker) || speaker == LacanSpeaker.None)
            || Closed && PendingAction != LacanSignifierAction.None
            || PendingAction != LacanSignifierAction.None
                && !LacanSignifierFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (LacanSignifierAction action in ConfirmedActions)
        {
            if (replay.Closed || !LacanSignifierFlow.Options(replay.Stage).Contains(action)) return false;
            LacanSignifierFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && Speaker == replay.Speaker
            && UnansweredRequests.SequenceEqual(replay.UnansweredRequests)
            && EdgeCommitted == replay.EdgeCommitted && FixedDeclined == replay.FixedDeclined
            && Closed == replay.Closed;
    }
}

internal static class LacanSignifierFlow
{
    public static IReadOnlyList<LacanSignifierAction> Options(LacanSignifierStage stage) => stage switch
    {
        LacanSignifierStage.Request =>
            [LacanSignifierAction.BindKeeper, LacanSignifierAction.BindCourier,
                LacanSignifierAction.BindArchivist],
        LacanSignifierStage.Interrupted =>
            [LacanSignifierAction.Clarify, LacanSignifierAction.ClaimNow,
                LacanSignifierAction.RefuseObject],
        LacanSignifierStage.ResultClarified or LacanSignifierStage.ResultClaimed
            or LacanSignifierStage.ResultUnresolved => [LacanSignifierAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(LacanSignifierState state, LacanSignifierAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != LacanSignifierAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(LacanSignifierState state, LacanSignifierAction action)
    {
        switch (action)
        {
            case LacanSignifierAction.BindKeeper:
                state.Speaker = LacanSpeaker.Keeper;
                state.Stage = LacanSignifierStage.Interrupted;
                break;
            case LacanSignifierAction.BindCourier:
                state.Speaker = LacanSpeaker.Courier;
                state.Stage = LacanSignifierStage.Interrupted;
                break;
            case LacanSignifierAction.BindArchivist:
                state.Speaker = LacanSpeaker.Archivist;
                state.Stage = LacanSignifierStage.Interrupted;
                break;
            case LacanSignifierAction.Clarify:
                state.Standard = LacanSignifierStandard.Clarified;
                state.EdgeCommitted = true;
                state.Stage = LacanSignifierStage.ResultClarified;
                break;
            case LacanSignifierAction.ClaimNow:
                state.Standard = LacanSignifierStandard.Claimed;
                state.EdgeCommitted = true;
                if (state.Speaker != LacanSpeaker.None) state.UnansweredRequests.Add(state.Speaker);
                state.Stage = LacanSignifierStage.ResultClaimed;
                break;
            case LacanSignifierAction.RefuseObject:
                state.Standard = LacanSignifierStandard.Unresolved;
                state.FixedDeclined = true;
                state.Stage = LacanSignifierStage.ResultUnresolved;
                break;
            case LacanSignifierAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = LacanSignifierAction.None;
    }

    public static bool TryCommitPending(LacanSignifierState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == LacanSignifierAction.None) return false;
        LacanSignifierAction action = state.PendingAction;
        bool adopts = action is LacanSignifierAction.Clarify or LacanSignifierAction.ClaimNow;
        bool declines = action == LacanSignifierAction.RefuseObject;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(LacanSignifierState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        int declined = state.ActIndex;
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == state.ActIndex && !adopted
                && !journey.DeclinedFixedActs.Contains(declined)
                && journey.CurrentNodeId == LacanSignifierState.SourceNodeId
                && journey.CompletedEdgeIds.Contains(LacanSignifierState.RequiredEdgeId);
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == Math.Min(3, state.ActIndex + 1)
                && journey.CurrentNodeId == LacanSignifierState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == state.ActIndex
            && journey.DeclinedFixedActs.Contains(declined)
            && journey.CurrentNodeId == LacanSignifierState.SourceNodeId;
    }

    public static string DescriptionKey(LacanSignifierState state) => UpperSnake(state.Stage.ToString());
    public static string StageKey(LacanSignifierStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(LacanSignifierAction action) => UpperSnake(action.ToString());
    public static string SpeakerKey(LacanSpeaker speaker) => UpperSnake(speaker.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
