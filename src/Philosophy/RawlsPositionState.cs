using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum RawlsPositionStage { Invitation, RuleFrozen, ResultExecuted, ResultAmended, ResultWithdrawn }
internal enum RawlsPositionAction
{
    None, DraftMinimum, DraftTotalDiscount, DraftLottery, Execute, AmendUniversally, Withdraw, Leave,
}
internal enum RawlsRule { None, Minimum, TotalDiscount, Lottery }
internal enum RawlsStanding { Frail, Merchant, Wealthy }

// Saved state for Rawls's scene. The rule is published before the player learns
// which standing they hold, and the standing is derived from the instance id the
// run fixed when the room opened: a reload can never reroll it. Nothing here
// judges whether the run's luck proves or disproves a theory of justice.
internal sealed class RawlsPositionState : IJsonOnDeserialized, IEventGoldCostState
{
    public const int CurrentVersion = 1;
    public const string AdoptedNodeId = "RAWLS__FREEDOM_AND_INSTITUTIONS__CORE";

    // Reopening a published scheme for everyone, not just for the one standing
    // the player happens to hold.
    public const decimal AmendGoldCost = 25m;

    // The two lines that reach Rawls: the act-three fixed room after Kant's
    // institutions, and after Mill's freedom. The required edge names the line.
    public const string KantRequiredEdgeId = "WESTERN_EDGE_172";
    public const string KantCompletionEdgeId = "WESTERN_EDGE_173";
    public const string MillRequiredEdgeId = "WESTERN_EDGE_169";
    public const string MillCompletionEdgeId = "WESTERN_EDGE_170";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int ActIndex { get; set; } = 3;
    public RawlsPositionStage Stage { get; set; }
    public RawlsPositionAction PendingAction { get; set; }
    public List<RawlsPositionAction> ConfirmedActions { get; set; } = [];
    public RawlsRule Rule { get; set; }
    public string RequiredEdgeId { get; set; } = KantRequiredEdgeId;
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public decimal CostGoldBefore { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;
    public decimal CostGoldAfter { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;

    public decimal Cost => Stage == RawlsPositionStage.ResultAmended ? AmendGoldCost : 0m;

    // Fixed when the room opened, so restoring the save cannot redraw it.
    public RawlsStanding Standing => RawlsPositionFlow.StandingOf(InstanceId);

    // An unknown line falls back to the Kant edge; IsRouteConsistent then requires
    // that edge to be completed, so a corrupted record blocks the room instead of
    // showing it on the wrong line.
    public static RawlsPositionState Create(string instanceId, string requiredEdgeId) => new()
    {
        InstanceId = instanceId,
        RequiredEdgeId = requiredEdgeId is KantRequiredEdgeId or MillRequiredEdgeId ? requiredEdgeId : KantRequiredEdgeId,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
        if (RequiredEdgeId is not (KantRequiredEdgeId or MillRequiredEdgeId)) RequiredEdgeId = KantRequiredEdgeId;
        if (ActIndex is < 1 or > 3) ActIndex = 3;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || ActIndex is < 1 or > 3
            || RequiredEdgeId is not (KantRequiredEdgeId or MillRequiredEdgeId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Rule)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != RawlsPositionAction.None
            || PendingAction != RawlsPositionAction.None
                && !RawlsPositionFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId, RequiredEdgeId);
        replay.ActIndex = ActIndex;
        foreach (RawlsPositionAction action in ConfirmedActions)
        {
            if (replay.Closed || !RawlsPositionFlow.Options(replay.Stage).Contains(action)) return false;
            RawlsPositionFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Rule == replay.Rule && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class RawlsPositionFlow
{
    public static string SourceNodeIdOf(string requiredEdgeId) => requiredEdgeId == RawlsPositionState.MillRequiredEdgeId
        ? "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE"
        : "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE";

    public static string CompletionEdgeIdOf(string requiredEdgeId) => requiredEdgeId == RawlsPositionState.MillRequiredEdgeId
        ? RawlsPositionState.MillCompletionEdgeId : RawlsPositionState.KantCompletionEdgeId;

    // The standing is a stable function of the instance id the run fixed when the
    // room opened. The rule is published first, so the player drafts it without
    // knowing which standing they will hold, and a reload cannot redraw the draw.
    public static RawlsStanding StandingOf(string instanceId)
    {
        ulong hash = 1469598103934665603UL;
        foreach (char c in instanceId ?? string.Empty)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return (RawlsStanding)(hash % 3UL);
    }

    public static IReadOnlyList<RawlsPositionAction> Options(RawlsPositionStage stage) => stage switch
    {
        RawlsPositionStage.Invitation =>
            [RawlsPositionAction.DraftMinimum, RawlsPositionAction.DraftTotalDiscount, RawlsPositionAction.DraftLottery],
        RawlsPositionStage.RuleFrozen =>
            [RawlsPositionAction.Execute, RawlsPositionAction.AmendUniversally, RawlsPositionAction.Withdraw],
        RawlsPositionStage.ResultExecuted or RawlsPositionStage.ResultAmended or RawlsPositionStage.ResultWithdrawn =>
            [RawlsPositionAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(RawlsPositionState state, RawlsPositionAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != RawlsPositionAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(RawlsPositionState state, RawlsPositionAction action)
    {
        switch (action)
        {
            case RawlsPositionAction.DraftMinimum:
                state.Rule = RawlsRule.Minimum; state.Stage = RawlsPositionStage.RuleFrozen; break;
            case RawlsPositionAction.DraftTotalDiscount:
                state.Rule = RawlsRule.TotalDiscount; state.Stage = RawlsPositionStage.RuleFrozen; break;
            case RawlsPositionAction.DraftLottery:
                state.Rule = RawlsRule.Lottery; state.Stage = RawlsPositionStage.RuleFrozen; break;
            case RawlsPositionAction.Execute:
                state.EdgeCommitted = true; state.Stage = RawlsPositionStage.ResultExecuted; break;
            case RawlsPositionAction.AmendUniversally:
                state.EdgeCommitted = true; state.Stage = RawlsPositionStage.ResultAmended; break;
            case RawlsPositionAction.Withdraw:
                state.FixedDeclined = true; state.Stage = RawlsPositionStage.ResultWithdrawn; break;
            case RawlsPositionAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = RawlsPositionAction.None;
    }

    public static bool TryCommitPending(RawlsPositionState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == RawlsPositionAction.None) return false;
        RawlsPositionAction action = state.PendingAction;
        string completion = CompletionEdgeIdOf(state.RequiredEdgeId);
        bool adopts = action is RawlsPositionAction.Execute or RawlsPositionAction.AmendUniversally;
        bool declines = action == RawlsPositionAction.Withdraw;
        if (adopts && !journey.CompletedEdgeIds.Contains(completion) && !graph.TryApply(journey, completion))
            return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3) && !graph.TryDecline(journey, "Fixed", 3))
            return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(RawlsPositionState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(state.RequiredEdgeId)) return false;
        string completion = CompletionEdgeIdOf(state.RequiredEdgeId);
        string source = SourceNodeIdOf(state.RequiredEdgeId);
        bool adopted = journey.CompletedEdgeIds.Contains(completion);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct >= 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == source;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == RawlsPositionState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.CurrentNodeId == source
            && journey.DeclinedFixedActs.Contains(3);
    }

    public static string DescriptionKey(RawlsPositionState state) => state.Stage switch
    {
        RawlsPositionStage.Invitation => "INVITATION",
        RawlsPositionStage.RuleFrozen => "RULE_FROZEN_" + RuleKey(state.Rule),
        RawlsPositionStage.ResultExecuted => "RESULT_EXECUTED_" + RuleKey(state.Rule) + "_" + state.Standing.ToString().ToUpperInvariant(),
        RawlsPositionStage.ResultAmended => "RESULT_AMENDED_" + state.Standing.ToString().ToUpperInvariant(),
        RawlsPositionStage.ResultWithdrawn => "RESULT_WITHDRAWN",
        _ => "INVITATION",
    };

    public static string RuleKey(RawlsRule rule) => rule switch
    {
        RawlsRule.Minimum => "MINIMUM",
        RawlsRule.TotalDiscount => "TOTAL_DISCOUNT",
        RawlsRule.Lottery => "LOTTERY",
        _ => "MINIMUM",
    };

    public static string StageKey(RawlsPositionStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(RawlsPositionAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
