using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum HegelSubstanceSubjectStage { Invitation, Ledger, ResultAdopted, ResultKept }
internal enum HegelSubstanceSubjectAction { None, InspectLedger, AdoptSubject, KeepSpinoza, Leave }

internal sealed class HegelSubstanceSubjectState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_024";
    public const string CompletionEdgeId = "WESTERN_EDGE_025";

    // A run the first act's question turned onto Kant stands on him in the act the
    // route calls its second, so Hegel is reached by that turn and not by Spinoza.
    public const string TurnedSourceNodeId = "IMMANUEL_KANT__SELF_AND_OTHER__CORE";
    public const string TurnedRequiredEdgeId = "WESTERN_EDGE_153";
    public const string TurnedAdoptEdgeId = "WESTERN_EDGE_154";
    public const string TurnedAdoptedNodeId = "GEORG_WILHELM_FRIEDRICH_HEGEL__HISTORY_AND_POWER__CORE";
    public const string AdoptedNodeId = "GEORG_WILHELM_FRIEDRICH_HEGEL__BEING_AND_CHANGE__CORE";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    // The act and the edges this question belongs to.  They stay at the line's own
    // values unless the first act's question turned the run onto Hegel early.
    public int ActIndex { get; set; } = 3;
    public string RequiredEdge { get; set; } = RequiredEdgeId;
    public string AdoptEdge { get; set; } = CompletionEdgeId;
    public string AdoptedNode { get; set; } = AdoptedNodeId;
    public string SourceNode { get; set; } = SourceNodeId;
    public HegelSubstanceSubjectStage Stage { get; set; }
    public HegelSubstanceSubjectAction PendingAction { get; set; }
    public List<HegelSubstanceSubjectAction> ConfirmedActions { get; set; } = [];
    public bool LedgerInspected { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static HegelSubstanceSubjectState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != HegelSubstanceSubjectAction.None
            || PendingAction != HegelSubstanceSubjectAction.None
                && !HegelSubstanceSubjectFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = Create(InstanceId);
        foreach (HegelSubstanceSubjectAction action in ConfirmedActions)
        {
            if (replay.Closed || !HegelSubstanceSubjectFlow.Options(replay.Stage).Contains(action)) return false;
            HegelSubstanceSubjectFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && LedgerInspected == replay.LedgerInspected
            && EdgeCommitted == replay.EdgeCommitted && FixedDeclined == replay.FixedDeclined
            && Closed == replay.Closed;
    }
}

internal static class HegelSubstanceSubjectFlow
{
    public static IReadOnlyList<HegelSubstanceSubjectAction> Options(HegelSubstanceSubjectStage stage) => stage switch
    {
        HegelSubstanceSubjectStage.Invitation =>
            [HegelSubstanceSubjectAction.InspectLedger, HegelSubstanceSubjectAction.KeepSpinoza],
        HegelSubstanceSubjectStage.Ledger =>
            [HegelSubstanceSubjectAction.AdoptSubject, HegelSubstanceSubjectAction.KeepSpinoza],
        HegelSubstanceSubjectStage.ResultAdopted or HegelSubstanceSubjectStage.ResultKept =>
            [HegelSubstanceSubjectAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(HegelSubstanceSubjectState state, HegelSubstanceSubjectAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != HegelSubstanceSubjectAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(HegelSubstanceSubjectState state, HegelSubstanceSubjectAction action)
    {
        switch (action)
        {
            case HegelSubstanceSubjectAction.InspectLedger:
                state.LedgerInspected = true;
                state.Stage = HegelSubstanceSubjectStage.Ledger;
                break;
            case HegelSubstanceSubjectAction.AdoptSubject:
                state.EdgeCommitted = true;
                state.Stage = HegelSubstanceSubjectStage.ResultAdopted;
                break;
            case HegelSubstanceSubjectAction.KeepSpinoza:
                state.FixedDeclined = true;
                state.Stage = HegelSubstanceSubjectStage.ResultKept;
                break;
            case HegelSubstanceSubjectAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = HegelSubstanceSubjectAction.None;
    }

    public static bool TryCommitPending(HegelSubstanceSubjectState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == HegelSubstanceSubjectAction.None) return false;
        HegelSubstanceSubjectAction action = state.PendingAction;
        if (action == HegelSubstanceSubjectAction.AdoptSubject
            && !journey.CompletedEdgeIds.Contains(state.AdoptEdge)
            && !graph.TryApply(journey, state.AdoptEdge)) return false;
        if (action == HegelSubstanceSubjectAction.KeepSpinoza
            && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HegelSubstanceSubjectState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        // The edge that handed Hegel over is what proves the turn; a run that walked
        // the line itself proves it by standing on the figure that hands him over.
        if (state.ActIndex == 2 && !journey.CompletedEdgeIds.Contains(state.RequiredEdge))
            return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.AdoptEdge);
        bool declined = journey.DeclinedFixedActs.Contains(state.ActIndex);
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == state.ActIndex
                && journey.CurrentNodeId == state.AdoptedNode;
        if (state.FixedDeclined)
            return !adopted && declined && journey.LastFixedAct == state.ActIndex
                && journey.CurrentNodeId == state.SourceNode;
        return journey.LastFixedAct >= 2 && !adopted && !declined
            && journey.CurrentNodeId == state.SourceNode;
    }

    public static string DescriptionKey(HegelSubstanceSubjectState state) => state.Stage switch
    {
        HegelSubstanceSubjectStage.Invitation => "INVITATION",
        HegelSubstanceSubjectStage.Ledger => "LEDGER",
        HegelSubstanceSubjectStage.ResultAdopted => "RESULT_ADOPTED",
        HegelSubstanceSubjectStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(HegelSubstanceSubjectStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(HegelSubstanceSubjectAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
