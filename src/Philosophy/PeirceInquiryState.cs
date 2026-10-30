using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum PeirceInquiryStage { Invitation, ResultTest, ResultCommunity, ResultKept }
internal enum PeirceInquiryAction { None, TestByConsequences, FallibleCommunity, KeepHume, Leave }
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
    public PeirceInquiryStage Stage { get; set; }
    public PeirceInquiryAction PendingAction { get; set; }
    public List<PeirceInquiryAction> ConfirmedActions { get; set; } = [];
    public PeirceInquiryStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static PeirceInquiryState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
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
            or PeirceInquiryStage.ResultKept => [PeirceInquiryAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(PeirceInquiryState state, PeirceInquiryAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != PeirceInquiryAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
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
        if (adopts && !journey.CompletedEdgeIds.Contains(PeirceInquiryState.CompletionEdgeId)
            && !graph.TryApply(journey, PeirceInquiryState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(PeirceInquiryState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(PeirceInquiryState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == PeirceInquiryState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == PeirceInquiryState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && journey.CurrentNodeId == PeirceInquiryState.SourceNodeId;
    }

    public static string DescriptionKey(PeirceInquiryState state) => state.Stage switch
    {
        PeirceInquiryStage.Invitation => "INVITATION",
        PeirceInquiryStage.ResultTest => "RESULT_SEPARATION",
        PeirceInquiryStage.ResultCommunity => "RESULT_BALANCE",
        PeirceInquiryStage.ResultKept => "RESULT_PRIVILEGE",
        _ => "INVITATION",
    };

    public static string StageKey(PeirceInquiryStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(PeirceInquiryAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
