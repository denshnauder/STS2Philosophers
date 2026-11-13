using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum WittgensteinGameStage { Invitation, ResultGame, ResultPicture, ResultKept }
internal enum WittgensteinGameAction { None, LanguageGame, PictureTheory, KeepRussell, Leave }
internal enum WittgensteinGameStandard { None, Game, Picture, Russellian }

/// <summary>
/// Wittgenstein's third-act resolution on the formal-language line. Russell's
/// second-act room is the prerequisite; the act's single resolution is a question
/// edge (switch to the later language-game view) or keeping Russell.
/// </summary>
internal sealed class WittgensteinGameState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "BERTRAND_RUSSELL__LANGUAGE_AND_MEANING__CORE";
    public const string AdoptedNodeId = "LUDWIG_WITTGENSTEIN__LANGUAGE_AND_MEANING__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_078";
    public const string CompletionEdgeId = "WESTERN_EDGE_079";
    public const string EarlySourceNodeId = "ARISTOTLE__LANGUAGE_AND_MEANING__CORE";
    public const string EarlyCompletionEdgeId = "WESTERN_EDGE_152";
    public const string LateFormalCompletionEdgeId = "WESTERN_EDGE_185";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int ActIndex { get; set; } = 3;
    public string EdgeId { get; set; } = CompletionEdgeId;
    public bool FixedMode { get; set; }
    public string FixedSourceNodeId { get; set; } = string.Empty;
    public bool FixedDeclined { get; set; }
    public WittgensteinGameStage Stage { get; set; }
    public WittgensteinGameAction PendingAction { get; set; }
    public List<WittgensteinGameAction> ConfirmedActions { get; set; } = [];
    public WittgensteinGameStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static WittgensteinGameState Create(string instanceId) =>
        CreateAt(instanceId, 3, CompletionEdgeId, fixedMode: false);

    public static WittgensteinGameState CreateAt(string instanceId, int actIndex, string edgeId, bool fixedMode) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId, FixedMode = fixedMode };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= CompletionEdgeId;
        FixedSourceNodeId ??= string.Empty;
        if (ActIndex is < 2 or > 3) ActIndex = 3;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != WittgensteinGameAction.None
            || PendingAction != WittgensteinGameAction.None
                && !WittgensteinGameFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId, FixedMode);
        foreach (WittgensteinGameAction action in ConfirmedActions)
        {
            if (replay.Closed || !WittgensteinGameFlow.Options(replay.Stage).Contains(action)) return false;
            WittgensteinGameFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && FixedDeclined == replay.FixedDeclined
            && Closed == replay.Closed;
    }
}

internal static class WittgensteinGameFlow
{
    public static IReadOnlyList<WittgensteinGameAction> Options(WittgensteinGameStage stage) => stage switch
    {
        WittgensteinGameStage.Invitation =>
            [WittgensteinGameAction.LanguageGame, WittgensteinGameAction.PictureTheory,
                WittgensteinGameAction.KeepRussell],
        WittgensteinGameStage.ResultGame or WittgensteinGameStage.ResultPicture
            or WittgensteinGameStage.ResultKept => [WittgensteinGameAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(WittgensteinGameState state, WittgensteinGameAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != WittgensteinGameAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(WittgensteinGameState state, WittgensteinGameAction action)
    {
        switch (action)
        {
            case WittgensteinGameAction.LanguageGame:
                state.Standard = WittgensteinGameStandard.Game;
                state.EdgeCommitted = true;
                state.Stage = WittgensteinGameStage.ResultGame;
                break;
            case WittgensteinGameAction.PictureTheory:
                state.Standard = WittgensteinGameStandard.Picture;
                state.EdgeCommitted = true;
                state.Stage = WittgensteinGameStage.ResultPicture;
                break;
            case WittgensteinGameAction.KeepRussell:
                state.Standard = WittgensteinGameStandard.Russellian;
                if (state.FixedMode) state.FixedDeclined = true;
                else state.QuestionDeclined = true;
                state.Stage = WittgensteinGameStage.ResultKept;
                break;
            case WittgensteinGameAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = WittgensteinGameAction.None;
    }

    public static bool TryCommitPending(WittgensteinGameState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == WittgensteinGameAction.None) return false;
        WittgensteinGameAction action = state.PendingAction;
        bool adopts = action is WittgensteinGameAction.LanguageGame or WittgensteinGameAction.PictureTheory;
        bool declines = action == WittgensteinGameAction.KeepRussell;
        if (adopts && state.EdgeId.Length > 0 && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && state.FixedMode && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        if (declines && !state.FixedMode && !journey.ResolvedQuestionActs.Contains(3)
            && !graph.TryDecline(journey, "Question", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(WittgensteinGameState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        if (state.FixedMode)
        {
            string source = state.FixedSourceNodeId.Length > 0
                ? state.FixedSourceNodeId
                : state.ActIndex == 2
                    ? WittgensteinGameState.EarlySourceNodeId
                    : WittgensteinGameState.AdoptedNodeId;
            bool hasEdge = state.EdgeId.Length > 0;
            bool adopted = hasEdge && journey.CompletedEdgeIds.Contains(state.EdgeId);
            bool declined = journey.DeclinedFixedActs.Contains(state.ActIndex);
            if (!state.EdgeCommitted && !state.FixedDeclined)
                return journey.LastFixedAct == state.ActIndex && !declined
                    && (!hasEdge || !adopted) && journey.CurrentNodeId == source;
            if (state.EdgeCommitted)
                return (!hasEdge || adopted) && !state.FixedDeclined
                    && journey.LastFixedAct == Math.Min(3, state.ActIndex + 1)
                    && journey.CurrentNodeId == WittgensteinGameState.AdoptedNodeId;
            return (!hasEdge || !adopted) && declined && journey.LastFixedAct == state.ActIndex
                && journey.CurrentNodeId == source;
        }
        if (!journey.CompletedEdgeIds.Contains(WittgensteinGameState.RequiredEdgeId)) return false;
        bool legacyAdopted = journey.CompletedEdgeIds.Contains(WittgensteinGameState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !legacyAdopted && !journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == WittgensteinGameState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && legacyAdopted && journey.LastFixedAct == 3
                && journey.ResolvedQuestionActs.Contains(3)
                && journey.CurrentNodeId == WittgensteinGameState.AdoptedNodeId;
        return !legacyAdopted && journey.LastFixedAct == 3
            && journey.ResolvedQuestionActs.Contains(3)
            && journey.CurrentNodeId == WittgensteinGameState.SourceNodeId;
    }

    public static string DescriptionKey(WittgensteinGameState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(WittgensteinGameStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(WittgensteinGameAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
