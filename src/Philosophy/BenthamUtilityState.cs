using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum BenthamUtilityStage { Invitation, ResultUtility, ResultBoundary, ResultKept }
internal enum BenthamUtilityAction { None, AggregateUtility, KeepIndividualBoundary, KeepSocrates, Leave }
internal enum BenthamUtilityStandard { None, Utility, Boundary, Socratic }

/// <summary>
/// Kierkegaard's first-act room on the self-and-other line. The player either
/// takes the leap of faith, keeps the ethical universal, or stays with Socrates
/// and gives up the act's fixed slot.
/// </summary>
internal sealed class BenthamUtilityState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "SOCRATES__VIRTUE_AND_HAPPINESS__CORE";
    public const string AdoptedNodeId = "BENTHAM__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_065";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public BenthamUtilityStage Stage { get; set; }
    public BenthamUtilityAction PendingAction { get; set; }
    public List<BenthamUtilityAction> ConfirmedActions { get; set; } = [];
    public BenthamUtilityStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static BenthamUtilityState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != BenthamUtilityAction.None
            || PendingAction != BenthamUtilityAction.None
                && !BenthamUtilityFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (BenthamUtilityAction action in ConfirmedActions)
        {
            if (replay.Closed || !BenthamUtilityFlow.Options(replay.Stage).Contains(action)) return false;
            BenthamUtilityFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class BenthamUtilityFlow
{
    public static IReadOnlyList<BenthamUtilityAction> Options(BenthamUtilityStage stage) => stage switch
    {
        BenthamUtilityStage.Invitation =>
            [BenthamUtilityAction.AggregateUtility, BenthamUtilityAction.KeepIndividualBoundary,
                BenthamUtilityAction.KeepSocrates],
        BenthamUtilityStage.ResultUtility or BenthamUtilityStage.ResultBoundary
            or BenthamUtilityStage.ResultKept => [BenthamUtilityAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(BenthamUtilityState state, BenthamUtilityAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != BenthamUtilityAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(BenthamUtilityState state, BenthamUtilityAction action)
    {
        switch (action)
        {
            case BenthamUtilityAction.AggregateUtility:
                state.Standard = BenthamUtilityStandard.Utility;
                state.EdgeCommitted = true;
                state.Stage = BenthamUtilityStage.ResultUtility;
                break;
            case BenthamUtilityAction.KeepIndividualBoundary:
                state.Standard = BenthamUtilityStandard.Boundary;
                state.EdgeCommitted = true;
                state.Stage = BenthamUtilityStage.ResultBoundary;
                break;
            case BenthamUtilityAction.KeepSocrates:
                state.Standard = BenthamUtilityStandard.Socratic;
                state.FixedDeclined = true;
                state.Stage = BenthamUtilityStage.ResultKept;
                break;
            case BenthamUtilityAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = BenthamUtilityAction.None;
    }

    public static bool TryCommitPending(BenthamUtilityState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == BenthamUtilityAction.None) return false;
        BenthamUtilityAction action = state.PendingAction;
        bool adopts = action is BenthamUtilityAction.AggregateUtility or BenthamUtilityAction.KeepIndividualBoundary;
        bool declines = action == BenthamUtilityAction.KeepSocrates;
        if (adopts && !journey.CompletedEdgeIds.Contains(BenthamUtilityState.CompletionEdgeId)
            && !graph.TryApply(journey, BenthamUtilityState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(2)
            && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(BenthamUtilityState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(BenthamUtilityState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 1 && !adopted && !journey.DeclinedFixedActs.Contains(2)
                && journey.CurrentNodeId == BenthamUtilityState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 2
                && journey.CurrentNodeId == BenthamUtilityState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 2 && journey.DeclinedFixedActs.Contains(2)
            && journey.CurrentNodeId == BenthamUtilityState.SourceNodeId;
    }

    public static string DescriptionKey(BenthamUtilityState state) => state.Stage switch
    {
        BenthamUtilityStage.Invitation => "INVITATION",
        BenthamUtilityStage.ResultUtility => "RESULT_SEPARATION",
        BenthamUtilityStage.ResultBoundary => "RESULT_BALANCE",
        BenthamUtilityStage.ResultKept => "RESULT_PRIVILEGE",
        _ => "INVITATION",
    };

    public static string StageKey(BenthamUtilityStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(BenthamUtilityAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
