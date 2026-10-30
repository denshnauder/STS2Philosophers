using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum NietzscheRevaluationStage { Invitation, ResultRevalue, ResultGenealogy, ResultKept }
internal enum NietzscheRevaluationAction { None, RevalueAllValues, GenealogyExposure, KeepRousseau, Leave }
internal enum NietzscheRevaluationStandard { None, Revaluation, Genealogy, Rousseauvian }

/// <summary>
/// Kierkegaard's first-act room on the self-and-other line. The player either
/// takes the leap of faith, keeps the ethical universal, or stays with Socrates
/// and gives up the act's fixed slot.
/// </summary>
internal sealed class NietzscheRevaluationState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ROUSSEAU__HISTORY_AND_POWER__CORE";
    public const string AdoptedNodeId = "FRIEDRICH_NIETZSCHE__HISTORY_AND_POWER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_062";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public NietzscheRevaluationStage Stage { get; set; }
    public NietzscheRevaluationAction PendingAction { get; set; }
    public List<NietzscheRevaluationAction> ConfirmedActions { get; set; } = [];
    public NietzscheRevaluationStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static NietzscheRevaluationState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != NietzscheRevaluationAction.None
            || PendingAction != NietzscheRevaluationAction.None
                && !NietzscheRevaluationFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (NietzscheRevaluationAction action in ConfirmedActions)
        {
            if (replay.Closed || !NietzscheRevaluationFlow.Options(replay.Stage).Contains(action)) return false;
            NietzscheRevaluationFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class NietzscheRevaluationFlow
{
    public static IReadOnlyList<NietzscheRevaluationAction> Options(NietzscheRevaluationStage stage) => stage switch
    {
        NietzscheRevaluationStage.Invitation =>
            [NietzscheRevaluationAction.RevalueAllValues, NietzscheRevaluationAction.GenealogyExposure,
                NietzscheRevaluationAction.KeepRousseau],
        NietzscheRevaluationStage.ResultRevalue or NietzscheRevaluationStage.ResultGenealogy
            or NietzscheRevaluationStage.ResultKept => [NietzscheRevaluationAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(NietzscheRevaluationState state, NietzscheRevaluationAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != NietzscheRevaluationAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(NietzscheRevaluationState state, NietzscheRevaluationAction action)
    {
        switch (action)
        {
            case NietzscheRevaluationAction.RevalueAllValues:
                state.Standard = NietzscheRevaluationStandard.Revaluation;
                state.EdgeCommitted = true;
                state.Stage = NietzscheRevaluationStage.ResultRevalue;
                break;
            case NietzscheRevaluationAction.GenealogyExposure:
                state.Standard = NietzscheRevaluationStandard.Genealogy;
                state.EdgeCommitted = true;
                state.Stage = NietzscheRevaluationStage.ResultGenealogy;
                break;
            case NietzscheRevaluationAction.KeepRousseau:
                state.Standard = NietzscheRevaluationStandard.Rousseauvian;
                state.FixedDeclined = true;
                state.Stage = NietzscheRevaluationStage.ResultKept;
                break;
            case NietzscheRevaluationAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = NietzscheRevaluationAction.None;
    }

    public static bool TryCommitPending(NietzscheRevaluationState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == NietzscheRevaluationAction.None) return false;
        NietzscheRevaluationAction action = state.PendingAction;
        bool adopts = action is NietzscheRevaluationAction.RevalueAllValues or NietzscheRevaluationAction.GenealogyExposure;
        bool declines = action == NietzscheRevaluationAction.KeepRousseau;
        if (adopts && !journey.CompletedEdgeIds.Contains(NietzscheRevaluationState.CompletionEdgeId)
            && !graph.TryApply(journey, NietzscheRevaluationState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(2)
            && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(NietzscheRevaluationState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(NietzscheRevaluationState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 1 && !adopted && !journey.DeclinedFixedActs.Contains(2)
                && journey.CurrentNodeId == NietzscheRevaluationState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 2
                && journey.CurrentNodeId == NietzscheRevaluationState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 2 && journey.DeclinedFixedActs.Contains(2)
            && journey.CurrentNodeId == NietzscheRevaluationState.SourceNodeId;
    }

    public static string DescriptionKey(NietzscheRevaluationState state) => state.Stage switch
    {
        NietzscheRevaluationStage.Invitation => "INVITATION",
        NietzscheRevaluationStage.ResultRevalue => "RESULT_SEPARATION",
        NietzscheRevaluationStage.ResultGenealogy => "RESULT_BALANCE",
        NietzscheRevaluationStage.ResultKept => "RESULT_PRIVILEGE",
        _ => "INVITATION",
    };

    public static string StageKey(NietzscheRevaluationStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(NietzscheRevaluationAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
