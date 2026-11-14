using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum ArendtBeginningStage { Invitation, Deliberation, ResultAlone, ResultInvited, ResultSigned }
internal enum ArendtBeginningAction { None, ObserveWaypoint, FinishAlone, InviteProposal, SignOrder, Leave }
internal enum ArendtJudgment { None, Alone, Invited, Signed }

// Saved state for Arendt's scene. A public beginning cannot be declared by one
// person: it happens only when others answer on their own. The record keeps who
// started, whether anyone actually answered, and what is still open; a run where
// nobody answers is stored as an attempt, not as a failure to be re-rolled.
internal sealed class ArendtBeginningState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string AdoptedNodeId = "HANNAH_ARENDT__FREEDOM_AND_INSTITUTIONS__CORE";

    public const string MillRequiredEdgeId = "WESTERN_EDGE_175";
    public const string MillCompletionEdgeId = "WESTERN_EDGE_176";
    // The early Smith turn reaches Mill in act two on 055 and Arendt on 056.
    public const string EarlyMillRequiredEdgeId = "WESTERN_EDGE_055";
    public const string EarlyMillCompletionEdgeId = "WESTERN_EDGE_056";
    public const string FoucaultRequiredEdgeId = "WESTERN_EDGE_181";
    public const string FoucaultCompletionEdgeId = "WESTERN_EDGE_182";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public int ActIndex { get; set; } = 3;
    public ArendtBeginningStage Stage { get; set; }
    public ArendtBeginningAction PendingAction { get; set; }
    public List<ArendtBeginningAction> ConfirmedActions { get; set; } = [];
    public ArendtJudgment Judgment { get; set; }
    public string RequiredEdgeId { get; set; } = MillRequiredEdgeId;
    public bool EdgeCommitted { get; set; }
    public bool Closed { get; set; }

    // Fixed when the room opened: whether anyone answers the invitation is not
    // something the player can decide, and a reload cannot redraw it.
    public bool Answered => ArendtBeginningFlow.AnsweredOf(InstanceId);

    public static ArendtBeginningState Create(string instanceId, string requiredEdgeId) => new()
    {
        InstanceId = instanceId,
        RequiredEdgeId = requiredEdgeId is MillRequiredEdgeId or FoucaultRequiredEdgeId or EarlyMillRequiredEdgeId
            ? requiredEdgeId : MillRequiredEdgeId,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
        if (RequiredEdgeId is not (MillRequiredEdgeId or FoucaultRequiredEdgeId or EarlyMillRequiredEdgeId)) RequiredEdgeId = MillRequiredEdgeId;
        if (ActIndex is < 1 or > 3) ActIndex = 3;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || ActIndex is < 1 or > 3
            || RequiredEdgeId is not (MillRequiredEdgeId or FoucaultRequiredEdgeId or EarlyMillRequiredEdgeId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Judgment)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != ArendtBeginningAction.None
            || PendingAction != ArendtBeginningAction.None
                && !ArendtBeginningFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId, RequiredEdgeId);
        replay.ActIndex = ActIndex;
        foreach (ArendtBeginningAction action in ConfirmedActions)
        {
            if (replay.Closed || !ArendtBeginningFlow.Options(replay.Stage).Contains(action)) return false;
            ArendtBeginningFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Judgment == replay.Judgment && EdgeCommitted == replay.EdgeCommitted
            && Closed == replay.Closed;
    }
}

internal static class ArendtBeginningFlow
{
    public static string SourceNodeIdOf(string requiredEdgeId) => requiredEdgeId == ArendtBeginningState.EarlyMillRequiredEdgeId
        ? "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE"
        : requiredEdgeId == ArendtBeginningState.FoucaultRequiredEdgeId
        ? "MICHEL_FOUCAULT__HISTORY_AND_POWER__CORE"
        : "JOHN_STUART_MILL__FREEDOM_AND_INSTITUTIONS__CORE";

    public static string CompletionEdgeIdOf(string requiredEdgeId) => requiredEdgeId == ArendtBeginningState.EarlyMillRequiredEdgeId
        ? ArendtBeginningState.EarlyMillCompletionEdgeId
        : requiredEdgeId == ArendtBeginningState.FoucaultRequiredEdgeId
            ? ArendtBeginningState.FoucaultCompletionEdgeId : ArendtBeginningState.MillCompletionEdgeId;

