using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HeideggerDaseinStage { Invitation, ResultSituation, ResultDasein, ResultKept }
internal enum HeideggerDaseinAction { None, BeingInSituation, DescribeDasein, KeepHusserl, Leave }
internal enum HeideggerDaseinStandard { None, Situation, Dasein, Husserlian }

/// <summary>
/// Heidegger's second-act room on the phenomenology-and-interpretation line.
/// The player either reads being from its situation, describes Dasein's
/// structures, or keeps Husserl and gives up the act's fixed slot.
/// </summary>
internal sealed class HeideggerDaseinState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "EDMUND_HUSSERL__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "MARTIN_HEIDEGGER__SELF_AND_OTHER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_073";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public HeideggerDaseinStage Stage { get; set; }
    public HeideggerDaseinAction PendingAction { get; set; }
    public List<HeideggerDaseinAction> ConfirmedActions { get; set; } = [];
    public HeideggerDaseinStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static HeideggerDaseinState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != HeideggerDaseinAction.None
            || PendingAction != HeideggerDaseinAction.None
                && !HeideggerDaseinFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (HeideggerDaseinAction action in ConfirmedActions)
        {
            if (replay.Closed || !HeideggerDaseinFlow.Options(replay.Stage).Contains(action)) return false;
            HeideggerDaseinFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class HeideggerDaseinFlow
{
    public static IReadOnlyList<HeideggerDaseinAction> Options(HeideggerDaseinStage stage) => stage switch
    {
        HeideggerDaseinStage.Invitation =>
            [HeideggerDaseinAction.BeingInSituation, HeideggerDaseinAction.DescribeDasein,
                HeideggerDaseinAction.KeepHusserl],
        HeideggerDaseinStage.ResultSituation or HeideggerDaseinStage.ResultDasein
            or HeideggerDaseinStage.ResultKept =>
            [HeideggerDaseinAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(HeideggerDaseinState state, HeideggerDaseinAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != HeideggerDaseinAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(HeideggerDaseinState state, HeideggerDaseinAction action)
    {
        switch (action)
        {
            case HeideggerDaseinAction.BeingInSituation:
                state.Standard = HeideggerDaseinStandard.Situation;
                state.EdgeCommitted = true;
                state.Stage = HeideggerDaseinStage.ResultSituation;
                break;
            case HeideggerDaseinAction.DescribeDasein:
                state.Standard = HeideggerDaseinStandard.Dasein;
                state.EdgeCommitted = true;
                state.Stage = HeideggerDaseinStage.ResultDasein;
                break;
            case HeideggerDaseinAction.KeepHusserl:
                state.Standard = HeideggerDaseinStandard.Husserlian;
                state.FixedDeclined = true;
                state.Stage = HeideggerDaseinStage.ResultKept;
                break;
            case HeideggerDaseinAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = HeideggerDaseinAction.None;
    }

    public static bool TryCommitPending(HeideggerDaseinState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == HeideggerDaseinAction.None) return false;
        HeideggerDaseinAction action = state.PendingAction;
        bool adopts = action is HeideggerDaseinAction.BeingInSituation or HeideggerDaseinAction.DescribeDasein;
        bool declines = action == HeideggerDaseinAction.KeepHusserl;
        if (adopts && !journey.CompletedEdgeIds.Contains(HeideggerDaseinState.CompletionEdgeId)
            && !graph.TryApply(journey, HeideggerDaseinState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HeideggerDaseinState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(HeideggerDaseinState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == HeideggerDaseinState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == HeideggerDaseinState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && journey.CurrentNodeId == HeideggerDaseinState.SourceNodeId;
    }

    public static string DescriptionKey(HeideggerDaseinState state) => state.Stage switch
    {
        HeideggerDaseinStage.Invitation => "INVITATION",
        HeideggerDaseinStage.ResultSituation => "RESULT_SEPARATION",
        HeideggerDaseinStage.ResultDasein => "RESULT_BALANCE",
        HeideggerDaseinStage.ResultKept => "RESULT_PRIVILEGE",
        _ => "INVITATION",
    };

    public static string StageKey(HeideggerDaseinStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HeideggerDaseinAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
