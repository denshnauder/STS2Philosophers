using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HumeInductionStage { Invitation, Test, ResultRepair, ResultKeep, ResultRisk, ResultKept }
internal enum HumeInductionAction { None, TestOnce, BuyPass, RepairFirst, KeepPrediction, RetractNecessity, KeepLocke, Leave }
internal enum HumeBasis { None, Tested, Habit }

internal sealed class HumeInductionState : IJsonOnDeserialized, IEventGoldCostState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "JOHN_LOCKE__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "DAVID_HUME__SELF_AND_OTHER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_026";
    public const string CompletionEdgeId = "WESTERN_EDGE_027";
    public const string SecondActEdgeId = "WESTERN_EDGE_093";

    /// <summary>The act this instance was presented in; the default line reaches it in the third.</summary>
    public int ActIndex { get; set; } = 2;

    public string CompletionEdgeIdFor => ActIndex == 2 ? SecondActEdgeId : CompletionEdgeId;
    public const decimal RepairFirstGoldCost = 75m;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public HumeInductionStage Stage { get; set; }
    public HumeInductionAction PendingAction { get; set; }
    public List<HumeInductionAction> ConfirmedActions { get; set; } = [];
    public HumeBasis Basis { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public decimal CostGoldBefore { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;
    public decimal CostGoldAfter { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;

    public decimal Cost => Stage == HumeInductionStage.ResultRepair ? RepairFirstGoldCost : 0m;

    public static HumeInductionState Create(string instanceId) => new() { InstanceId = instanceId };

    public static HumeInductionState CreateAt(string instanceId, int actIndex) =>
        new() { InstanceId = instanceId, ActIndex = actIndex };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Basis)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != HumeInductionAction.None
            || PendingAction != HumeInductionAction.None
                && !HumeInductionFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = Create(InstanceId);
        foreach (HumeInductionAction action in ConfirmedActions)
        {
            if (replay.Closed || !HumeInductionFlow.Options(replay.Stage).Contains(action)) return false;
            HumeInductionFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Basis == replay.Basis && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class HumeInductionFlow
{
    public static IReadOnlyList<HumeInductionAction> Options(HumeInductionStage stage) => stage switch
    {
        HumeInductionStage.Invitation => [HumeInductionAction.TestOnce, HumeInductionAction.BuyPass, HumeInductionAction.KeepLocke],
        HumeInductionStage.Test => [HumeInductionAction.RepairFirst, HumeInductionAction.KeepPrediction, HumeInductionAction.RetractNecessity],
        HumeInductionStage.ResultRepair or HumeInductionStage.ResultKeep or HumeInductionStage.ResultRisk or HumeInductionStage.ResultKept =>
            [HumeInductionAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(HumeInductionState state, HumeInductionAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != HumeInductionAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(HumeInductionState state, HumeInductionAction action)
    {
        switch (action)
        {
            case HumeInductionAction.TestOnce: state.Basis = HumeBasis.Tested; state.Stage = HumeInductionStage.Test; break;
            case HumeInductionAction.BuyPass: state.Basis = HumeBasis.Habit; state.Stage = HumeInductionStage.Test; break;
            case HumeInductionAction.RepairFirst: state.Stage = HumeInductionStage.ResultRepair; state.EdgeCommitted = true; break;
            case HumeInductionAction.KeepPrediction: state.Stage = HumeInductionStage.ResultKeep; state.EdgeCommitted = true; break;
            case HumeInductionAction.RetractNecessity: state.Stage = HumeInductionStage.ResultRisk; state.EdgeCommitted = true; break;
            case HumeInductionAction.KeepLocke: state.FixedDeclined = true; state.Stage = HumeInductionStage.ResultKept; break;
            case HumeInductionAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = HumeInductionAction.None;
    }

    public static bool TryCommitPending(HumeInductionState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == HumeInductionAction.None) return false;
        HumeInductionAction action = state.PendingAction;
        bool adopts = action is HumeInductionAction.RepairFirst or HumeInductionAction.KeepPrediction or HumeInductionAction.RetractNecessity;
        bool declines = action == HumeInductionAction.KeepLocke;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.CompletionEdgeIdFor)
            && !graph.TryApply(journey, state.CompletionEdgeIdFor)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(Math.Min(3, state.ActIndex))
            && !graph.TryDecline(journey, "Fixed", Math.Min(3, state.ActIndex))) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HumeInductionState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(HumeInductionState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.CompletionEdgeIdFor);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct is 1 or 2 or 3 && !adopted
                && !journey.DeclinedFixedActs.Contains(Math.Min(3, state.ActIndex))
                && journey.CurrentNodeId == HumeInductionState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct is 2 or 3
                && journey.CurrentNodeId == HumeInductionState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct is 2 or 3
            && journey.CurrentNodeId == HumeInductionState.SourceNodeId
            && journey.DeclinedFixedActs.Contains(Math.Min(3, state.ActIndex));
    }

    public static string DescriptionKey(HumeInductionState state) => state.Stage switch
    {
        HumeInductionStage.Invitation => "INVITATION",
        HumeInductionStage.Test => state.Basis == HumeBasis.Tested ? "TEST_OBSERVED" : "TEST_HABIT",
        HumeInductionStage.ResultRepair => "RESULT_REPAIR",
        HumeInductionStage.ResultKeep => "RESULT_KEEP",
        HumeInductionStage.ResultRisk => "RESULT_RISK",
        HumeInductionStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(HumeInductionStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HumeInductionAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
