using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum PeirceInquiryStage { Invitation, ResultTest, ResultCommunity, ResultKept }
internal enum PeirceInquiryAction { None, TestByConsequences, FallibleCommunity, KeepHume, AskJames, Leave }
internal enum PeirceInquiryStandard { None, Consequences, Community, Humean }

/// <summary>
/// Peirce's second-act room on the fallible-inquiry line. The player either
/// tests belief by its consequences, submits it to a fallible community, or
/// keeps Hume and gives up the act's fixed slot.
/// </summary>
internal sealed class PeirceInquiryState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "DAVID_HUME__KNOWLEDGE_AND_DOUBT__CORE";
    public const string AdoptedNodeId = "CHARLES_SANDERS_PEIRCE__KNOWLEDGE_AND_DOUBT__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_070";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int ActIndex { get; set; } = 2;
    public string EdgeId { get; set; } = CompletionEdgeId;

    /// <summary>The node a route arrives from when it places this room outside its own act.</summary>
    public string RouteSourceNodeId { get; set; } = string.Empty;
    public PeirceInquiryStage Stage { get; set; }
    public PeirceInquiryAction PendingAction { get; set; }
    public List<PeirceInquiryAction> ConfirmedActions { get; set; } = [];
    public PeirceInquiryStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }
    public JamesPragmatismState? James { get; set; }
    public bool InspectingJames { get; set; }

    public static PeirceInquiryState Create(string instanceId) => new() { InstanceId = instanceId };

    public static PeirceInquiryState CreateAt(string instanceId, int actIndex, string edgeId) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= CompletionEdgeId;
        if (ActIndex is < 1 or > 3) ActIndex = 2;
        ConfirmedActions ??= [];
        // A closed nested segment can never be re-entered; clear the stale
        // inspection flag left by the old deadlock without dropping the segment.
        if (InspectingJames && James is { Closed: true }) InspectingJames = false;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || InspectingJames && James is null
            || James is not null && !James.IsUsable()
            || Closed && PendingAction != PeirceInquiryAction.None
            || PendingAction != PeirceInquiryAction.None
                && !PeirceInquiryFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (PeirceInquiryAction action in ConfirmedActions)
        {
            if (replay.Closed || !PeirceInquiryFlow.Options(replay.Stage).Contains(action)) return false;
            PeirceInquiryFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class PeirceInquiryFlow
{
    public static IReadOnlyList<PeirceInquiryAction> Options(PeirceInquiryStage stage) => stage switch
    {
        PeirceInquiryStage.Invitation =>
            [PeirceInquiryAction.TestByConsequences, PeirceInquiryAction.FallibleCommunity,
                PeirceInquiryAction.KeepHume],
        PeirceInquiryStage.ResultTest or PeirceInquiryStage.ResultCommunity
            or PeirceInquiryStage.ResultKept =>
            [PeirceInquiryAction.Leave, PeirceInquiryAction.AskJames],
        _ => [],
    };

    public static bool TryPrepare(PeirceInquiryState state, PeirceInquiryAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != PeirceInquiryAction.None
            || !Options(state.Stage).Contains(action)) return false;
        // A closed nested segment must never be re-entered.
        if (action == PeirceInquiryAction.AskJames && state.James is { Closed: true }) return false;
        state.PendingAction = action;
        return true;
    }

    /// <summary>
    /// Cancels a restored AskJames pending action whose nested segment is already
    /// closed. This is a no-op re-entry: it returns to the parent result page
    /// without recording a confirmed action.
    /// </summary>
    public static bool TryCancelClosedJamesReentry(PeirceInquiryState state)
    {
        if (state.PendingAction != PeirceInquiryAction.AskJames
            || state.James is not { Closed: true }) return false;
        state.PendingAction = PeirceInquiryAction.None;
        state.InspectingJames = false;
        return true;
    }

    public static void Apply(PeirceInquiryState state, PeirceInquiryAction action)
    {
        switch (action)
        {
            case PeirceInquiryAction.TestByConsequences:
                state.Standard = PeirceInquiryStandard.Consequences;
                state.EdgeCommitted = true;
                state.Stage = PeirceInquiryStage.ResultTest;
                break;
            case PeirceInquiryAction.FallibleCommunity:
                state.Standard = PeirceInquiryStandard.Community;
                state.EdgeCommitted = true;
                state.Stage = PeirceInquiryStage.ResultCommunity;
                break;
            case PeirceInquiryAction.KeepHume:
                state.Standard = PeirceInquiryStandard.Humean;
                state.FixedDeclined = true;
                state.Stage = PeirceInquiryStage.ResultKept;
                break;
            case PeirceInquiryAction.AskJames:
                state.James ??= JamesPragmatismState.Create($"{state.InstanceId}:JAMES");
                state.InspectingJames = true;
                break;
            case PeirceInquiryAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = PeirceInquiryAction.None;
    }

    public static bool TryCommitPending(PeirceInquiryState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == PeirceInquiryAction.None) return false;
        PeirceInquiryAction action = state.PendingAction;
        bool adopts = action is PeirceInquiryAction.TestByConsequences or PeirceInquiryAction.FallibleCommunity;
        bool declines = action == PeirceInquiryAction.KeepHume;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(PeirceInquiryState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        // A route may reach the room from a different question node; the edge it
        // uses is recorded when the room is created for that act.
        bool atSource = journey.CurrentNodeId == PeirceInquiryState.SourceNodeId
            || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == state.ActIndex && !adopted
                && !journey.DeclinedFixedActs.Contains(state.ActIndex) && atSource;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == Math.Min(3, state.ActIndex + 1)
                && journey.CurrentNodeId == PeirceInquiryState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == state.ActIndex
            && journey.DeclinedFixedActs.Contains(state.ActIndex) && atSource;
    }

    public static string DescriptionKey(PeirceInquiryState state) => state.Stage switch
    {
        PeirceInquiryStage.Invitation => "INVITATION",
        PeirceInquiryStage.ResultTest => "RESULT_TEST",
        PeirceInquiryStage.ResultCommunity => "RESULT_COMMUNITY",
        PeirceInquiryStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(PeirceInquiryStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(PeirceInquiryAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
