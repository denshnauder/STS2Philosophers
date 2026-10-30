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

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public AdornoNegativeStage Stage { get; set; }
    public AdornoNegativeAction PendingAction { get; set; }
    public List<AdornoNegativeAction> ConfirmedActions { get; set; } = [];
    public AdornoNegativeStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static AdornoNegativeState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != AdornoNegativeAction.None
            || PendingAction != AdornoNegativeAction.None
                && !AdornoNegativeFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (AdornoNegativeAction action in ConfirmedActions)
        {
            if (replay.Closed || !AdornoNegativeFlow.Options(replay.Stage).Contains(action)) return false;
            AdornoNegativeFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
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
                state.QuestionDeclined = true;
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
        if (adopts && !journey.CompletedEdgeIds.Contains(AdornoNegativeState.CompletionEdgeId)
            && !graph.TryApply(journey, AdornoNegativeState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(3)
            && !graph.TryDecline(journey, "Question", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(AdornoNegativeState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(AdornoNegativeState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(AdornoNegativeState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !adopted && !journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == AdornoNegativeState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 3
                && journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == AdornoNegativeState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3
            && journey.ResolvedQuestionActs.Contains(3)
            && journey.CurrentNodeId == AdornoNegativeState.SourceNodeId;
    }

    public static string DescriptionKey(AdornoNegativeState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(AdornoNegativeStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(AdornoNegativeAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
