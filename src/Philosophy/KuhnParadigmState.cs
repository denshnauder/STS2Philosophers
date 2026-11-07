using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum KuhnParadigmStage { Measurement, Decision, ResultKeptMethod, ResultTrained, ResultParallel }
internal enum KuhnParadigmAction
{
    None, CheckInstruments, StudyNewClassification, KeepOldMethod, TrainTeam, ObserveInParallel, Leave,
}
internal enum KuhnParadigmStandard { None, OldMethod, Trained, Parallel }

/// <summary>
/// Kuhn's third-act room on the two turned knowledge lines. The room owns the
/// act's fixed slot: Newton's route (166 -> 167) or Comte's route (198 -> 199).
/// The default lines still close on Newton/Comte; only the early first-act turn
/// reaches this room. An anomaly is recorded before any paradigm change, and a
/// single counterexample never replaces the whole method by itself.
/// </summary>
internal sealed class KuhnParadigmState : IJsonOnDeserialized, IEventGoldCostState
{
    public const int CurrentVersion = 1;
    public const string NewtonSourceNodeId = "ISAAC_NEWTON__KNOWLEDGE_AND_DOUBT__CORE";
    public const string ComteSourceNodeId = "COMTE__KNOWLEDGE_AND_DOUBT__CORE";
    public const string SourceNodeId = NewtonSourceNodeId;
    public const string AdoptedNodeId = "THOMAS_KUHN__KNOWLEDGE_AND_DOUBT__CORE";

    // The old question-mode record stays readable; the fixed room uses the two
    // route-specific edges that the graph already carries.
    public const string LegacyRequiredEdgeId = "WESTERN_EDGE_049";
    public const string LegacyCompletionEdgeId = "WESTERN_EDGE_090";
    public const string NewtonRequiredEdgeId = "WESTERN_EDGE_166";
    public const string ComteRequiredEdgeId = "WESTERN_EDGE_198";
    public const string NewtonCompletionEdgeId = "WESTERN_EDGE_167";
    public const string ComteCompletionEdgeId = "WESTERN_EDGE_199";
    public const string ScientificRouteId = "SCIENTIFIC_MODEL";
    public const string SocialRouteId = "SOCIAL_KNOWLEDGE";

    public const string RequiredEdgeId = LegacyRequiredEdgeId;
    public const string CompletionEdgeId = NewtonCompletionEdgeId;
    public const decimal TrainTeamGoldCost = 75m;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>The act this room occupies; both fixed edges are third-act edges.</summary>
    public int ActIndex { get; set; } = 3;

    /// <summary>True for the new fixed modes; false for the old question mode.</summary>
    public bool FixedMode { get; set; }

    /// <summary>The edge this room's adoption commits.</summary>
    public string EdgeId { get; set; } = LegacyCompletionEdgeId;

    /// <summary>True when a route, not the default line, placed this room in its act.</summary>
    public bool RoutePlaced { get; set; }

    /// <summary>The node a route arrives from when it places this room.</summary>
    public string RouteSourceNodeId { get; set; } = string.Empty;

