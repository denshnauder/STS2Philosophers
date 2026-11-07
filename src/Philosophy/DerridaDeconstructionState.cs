using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum DerridaDeconstructionStage { Invitation, ResultBinary, ResultDifferance, ResultKept }
internal enum DerridaDeconstructionAction { None, BinaryHierarchy, Differance, KeepLeviStrauss, Leave }
internal enum DerridaDeconstructionStandard { None, Binary, Differance, Structuralist }

/// <summary>
/// Derrida's third-act resolution on the language-and-meaning line.
/// Levi-Strauss's second-act room is the prerequisite; the act's single
/// resolution is a question edge (deconstruction) or keeping Levi-Strauss.
/// </summary>
internal sealed class DerridaDeconstructionState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "CLAUDE_LEVI_STRAUSS__LANGUAGE_AND_MEANING__CORE";
    public const string AdoptedNodeId = "JACQUES_DERRIDA__LANGUAGE_AND_MEANING__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_081";
    public const string CompletionEdgeId = "WESTERN_EDGE_082";
    public const string FixedRequiredEdgeId = "WESTERN_EDGE_187";
    public const string FixedCompletionEdgeId = "WESTERN_EDGE_188";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public DerridaDeconstructionStage Stage { get; set; }
    public DerridaDeconstructionAction PendingAction { get; set; }
    public List<DerridaDeconstructionAction> ConfirmedActions { get; set; } = [];
    public DerridaDeconstructionStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }
    /// <summary>True when the route takes Derrida as the third act's fixed room.</summary>
    public bool FixedMode { get; set; }
    public int ActIndex { get; set; } = 3;
    public string EdgeId { get; set; } = CompletionEdgeId;

    public static DerridaDeconstructionState Create(string instanceId) =>
        CreateAt(instanceId, 3, CompletionEdgeId, fixedMode: false);

    public static DerridaDeconstructionState CreateAt(string instanceId, int actIndex, string edgeId, bool fixedMode) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId, FixedMode = fixedMode };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
        if (ActIndex is < 1 or > 3) ActIndex = 3;
        EdgeId = string.IsNullOrWhiteSpace(EdgeId)
            ? (FixedMode ? FixedCompletionEdgeId : CompletionEdgeId) : EdgeId;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != DerridaDeconstructionAction.None
            || PendingAction != DerridaDeconstructionAction.None
                && !DerridaDeconstructionFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId, FixedMode);
        foreach (DerridaDeconstructionAction action in ConfirmedActions)
        {
            if (replay.Closed || !DerridaDeconstructionFlow.Options(replay.Stage).Contains(action)) return false;
            DerridaDeconstructionFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class DerridaDeconstructionFlow
{
    public static IReadOnlyList<DerridaDeconstructionAction> Options(DerridaDeconstructionStage stage) => stage switch
    {
        DerridaDeconstructionStage.Invitation =>
            [DerridaDeconstructionAction.BinaryHierarchy, DerridaDeconstructionAction.Differance,
                DerridaDeconstructionAction.KeepLeviStrauss],
        DerridaDeconstructionStage.ResultBinary or DerridaDeconstructionStage.ResultDifferance
            or DerridaDeconstructionStage.ResultKept => [DerridaDeconstructionAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(DerridaDeconstructionState state, DerridaDeconstructionAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != DerridaDeconstructionAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(DerridaDeconstructionState state, DerridaDeconstructionAction action)
    {
        switch (action)
        {
            case DerridaDeconstructionAction.BinaryHierarchy:
                state.Standard = DerridaDeconstructionStandard.Binary;
                state.EdgeCommitted = true;
                state.Stage = DerridaDeconstructionStage.ResultBinary;
                break;
            case DerridaDeconstructionAction.Differance:
                state.Standard = DerridaDeconstructionStandard.Differance;
                state.EdgeCommitted = true;
                state.Stage = DerridaDeconstructionStage.ResultDifferance;
                break;
            case DerridaDeconstructionAction.KeepLeviStrauss:
                state.Standard = DerridaDeconstructionStandard.Structuralist;
                state.QuestionDeclined = true;
                state.Stage = DerridaDeconstructionStage.ResultKept;
                break;
            case DerridaDeconstructionAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = DerridaDeconstructionAction.None;
    }

    public static bool TryCommitPending(DerridaDeconstructionState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == DerridaDeconstructionAction.None) return false;
        DerridaDeconstructionAction action = state.PendingAction;
        bool adopts = action is DerridaDeconstructionAction.BinaryHierarchy or DerridaDeconstructionAction.Differance;
        bool declines = action == DerridaDeconstructionAction.KeepLeviStrauss;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines)
        {
            string slot = state.FixedMode ? "Fixed" : "Question";
            bool resolved = state.FixedMode
                ? journey.DeclinedFixedActs.Contains(3)
                : journey.ResolvedQuestionActs.Contains(3);
            if (!resolved && !graph.TryDecline(journey, slot, 3)) return false;
        }
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(DerridaDeconstructionState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        string required = state.FixedMode ? DerridaDeconstructionState.FixedRequiredEdgeId : DerridaDeconstructionState.RequiredEdgeId;
        if (!journey.CompletedEdgeIds.Contains(required)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        bool slotResolved = state.FixedMode
            ? journey.DeclinedFixedActs.Contains(3)
            : journey.ResolvedQuestionActs.Contains(3);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 3 && !adopted && !slotResolved
                && journey.CurrentNodeId == DerridaDeconstructionState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 3
                && (state.FixedMode || journey.ResolvedQuestionActs.Contains(3))
                && journey.CurrentNodeId == DerridaDeconstructionState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && slotResolved
            && journey.CurrentNodeId == DerridaDeconstructionState.SourceNodeId;
    }

    public static string DescriptionKey(DerridaDeconstructionState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(DerridaDeconstructionStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(DerridaDeconstructionAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
