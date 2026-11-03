using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum ButlerAddressStage { Invitation, Ledger, ResultAmended, ResultAlternative, ResultKept }
internal enum ButlerAddressAction { None, ObserveLedger, AmendRegistry, AlternativeProcess, KeepLedger, Leave }
internal enum ButlerAddressJudgment { None, Amended, Alternative, Kept }

// Saved state for Butler's scene. It records only what the player changed in
// public and what the newcomer chose to disclose; the run never stores anything
// about a real player's identity, and the excluded person is never the answer to
// a puzzle. The record says which act it belongs to so the same body can serve
// the act-three fixed room.
internal sealed class ButlerAddressState : IJsonOnDeserialized, IEventGoldCostState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "SIMONE_DE_BEAUVOIR__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "JUDITH_BUTLER__SELF_AND_OTHER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_193";
    public const string CompletionEdgeId = "WESTERN_EDGE_194";

    // Reprinting the sign-off table is the price of changing the procedure
    // instead of only explaining it to the person it excludes.
    public const decimal ReprintGoldCost = 15m;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int ActIndex { get; set; } = 3;
    public ButlerAddressStage Stage { get; set; }
    public ButlerAddressAction PendingAction { get; set; }
    public List<ButlerAddressAction> ConfirmedActions { get; set; } = [];
    public ButlerAddressJudgment Judgment { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public decimal CostGoldBefore { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;
    public decimal CostGoldAfter { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;

    public decimal Cost => Judgment == ButlerAddressJudgment.Amended ? ReprintGoldCost : 0m;

    public static ButlerAddressState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
        if (ActIndex is < 1 or > 3) ActIndex = 3;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || ActIndex is < 1 or > 3
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Judgment)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != ButlerAddressAction.None
            || PendingAction != ButlerAddressAction.None
                && !ButlerAddressFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        replay.ActIndex = ActIndex;
        foreach (ButlerAddressAction action in ConfirmedActions)
        {
            if (replay.Closed || !ButlerAddressFlow.Options(replay.Stage).Contains(action)) return false;
            ButlerAddressFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Judgment == replay.Judgment && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class ButlerAddressFlow
{
    public static IReadOnlyList<ButlerAddressAction> Options(ButlerAddressStage stage) => stage switch
    {
        ButlerAddressStage.Invitation => [ButlerAddressAction.ObserveLedger, ButlerAddressAction.KeepLedger],
        ButlerAddressStage.Ledger =>
            [ButlerAddressAction.AmendRegistry, ButlerAddressAction.AlternativeProcess, ButlerAddressAction.KeepLedger],
        ButlerAddressStage.ResultAmended or ButlerAddressStage.ResultAlternative or ButlerAddressStage.ResultKept =>
            [ButlerAddressAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(ButlerAddressState state, ButlerAddressAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != ButlerAddressAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(ButlerAddressState state, ButlerAddressAction action)
    {
        switch (action)
        {
            case ButlerAddressAction.ObserveLedger: state.Stage = ButlerAddressStage.Ledger; break;
            case ButlerAddressAction.AmendRegistry:
                state.Judgment = ButlerAddressJudgment.Amended; state.EdgeCommitted = true;
                state.Stage = ButlerAddressStage.ResultAmended; break;
            case ButlerAddressAction.AlternativeProcess:
                state.Judgment = ButlerAddressJudgment.Alternative; state.EdgeCommitted = true;
                state.Stage = ButlerAddressStage.ResultAlternative; break;
            case ButlerAddressAction.KeepLedger:
                state.Judgment = ButlerAddressJudgment.Kept; state.FixedDeclined = true;
                state.Stage = ButlerAddressStage.ResultKept; break;
            case ButlerAddressAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = ButlerAddressAction.None;
    }

    public static bool TryCommitPending(ButlerAddressState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == ButlerAddressAction.None) return false;
        ButlerAddressAction action = state.PendingAction;
        bool adopts = action is ButlerAddressAction.AmendRegistry or ButlerAddressAction.AlternativeProcess;
        bool declines = action == ButlerAddressAction.KeepLedger;
        if (adopts && !journey.CompletedEdgeIds.Contains(ButlerAddressState.CompletionEdgeId)
            && !graph.TryApply(journey, ButlerAddressState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(ButlerAddressState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(ButlerAddressState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(ButlerAddressState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct >= 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == ButlerAddressState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == ButlerAddressState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.CurrentNodeId == ButlerAddressState.SourceNodeId
            && journey.DeclinedFixedActs.Contains(3);
    }

    public static string DescriptionKey(ButlerAddressState state) => state.Stage switch
    {
        ButlerAddressStage.Invitation => "INVITATION",
        ButlerAddressStage.Ledger => "LEDGER",
        ButlerAddressStage.ResultAmended => "RESULT_AMENDED",
        ButlerAddressStage.ResultAlternative => "RESULT_ALTERNATIVE",
        ButlerAddressStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(ButlerAddressStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(ButlerAddressAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