    public KuhnParadigmStage Stage { get; set; }
    public KuhnParadigmAction PendingAction { get; set; }
    public List<KuhnParadigmAction> ConfirmedActions { get; set; } = [];
    public KuhnParadigmStandard Standard { get; set; }
    public int AnomalyCount { get; set; }
    public int IncomparableDimensions { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public decimal CostGoldBefore { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;
    public decimal CostGoldAfter { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;

    public decimal Cost => Standard == KuhnParadigmStandard.Trained ? TrainTeamGoldCost : 0m;

    public static KuhnParadigmState Create(string instanceId) => new() { InstanceId = instanceId };

    public static KuhnParadigmState CreateAt(string instanceId, int actIndex, string edgeId) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId, FixedMode = true };

    public static string RequiredEdgeFor(string edgeId) => edgeId switch
    {
        ComteCompletionEdgeId => ComteRequiredEdgeId,
        NewtonCompletionEdgeId => NewtonRequiredEdgeId,
        _ => LegacyRequiredEdgeId,
    };

    public static string SourceNodeFor(string edgeId) =>
        edgeId == ComteCompletionEdgeId ? ComteSourceNodeId : NewtonSourceNodeId;

    public static string RouteFor(string edgeId) =>
        edgeId == ComteCompletionEdgeId ? SocialRouteId : ScientificRouteId;

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= LegacyCompletionEdgeId;
        RouteSourceNodeId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || ActIndex is < 1 or > 3
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || AnomalyCount is < 0 or > 1 || IncomparableDimensions is < 0 or > 2
            || Closed && PendingAction != KuhnParadigmAction.None
            || PendingAction != KuhnParadigmAction.None
                && !KuhnParadigmFlow.Options(Stage).Contains(PendingAction)
            || FixedMode && PendingAction == KuhnParadigmAction.TrainTeam
                && !KuhnParadigmFlow.CanAdopt(this)) return false;
        // A fixed record is bound to one of the two route edges and to act three.
        // The legacy question record is bound to the old 090 edge.
        if (FixedMode)
        {
            if (ActIndex != 3 || EdgeId is not (NewtonCompletionEdgeId or ComteCompletionEdgeId)) return false;
        }
        else if (EdgeId != LegacyCompletionEdgeId)
        {
            return false;
        }

        var replay = FixedMode ? CreateAt(InstanceId, ActIndex, EdgeId) : Create(InstanceId);
        foreach (KuhnParadigmAction action in ConfirmedActions)
        {
            if (replay.Closed || !KuhnParadigmFlow.Options(replay.Stage).Contains(action)) return false;
            // A fixed record cannot claim a paradigm change from a single
            // anomaly: adoption needs the incompatible dimensions recorded by
            // the classification page. Legacy question records keep their old
            // replay path so pre-fixed saves stay readable.
            if (FixedMode && action == KuhnParadigmAction.TrainTeam
                && !KuhnParadigmFlow.CanAdopt(replay)) return false;
            KuhnParadigmFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard
            && AnomalyCount == replay.AnomalyCount
            && IncomparableDimensions == replay.IncomparableDimensions
            && EdgeCommitted == replay.EdgeCommitted && QuestionDeclined == replay.QuestionDeclined
            && Closed == replay.Closed && FixedMode == replay.FixedMode
            && EdgeId == replay.EdgeId && ActIndex == replay.ActIndex;
    }
}

internal static class KuhnParadigmFlow
{
    public static IReadOnlyList<KuhnParadigmAction> Options(KuhnParadigmStage stage) => stage switch
    {
        KuhnParadigmStage.Measurement => [KuhnParadigmAction.CheckInstruments, KuhnParadigmAction.StudyNewClassification],
        KuhnParadigmStage.Decision =>
            [KuhnParadigmAction.KeepOldMethod, KuhnParadigmAction.TrainTeam, KuhnParadigmAction.ObserveInParallel],
        KuhnParadigmStage.ResultKeptMethod or KuhnParadigmStage.ResultTrained or KuhnParadigmStage.ResultParallel =>
            [KuhnParadigmAction.Leave],
        _ => [],
    };

    public static bool CanAdopt(KuhnParadigmState state) => state.IncomparableDimensions >= 2;

