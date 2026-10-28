using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum SchopenhauerWillDenialStage
{
    Invitation, Evidence, ResultRefused, ResultCompensated, ResultShared,
}

internal enum SchopenhauerWillDenialAction
{
    None, BuyExtra, AskPorter, RefuseProfit, PayCompensation, ShareLabor, Leave,
}

internal enum SchopenhauerWillDenialOutcome
{
    None, RefusedProfit, PaidCompensation, SharedLabor,
}

internal sealed class SchopenhauerWillDenialState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const int QuestionAct = 3;
    public const string RequiredNodeId = "IMMANUEL_KANT__SELF_AND_OTHER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_028";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public string KantInstanceId { get; set; } = string.Empty;
    public SchopenhauerWillDenialStage Stage { get; set; }
    public SchopenhauerWillDenialAction PendingAction { get; set; }
    public List<SchopenhauerWillDenialAction> ConfirmedActions { get; set; } = [];
    public SchopenhauerWillDenialOutcome Outcome { get; set; }
    public bool ExtraDemandAccepted { get; set; }
    public bool QuestionReserved { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public bool Closed { get; set; }

    public static SchopenhauerWillDenialState Create(string instanceId, string kantInstanceId) => new()
    {
        InstanceId = instanceId,
        KantInstanceId = kantInstanceId,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        KantInstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || string.IsNullOrWhiteSpace(KantInstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Outcome)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && (PendingAction != SchopenhauerWillDenialAction.None || Outcome == SchopenhauerWillDenialOutcome.None)
            || PendingAction != SchopenhauerWillDenialAction.None
                && !SchopenhauerWillDenialFlow.Options(Stage).Contains(PendingAction)
            || CostPaid && (!CostAttempted || Outcome == SchopenhauerWillDenialOutcome.None)
            || CostAttempted && Outcome == SchopenhauerWillDenialOutcome.None) return false;

        var replay = Create(InstanceId, KantInstanceId);
        foreach (SchopenhauerWillDenialAction action in ConfirmedActions)
        {
            if (replay.Closed || !SchopenhauerWillDenialFlow.Options(replay.Stage).Contains(action)) return false;
            SchopenhauerWillDenialFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Outcome == replay.Outcome
            && ExtraDemandAccepted == replay.ExtraDemandAccepted
            && QuestionReserved == replay.QuestionReserved
            && Closed == replay.Closed;
    }

    public decimal Cost => Outcome switch
    {
        SchopenhauerWillDenialOutcome.RefusedProfit => 15m,
        SchopenhauerWillDenialOutcome.PaidCompensation => 8m,
        SchopenhauerWillDenialOutcome.SharedLabor => 4m,
        _ => 0m,
    };
}

internal static class SchopenhauerWillDenialFlow
{
    public static IReadOnlyList<SchopenhauerWillDenialAction> Options(SchopenhauerWillDenialStage stage) => stage switch
    {
        SchopenhauerWillDenialStage.Invitation =>
            [SchopenhauerWillDenialAction.BuyExtra, SchopenhauerWillDenialAction.AskPorter],
        SchopenhauerWillDenialStage.Evidence =>
            [SchopenhauerWillDenialAction.RefuseProfit, SchopenhauerWillDenialAction.PayCompensation,
                SchopenhauerWillDenialAction.ShareLabor],
        SchopenhauerWillDenialStage.ResultRefused or SchopenhauerWillDenialStage.ResultCompensated
            or SchopenhauerWillDenialStage.ResultShared => [SchopenhauerWillDenialAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(SchopenhauerWillDenialState state, SchopenhauerWillDenialAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != SchopenhauerWillDenialAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    internal static void Apply(SchopenhauerWillDenialState state, SchopenhauerWillDenialAction action)
    {
        switch (action)
        {
            case SchopenhauerWillDenialAction.BuyExtra:
                state.ExtraDemandAccepted = true;
                state.QuestionReserved = true;
                state.Stage = SchopenhauerWillDenialStage.Evidence;
                break;
            case SchopenhauerWillDenialAction.AskPorter:
                state.QuestionReserved = true;
                state.Stage = SchopenhauerWillDenialStage.Evidence;
                break;
            case SchopenhauerWillDenialAction.RefuseProfit:
                state.Outcome = SchopenhauerWillDenialOutcome.RefusedProfit;
                state.Stage = SchopenhauerWillDenialStage.ResultRefused;
                break;
            case SchopenhauerWillDenialAction.PayCompensation:
                state.Outcome = SchopenhauerWillDenialOutcome.PaidCompensation;
                state.Stage = SchopenhauerWillDenialStage.ResultCompensated;
                break;
            case SchopenhauerWillDenialAction.ShareLabor:
                state.Outcome = SchopenhauerWillDenialOutcome.SharedLabor;
                state.Stage = SchopenhauerWillDenialStage.ResultShared;
                break;
            case SchopenhauerWillDenialAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = SchopenhauerWillDenialAction.None;
    }

    public static bool TryCommitPending(SchopenhauerWillDenialState state, WesternJourneyState journey,
        KantApperceptionState kant, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey, kant) || state.Closed
            || state.PendingAction == SchopenhauerWillDenialAction.None) return false;
        SchopenhauerWillDenialAction action = state.PendingAction;
        if (!state.QuestionReserved && !graph.TryReserveQuestion(journey, SchopenhauerWillDenialState.QuestionAct)) return false;
        state.QuestionReserved = true;
        Apply(state, action);
        return IsRouteConsistent(state, journey, kant);
    }

    public static bool IsRouteConsistent(SchopenhauerWillDenialState state, WesternJourneyState journey,
        KantApperceptionState kant) =>
        state.IsUsable() && kant.IsUsable() && kant.Closed && kant.EdgeCommitted
        && state.KantInstanceId == kant.InstanceId
        && KantApperceptionFlow.IsRouteConsistent(kant, journey)
        && journey.CurrentNodeId == SchopenhauerWillDenialState.RequiredNodeId
        && journey.SeenThinkerIds.Contains("IMMANUEL_KANT")
        && journey.LastFixedAct == SchopenhauerWillDenialState.QuestionAct
        && !journey.RetainedEnding
        && journey.CompletedEdgeIds.Contains(SchopenhauerWillDenialState.RequiredEdgeId)
        && journey.ResolvedQuestionActs.Contains(SchopenhauerWillDenialState.QuestionAct) == state.QuestionReserved
        && (state.QuestionReserved || state.Outcome == SchopenhauerWillDenialOutcome.None);

    public static string StageKey(SchopenhauerWillDenialStage stage) => UpperSnake(stage.ToString());

    public static string ActionKey(SchopenhauerWillDenialAction action) => UpperSnake(action.ToString());

    public static string DescriptionKey(SchopenhauerWillDenialState state) => StageKey(state.Stage);

    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
