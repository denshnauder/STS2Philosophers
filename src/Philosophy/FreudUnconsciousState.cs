using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum FreudUnconsciousStage { Invitation, ResultUnconscious, ResultDream, ResultKept }
internal enum FreudUnconsciousAction { None, Unconscious, DreamWork, KeepDescartes, Leave }
internal enum FreudUnconsciousStandard { None, Unconscious, Dream, Cartesian }

/// <summary>
/// Kierkegaard's first-act room on the self-and-other line. The player either
/// takes the leap of faith, keeps the ethical universal, or stays with Socrates
/// and gives up the act's fixed slot.
/// </summary>
internal sealed class FreudUnconsciousState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "DESCARTES__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "SIGMUND_FREUD__SELF_AND_OTHER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_087";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public FreudUnconsciousStage Stage { get; set; }
    public FreudUnconsciousAction PendingAction { get; set; }
    public List<FreudUnconsciousAction> ConfirmedActions { get; set; } = [];
    public FreudUnconsciousStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static FreudUnconsciousState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != FreudUnconsciousAction.None
            || PendingAction != FreudUnconsciousAction.None
                && !FreudUnconsciousFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (FreudUnconsciousAction action in ConfirmedActions)
        {
            if (replay.Closed || !FreudUnconsciousFlow.Options(replay.Stage).Contains(action)) return false;
            FreudUnconsciousFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class FreudUnconsciousFlow
{
    public static IReadOnlyList<FreudUnconsciousAction> Options(FreudUnconsciousStage stage) => stage switch
    {
        FreudUnconsciousStage.Invitation =>
            [FreudUnconsciousAction.Unconscious, FreudUnconsciousAction.DreamWork,
                FreudUnconsciousAction.KeepDescartes],
        FreudUnconsciousStage.ResultUnconscious or FreudUnconsciousStage.ResultDream
            or FreudUnconsciousStage.ResultKept => [FreudUnconsciousAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(FreudUnconsciousState state, FreudUnconsciousAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != FreudUnconsciousAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(FreudUnconsciousState state, FreudUnconsciousAction action)
    {
        switch (action)
        {
            case FreudUnconsciousAction.Unconscious:
                state.Standard = FreudUnconsciousStandard.Unconscious;
                state.EdgeCommitted = true;
                state.Stage = FreudUnconsciousStage.ResultUnconscious;
                break;
            case FreudUnconsciousAction.DreamWork:
                state.Standard = FreudUnconsciousStandard.Dream;
                state.EdgeCommitted = true;
                state.Stage = FreudUnconsciousStage.ResultDream;
                break;
            case FreudUnconsciousAction.KeepDescartes:
                state.Standard = FreudUnconsciousStandard.Cartesian;
                state.FixedDeclined = true;
                state.Stage = FreudUnconsciousStage.ResultKept;
                break;
            case FreudUnconsciousAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = FreudUnconsciousAction.None;
    }

    public static bool TryCommitPending(FreudUnconsciousState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == FreudUnconsciousAction.None) return false;
        FreudUnconsciousAction action = state.PendingAction;
        bool adopts = action is FreudUnconsciousAction.Unconscious or FreudUnconsciousAction.DreamWork;
        bool declines = action == FreudUnconsciousAction.KeepDescartes;
        if (adopts && !journey.CompletedEdgeIds.Contains(FreudUnconsciousState.CompletionEdgeId)
            && !graph.TryApply(journey, FreudUnconsciousState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(2)
            && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(FreudUnconsciousState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(FreudUnconsciousState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 1 && !adopted && !journey.DeclinedFixedActs.Contains(2)
                && journey.CurrentNodeId == FreudUnconsciousState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 2
                && journey.CurrentNodeId == FreudUnconsciousState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 2 && journey.DeclinedFixedActs.Contains(2)
            && journey.CurrentNodeId == FreudUnconsciousState.SourceNodeId;
    }

    public static string DescriptionKey(FreudUnconsciousState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(FreudUnconsciousStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(FreudUnconsciousAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
