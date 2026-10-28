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
    public const string AdoptedNodeId = "GEORG_WILHELM_FRIEDRICH_HEGEL__BEING_AND_CHANGE__CORE";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
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
            && !journey.CompletedEdgeIds.Contains(HegelSubstanceSubjectState.CompletionEdgeId)
            && !graph.TryApply(journey, HegelSubstanceSubjectState.CompletionEdgeId)) return false;
        if (action == HegelSubstanceSubjectAction.KeepSpinoza
            && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(HegelSubstanceSubjectState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(HegelSubstanceSubjectState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(HegelSubstanceSubjectState.CompletionEdgeId);
        bool declined = journey.DeclinedFixedActs.Contains(3);
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == HegelSubstanceSubjectState.AdoptedNodeId;
        if (state.FixedDeclined)
            return !adopted && declined && journey.LastFixedAct == 3
                && journey.CurrentNodeId == HegelSubstanceSubjectState.SourceNodeId;
        return journey.LastFixedAct == 2 && !adopted && !declined
            && journey.CurrentNodeId == HegelSubstanceSubjectState.SourceNodeId;
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