    public static bool TryPrepare(KuhnParadigmState state, KuhnParadigmAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != KuhnParadigmAction.None
            || !Options(state.Stage).Contains(action)
            || action == KuhnParadigmAction.TrainTeam && !CanAdopt(state)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(KuhnParadigmState state, KuhnParadigmAction action)
    {
        switch (action)
        {
            case KuhnParadigmAction.CheckInstruments:
                state.AnomalyCount = 1; state.IncomparableDimensions = 0;
                state.Stage = KuhnParadigmStage.Decision; break;
            case KuhnParadigmAction.StudyNewClassification:
                state.AnomalyCount = 1; state.IncomparableDimensions = 2;
                state.Stage = KuhnParadigmStage.Decision; break;
            case KuhnParadigmAction.KeepOldMethod:
                state.Standard = KuhnParadigmStandard.OldMethod; state.QuestionDeclined = true;
                state.Stage = KuhnParadigmStage.ResultKeptMethod; break;
            case KuhnParadigmAction.TrainTeam:
                state.Standard = KuhnParadigmStandard.Trained; state.EdgeCommitted = true;
                state.Stage = KuhnParadigmStage.ResultTrained; break;
            case KuhnParadigmAction.ObserveInParallel:
                state.Standard = KuhnParadigmStandard.Parallel; state.QuestionDeclined = true;
                state.Stage = KuhnParadigmStage.ResultParallel; break;
            case KuhnParadigmAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = KuhnParadigmAction.None;
    }

    public static bool TryCommitPending(KuhnParadigmState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.Closed
            || state.PendingAction == KuhnParadigmAction.None) return false;
        KuhnParadigmAction action = state.PendingAction;
        bool adopts = action == KuhnParadigmAction.TrainTeam;
        bool declines = action is KuhnParadigmAction.KeepOldMethod or KuhnParadigmAction.ObserveInParallel;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines)
        {
            if (state.FixedMode)
            {
                if (!journey.DeclinedFixedActs.Contains(state.ActIndex)
                    && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
            }
            else if (!journey.ResolvedQuestionActs.Contains(state.ActIndex)
                && !graph.TryDecline(journey, "Question", state.ActIndex)) return false;
        }
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(KuhnParadigmState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        if (state.FixedMode) return IsFixedRouteConsistent(state, journey);

        // The old question mode stays playable for saves written before the
        // fixed handover. New runs always use the fixed mode above.
        if (!journey.CompletedEdgeIds.Contains(KuhnParadigmState.RequiredEdgeId)) return false;
        bool legacyAdopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == state.ActIndex && !legacyAdopted
                && !journey.ResolvedQuestionActs.Contains(state.ActIndex)
                && journey.CurrentNodeId == KuhnParadigmState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && legacyAdopted
                && journey.LastFixedAct == state.ActIndex
                && journey.ResolvedQuestionActs.Contains(state.ActIndex)
                && journey.CurrentNodeId == KuhnParadigmState.AdoptedNodeId;
        return !legacyAdopted && journey.LastFixedAct == state.ActIndex
            && journey.ResolvedQuestionActs.Contains(state.ActIndex)
            && journey.CurrentNodeId == KuhnParadigmState.SourceNodeId;
    }

    private static bool IsFixedRouteConsistent(KuhnParadigmState state, WesternJourneyState journey)
    {
        if (state.ActIndex != 3 || state.EdgeId is not (KuhnParadigmState.NewtonCompletionEdgeId or KuhnParadigmState.ComteCompletionEdgeId))
            return false;
        if (!journey.CompletedEdgeIds.Contains(KuhnParadigmState.RequiredEdgeFor(state.EdgeId))) return false;
        if (!string.Equals(journey.RouteId, KuhnParadigmState.RouteFor(state.EdgeId), StringComparison.Ordinal)) return false;
        string source = KuhnParadigmState.SourceNodeFor(state.EdgeId);
        bool atSource = journey.CurrentNodeId == source
            && (state.RouteSourceNodeId.Length == 0 || state.RouteSourceNodeId == source);
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == state.ActIndex && !adopted
                && !journey.DeclinedFixedActs.Contains(state.ActIndex) && atSource;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted
                && journey.LastFixedAct == Math.Min(3, state.ActIndex + 1)
                && journey.CurrentNodeId == KuhnParadigmState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == state.ActIndex
            && journey.DeclinedFixedActs.Contains(state.ActIndex) && atSource;
    }

    public static string StageKey(KuhnParadigmStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(KuhnParadigmAction action) => UpperSnake(action.ToString());

    public static string DescriptionKey(KuhnParadigmState state) => state.Stage switch
    {
        KuhnParadigmStage.ResultKeptMethod => "RESULT_KEPT_METHOD",
        KuhnParadigmStage.ResultTrained => "RESULT_TRAINED",
        KuhnParadigmStage.ResultParallel => "RESULT_PARALLEL",
        _ => state.Stage == KuhnParadigmStage.Measurement ? "MEASUREMENT" : "DECISION",
    };

    private static string UpperSnake(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 4);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsUpper(current) && index > 0) builder.Append('_');
            builder.Append(char.ToUpperInvariant(current));
        }
        return builder.ToString();
    }
}
