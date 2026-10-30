using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum KuhnParadigmStage { Measurement, Decision, ResultKeptMethod, ResultTrained, ResultParallel }
internal enum KuhnParadigmAction
{
    None, CheckInstruments, StudyNewClassification, KeepOldMethod, TrainTeam, ObserveInParallel, Leave,
}
internal enum KuhnParadigmStandard { None, OldMethod, Trained, Parallel }

/// <summary>
/// Kuhn's third-act resolution on the science line. Either Newton (model and
/// counterexample) or Comte (social stage model) can be the second-act host;
/// the act's single resolution is a question edge (train on the new scale) or
/// keeping the old method, so an anomaly never upgrades a theory by itself.
/// </summary>
internal sealed class KuhnParadigmState : IJsonOnDeserialized, IEventGoldCostState
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ISAAC_NEWTON__KNOWLEDGE_AND_DOUBT__CORE";
    public const string AdoptedNodeId = "THOMAS_KUHN__KNOWLEDGE_AND_DOUBT__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_049";
    public const string CompletionEdgeId = "WESTERN_EDGE_090";
    public const decimal TrainTeamGoldCost = 75m;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
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
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || AnomalyCount < 0 || IncomparableDimensions < 0
            || Closed && PendingAction != KuhnParadigmAction.None
            || PendingAction != KuhnParadigmAction.None
                && !KuhnParadigmFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = Create(InstanceId);
        foreach (KuhnParadigmAction action in ConfirmedActions)
        {
            if (replay.Closed || !KuhnParadigmFlow.Options(replay.Stage).Contains(action)) return false;
            KuhnParadigmFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard
            && AnomalyCount == replay.AnomalyCount
            && IncomparableDimensions == replay.IncomparableDimensions
            && EdgeCommitted == replay.EdgeCommitted && QuestionDeclined == replay.QuestionDeclined
            && Closed == replay.Closed;
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

    public static bool TryPrepare(KuhnParadigmState state, KuhnParadigmAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != KuhnParadigmAction.None
            || !Options(state.Stage).Contains(action)) return false;
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
        if (adopts && !graph.TryApply(journey, KuhnParadigmState.CompletionEdgeId)) return false;
        if (declines && !graph.TryDecline(journey, "Question", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(KuhnParadigmState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(KuhnParadigmState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(KuhnParadigmState.CompletionEdgeId);
        bool resolved = journey.ResolvedQuestionActs.Contains(3);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !adopted && !resolved
                && journey.CurrentNodeId == KuhnParadigmState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == KuhnParadigmState.AdoptedNodeId;
        return !adopted && resolved && journey.LastFixedAct == 3
            && journey.CurrentNodeId == KuhnParadigmState.SourceNodeId;
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
