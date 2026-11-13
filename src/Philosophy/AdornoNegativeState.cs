using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum AdornoNegativeStage { Invitation, ResultNegative, ResultCritique, ResultKept }
internal enum AdornoNegativeAction { None, NegativeDialectics, IdentityCritique, KeepHorkheimer, Leave }
internal enum AdornoNegativeStandard { None, Negative, Critique, Horkheimerian }

/// <summary>
/// Adorno's third-act resolution on the social-critique line. Horkheimer's
/// second-act room is the prerequisite; the act's single resolution is a
/// question edge (negative dialectics) or keeping Horkheimer.
/// </summary>
internal sealed class AdornoNegativeState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "MAX_HORKHEIMER__HISTORY_AND_POWER__CORE";
    public const string AdoptedNodeId = "THEODOR_ADORNO__HISTORY_AND_POWER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_084";
    public const string CompletionEdgeId = "WESTERN_EDGE_085";
    public const string FixedRequiredEdgeId = "WESTERN_EDGE_178";
    public const string FixedCompletionEdgeId = "WESTERN_EDGE_179";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int ActIndex { get; set; } = 3;
    public string EdgeId { get; set; } = CompletionEdgeId;
    public bool FixedMode { get; set; }
    public AdornoNegativeStage Stage { get; set; }
    public AdornoNegativeAction PendingAction { get; set; }
    public List<AdornoNegativeAction> ConfirmedActions { get; set; } = [];
    public AdornoNegativeStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static AdornoNegativeState Create(string instanceId) => new() { InstanceId = instanceId };

    public static AdornoNegativeState CreateAt(string instanceId, int actIndex, string edgeId, bool fixedMode) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId, FixedMode = fixedMode };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= CompletionEdgeId;
        if (ActIndex is < 1 or > 3) ActIndex = 3;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != AdornoNegativeAction.None
            || PendingAction != AdornoNegativeAction.None
                && !AdornoNegativeFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId, FixedMode);
        foreach (AdornoNegativeAction action in ConfirmedActions)
        {
            if (replay.Closed || !AdornoNegativeFlow.Options(replay.Stage).Contains(action)) return false;
            AdornoNegativeFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && FixedDeclined == replay.FixedDeclined
            && Closed == replay.Closed;
    }
}

internal static class AdornoNegativeFlow
{
    public static IReadOnlyList<AdornoNegativeAction> Options(AdornoNegativeStage stage) => stage switch
    {
        AdornoNegativeStage.Invitation =>
            [AdornoNegativeAction.NegativeDialectics, AdornoNegativeAction.IdentityCritique,
                AdornoNegativeAction.KeepHorkheimer],
        AdornoNegativeStage.ResultNegative or AdornoNegativeStage.ResultCritique
            or AdornoNegativeStage.ResultKept => [AdornoNegativeAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(AdornoNegativeState state, AdornoNegativeAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != AdornoNegativeAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(AdornoNegativeState state, AdornoNegativeAction action)
    {
        switch (action)
        {
            case AdornoNegativeAction.NegativeDialectics:
                state.Standard = AdornoNegativeStandard.Negative;
                state.EdgeCommitted = true;
                state.Stage = AdornoNegativeStage.ResultNegative;
                break;
            case AdornoNegativeAction.IdentityCritique:
                state.Standard = AdornoNegativeStandard.Critique;
                state.EdgeCommitted = true;
                state.Stage = AdornoNegativeStage.ResultCritique;
                break;
            case AdornoNegativeAction.KeepHorkheimer:
                state.Standard = AdornoNegativeStandard.Horkheimerian;
                if (state.FixedMode) state.FixedDeclined = true;
                else state.QuestionDeclined = true;
                state.Stage = AdornoNegativeStage.ResultKept;
                break;
            case AdornoNegativeAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = AdornoNegativeAction.None;
    }

    public static bool TryCommitPending(AdornoNegativeState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == AdornoNegativeAction.None) return false;
        AdornoNegativeAction action = state.PendingAction;
        bool adopts = action is AdornoNegativeAction.NegativeDialectics or AdornoNegativeAction.IdentityCritique;
        bool declines = action == AdornoNegativeAction.KeepHorkheimer;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && state.FixedMode && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        if (declines && !state.FixedMode && !journey.ResolvedQuestionActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Question", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(AdornoNegativeState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        string required = state.FixedMode ? AdornoNegativeState.FixedRequiredEdgeId : AdornoNegativeState.RequiredEdgeId;
        if (!journey.CompletedEdgeIds.Contains(required)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        bool declined = state.FixedMode
            ? journey.DeclinedFixedActs.Contains(state.ActIndex)
            : journey.ResolvedQuestionActs.Contains(state.ActIndex);
        if (!state.EdgeCommitted && !declined)
        {
            if (state.FixedMode)
                return journey.LastFixedAct is 2 or 3 && !adopted
                    && journey.CurrentNodeId == AdornoNegativeState.SourceNodeId;
            return journey.LastFixedAct == 3 && !adopted
                && journey.CurrentNodeId == AdornoNegativeState.SourceNodeId;
        }
        if (state.EdgeCommitted)
            return adopted && journey.LastFixedAct == 3 && (state.FixedMode || declined)
                && journey.CurrentNodeId == AdornoNegativeState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && declined
            && journey.CurrentNodeId == AdornoNegativeState.SourceNodeId;
    }

    public static string DescriptionKey(AdornoNegativeState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(AdornoNegativeStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(AdornoNegativeAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
