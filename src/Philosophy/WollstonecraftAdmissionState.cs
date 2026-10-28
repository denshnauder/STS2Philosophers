using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum WollstonecraftAdmissionStage { Invitation, Evidence, ResultUnified, ResultFunded, ResultExclusion, ResultKept }
internal enum WollstonecraftAdmissionAction { None, CompareRecords, UnifyRules, FundBoth, KeepPractice, KeepRousseau, Leave }
internal enum WollstonecraftLoss { None, RecommendationPriority, EvaluationSlot }

internal sealed class WollstonecraftAdmissionState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ROUSSEAU__HISTORY_AND_POWER__CORE";
    public const string AdoptedNodeId = "MARY_WOLLSTONECRAFT__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_040";
    public const string RivalEdgeId = "WESTERN_EDGE_046";
    public const string OriginalClause = "UNEQUAL_PROOF_CLAUSE";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public WollstonecraftAdmissionStage Stage { get; set; }
    public WollstonecraftAdmissionAction PendingAction { get; set; }
    public List<WollstonecraftAdmissionAction> ConfirmedActions { get; set; } = [];
    public string Clause { get; set; } = string.Empty;
    public bool EvidenceChecked { get; set; }
    public WollstonecraftLoss Loss { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static WollstonecraftAdmissionState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
        Clause ??= string.Empty;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Loss)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != WollstonecraftAdmissionAction.None
            || PendingAction != WollstonecraftAdmissionAction.None
                && !WollstonecraftAdmissionFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = Create(InstanceId);
        foreach (WollstonecraftAdmissionAction action in ConfirmedActions)
        {
            if (replay.Closed || !WollstonecraftAdmissionFlow.Options(replay.Stage).Contains(action)) return false;
            WollstonecraftAdmissionFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Clause == replay.Clause && EvidenceChecked == replay.EvidenceChecked
            && Loss == replay.Loss && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class WollstonecraftAdmissionFlow
{
    public static IReadOnlyList<WollstonecraftAdmissionAction> Options(WollstonecraftAdmissionStage stage) => stage switch
    {
        WollstonecraftAdmissionStage.Invitation => [WollstonecraftAdmissionAction.CompareRecords, WollstonecraftAdmissionAction.KeepRousseau],
        WollstonecraftAdmissionStage.Evidence => [WollstonecraftAdmissionAction.UnifyRules, WollstonecraftAdmissionAction.FundBoth, WollstonecraftAdmissionAction.KeepPractice],
        WollstonecraftAdmissionStage.ResultUnified or WollstonecraftAdmissionStage.ResultFunded
            or WollstonecraftAdmissionStage.ResultExclusion or WollstonecraftAdmissionStage.ResultKept =>
            [WollstonecraftAdmissionAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(WollstonecraftAdmissionState state, WollstonecraftAdmissionAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != WollstonecraftAdmissionAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(WollstonecraftAdmissionState state, WollstonecraftAdmissionAction action)
    {
        switch (action)
        {
            case WollstonecraftAdmissionAction.CompareRecords:
                state.Clause = WollstonecraftAdmissionState.OriginalClause;
                state.EvidenceChecked = true;
                state.Stage = WollstonecraftAdmissionStage.Evidence;
                break;
            case WollstonecraftAdmissionAction.UnifyRules:
                state.Loss = WollstonecraftLoss.RecommendationPriority;
                state.EdgeCommitted = true;
                state.Stage = WollstonecraftAdmissionStage.ResultUnified;
                break;
            case WollstonecraftAdmissionAction.FundBoth:
                state.Loss = WollstonecraftLoss.EvaluationSlot;
                state.EdgeCommitted = true;
                state.Stage = WollstonecraftAdmissionStage.ResultFunded;
                break;
            case WollstonecraftAdmissionAction.KeepPractice:
                state.Loss = WollstonecraftLoss.None;
                state.EdgeCommitted = true;
                state.Stage = WollstonecraftAdmissionStage.ResultExclusion;
                break;
            case WollstonecraftAdmissionAction.KeepRousseau:
                state.FixedDeclined = true;
                state.Stage = WollstonecraftAdmissionStage.ResultKept;
                break;
            case WollstonecraftAdmissionAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = WollstonecraftAdmissionAction.None;
    }

    public static bool TryCommitPending(WollstonecraftAdmissionState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == WollstonecraftAdmissionAction.None) return false;
        WollstonecraftAdmissionAction action = state.PendingAction;
        bool adopts = action is WollstonecraftAdmissionAction.UnifyRules
            or WollstonecraftAdmissionAction.FundBoth or WollstonecraftAdmissionAction.KeepPractice;
        bool declines = action == WollstonecraftAdmissionAction.KeepRousseau;
        if (adopts && !journey.CompletedEdgeIds.Contains(WollstonecraftAdmissionState.CompletionEdgeId)
            && !graph.TryApply(journey, WollstonecraftAdmissionState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(2)
            && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(WollstonecraftAdmissionState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || journey.CompletedEdgeIds.Contains(WollstonecraftAdmissionState.RivalEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(WollstonecraftAdmissionState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 1 && !adopted
                && !journey.DeclinedFixedActs.Contains(2)
                && journey.CurrentNodeId == WollstonecraftAdmissionState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 2
                && journey.CurrentNodeId == WollstonecraftAdmissionState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 2
            && journey.CurrentNodeId == WollstonecraftAdmissionState.SourceNodeId
            && journey.DeclinedFixedActs.Contains(2);
    }

    public static string DescriptionKey(WollstonecraftAdmissionState state) => state.Stage switch
    {
        WollstonecraftAdmissionStage.Invitation => "INVITATION",
        WollstonecraftAdmissionStage.Evidence => "EVIDENCE",
        WollstonecraftAdmissionStage.ResultUnified => "RESULT_UNIFIED",
        WollstonecraftAdmissionStage.ResultFunded => "RESULT_FUNDED",
        WollstonecraftAdmissionStage.ResultExclusion => "RESULT_EXCLUSION",
        WollstonecraftAdmissionStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(WollstonecraftAdmissionStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(WollstonecraftAdmissionAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