    // Whether other people take up the invitation is fixed when the room opens.
    // The player cannot make a public beginning happen alone, and a reload cannot
    // reroll a silence into an answer.
    public static bool AnsweredOf(string instanceId)
    {
        ulong hash = 1469598103934665603UL;
        foreach (char c in instanceId ?? string.Empty)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return (hash & 1UL) == 0UL;
    }

    public static IReadOnlyList<ArendtBeginningAction> Options(ArendtBeginningStage stage) => stage switch
    {
        ArendtBeginningStage.Invitation => [ArendtBeginningAction.ObserveWaypoint],
        ArendtBeginningStage.Deliberation =>
            [ArendtBeginningAction.FinishAlone, ArendtBeginningAction.InviteProposal, ArendtBeginningAction.SignOrder],
        ArendtBeginningStage.ResultAlone or ArendtBeginningStage.ResultInvited or ArendtBeginningStage.ResultSigned =>
            [ArendtBeginningAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(ArendtBeginningState state, ArendtBeginningAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != ArendtBeginningAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(ArendtBeginningState state, ArendtBeginningAction action)
    {
        switch (action)
        {
            case ArendtBeginningAction.ObserveWaypoint: state.Stage = ArendtBeginningStage.Deliberation; break;
            case ArendtBeginningAction.FinishAlone:
                state.Judgment = ArendtJudgment.Alone; state.EdgeCommitted = true;
                state.Stage = ArendtBeginningStage.ResultAlone; break;
            case ArendtBeginningAction.InviteProposal:
                state.Judgment = ArendtJudgment.Invited; state.EdgeCommitted = true;
                state.Stage = ArendtBeginningStage.ResultInvited; break;
            case ArendtBeginningAction.SignOrder:
                state.Judgment = ArendtJudgment.Signed; state.EdgeCommitted = true;
                state.Stage = ArendtBeginningStage.ResultSigned; break;
            case ArendtBeginningAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = ArendtBeginningAction.None;
    }

    public static bool TryCommitPending(ArendtBeginningState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == ArendtBeginningAction.None) return false;
        ArendtBeginningAction action = state.PendingAction;
        string completion = CompletionEdgeIdOf(state.RequiredEdgeId);
        bool adopts = action is ArendtBeginningAction.FinishAlone or ArendtBeginningAction.InviteProposal
            or ArendtBeginningAction.SignOrder;
        if (adopts && !journey.CompletedEdgeIds.Contains(completion) && !graph.TryApply(journey, completion))
            return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(ArendtBeginningState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(state.RequiredEdgeId)) return false;
        string completion = CompletionEdgeIdOf(state.RequiredEdgeId);
        string source = SourceNodeIdOf(state.RequiredEdgeId);
        if (journey.CurrentNodeId == ArendtBeginningState.AdoptedNodeId
            && journey.CompletedEdgeIds.Contains(completion))
            return journey.LastFixedAct == 3;
        bool adopted = journey.CompletedEdgeIds.Contains(completion);
        if (!state.EdgeCommitted)
            return journey.LastFixedAct >= 2 && !adopted
                && journey.CurrentNodeId == source;
        return adopted && journey.LastFixedAct == 3
            && journey.CurrentNodeId == ArendtBeginningState.AdoptedNodeId;
    }

    public static string DescriptionKey(ArendtBeginningState state) => state.Stage switch
    {
        ArendtBeginningStage.Invitation => "INVITATION",
        ArendtBeginningStage.Deliberation => "DELIBERATION",
        ArendtBeginningStage.ResultAlone => "RESULT_ALONE",
        ArendtBeginningStage.ResultInvited => state.Answered ? "RESULT_INVITED_RESPONDED" : "RESULT_INVITED_SILENT",
        ArendtBeginningStage.ResultSigned => "RESULT_SIGNED",
        _ => "INVITATION",
    };

    public static string StageKey(ArendtBeginningStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(ArendtBeginningAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
