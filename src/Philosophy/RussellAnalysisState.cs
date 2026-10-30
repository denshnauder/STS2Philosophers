using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum RussellAnalysisStage { Invitation, ResultDescription, ResultAtomism, ResultKept }
internal enum RussellAnalysisAction { None, DefiniteDescription, LogicalAtomism, KeepFrege, Leave }
internal enum RussellAnalysisStandard { None, Description, Atomism, Fregean }

/// <summary>
/// Russell's second-act room on the formal-language line. The player either
/// analyses definite descriptions, builds a logical atomism, or keeps Frege and
/// gives up the act's fixed slot.
/// </summary>
internal sealed class RussellAnalysisState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "GOTTLOB_FREGE__LANGUAGE_AND_MEANING__CORE";
    public const string AdoptedNodeId = "BERTRAND_RUSSELL__LANGUAGE_AND_MEANING__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_078";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public RussellAnalysisStage Stage { get; set; }
    public RussellAnalysisAction PendingAction { get; set; }
    public List<RussellAnalysisAction> ConfirmedActions { get; set; } = [];
    public RussellAnalysisStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static RussellAnalysisState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != RussellAnalysisAction.None
            || PendingAction != RussellAnalysisAction.None
                && !RussellAnalysisFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (RussellAnalysisAction action in ConfirmedActions)
        {
            if (replay.Closed || !RussellAnalysisFlow.Options(replay.Stage).Contains(action)) return false;
            RussellAnalysisFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class RussellAnalysisFlow
{
    public static IReadOnlyList<RussellAnalysisAction> Options(RussellAnalysisStage stage) => stage switch
    {
        RussellAnalysisStage.Invitation =>
            [RussellAnalysisAction.DefiniteDescription, RussellAnalysisAction.LogicalAtomism,
                RussellAnalysisAction.KeepFrege],
        RussellAnalysisStage.ResultDescription or RussellAnalysisStage.ResultAtomism
            or RussellAnalysisStage.ResultKept =>
            [RussellAnalysisAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(RussellAnalysisState state, RussellAnalysisAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != RussellAnalysisAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(RussellAnalysisState state, RussellAnalysisAction action)
    {
        switch (action)
        {
            case RussellAnalysisAction.DefiniteDescription:
                state.Standard = RussellAnalysisStandard.Description;
                state.EdgeCommitted = true;
                state.Stage = RussellAnalysisStage.ResultDescription;
                break;
            case RussellAnalysisAction.LogicalAtomism:
                state.Standard = RussellAnalysisStandard.Atomism;
                state.EdgeCommitted = true;
                state.Stage = RussellAnalysisStage.ResultAtomism;
                break;
            case RussellAnalysisAction.KeepFrege:
                state.Standard = RussellAnalysisStandard.Fregean;
                state.FixedDeclined = true;
                state.Stage = RussellAnalysisStage.ResultKept;
                break;
            case RussellAnalysisAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = RussellAnalysisAction.None;
    }

    public static bool TryCommitPending(RussellAnalysisState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == RussellAnalysisAction.None) return false;
        RussellAnalysisAction action = state.PendingAction;
        bool adopts = action is RussellAnalysisAction.DefiniteDescription or RussellAnalysisAction.LogicalAtomism;
        bool declines = action == RussellAnalysisAction.KeepFrege;
        if (adopts && !journey.CompletedEdgeIds.Contains(RussellAnalysisState.CompletionEdgeId)
            && !graph.TryApply(journey, RussellAnalysisState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(RussellAnalysisState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(RussellAnalysisState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == RussellAnalysisState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == RussellAnalysisState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && journey.CurrentNodeId == RussellAnalysisState.SourceNodeId;
    }

    public static string DescriptionKey(RussellAnalysisState state) => state.Stage switch
    {
        RussellAnalysisStage.Invitation => "INVITATION",
        RussellAnalysisStage.ResultDescription => "RESULT_SEPARATION",
        RussellAnalysisStage.ResultAtomism => "RESULT_BALANCE",
        RussellAnalysisStage.ResultKept => "RESULT_PRIVILEGE",
        _ => "INVITATION",
    };

    public static string StageKey(RussellAnalysisStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(RussellAnalysisAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
