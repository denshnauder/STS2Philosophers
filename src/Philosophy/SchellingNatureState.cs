using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum SchellingNatureStage { Invitation, ResultSpirit, ResultMechanism, ResultKept }
internal enum SchellingNatureAction { None, NatureAsSpirit, NatureAsMechanism, KeepSpinoza, Leave }
internal enum SchellingNatureStandard { None, Spirit, Mechanism, Spinoza }

/// <summary>
/// Schelling's second-act room on the being-and-change line. The player either
/// adopts nature as visible spirit or as mechanism, or keeps Spinoza and gives
/// up the act's fixed slot.
/// </summary>
internal sealed class SchellingNatureState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE";
    public const string AdoptedNodeId = "FRIEDRICH_WILHELM_JOSEPH_SCHELLING__BEING_AND_CHANGE__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_057";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public SchellingNatureStage Stage { get; set; }
    public SchellingNatureAction PendingAction { get; set; }
    public List<SchellingNatureAction> ConfirmedActions { get; set; } = [];
    public SchellingNatureStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static SchellingNatureState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != SchellingNatureAction.None
            || PendingAction != SchellingNatureAction.None
                && !SchellingNatureFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (SchellingNatureAction action in ConfirmedActions)
        {
            if (replay.Closed || !SchellingNatureFlow.Options(replay.Stage).Contains(action)) return false;
            SchellingNatureFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class SchellingNatureFlow
{
    public static IReadOnlyList<SchellingNatureAction> Options(SchellingNatureStage stage) => stage switch
    {
        SchellingNatureStage.Invitation =>
            [SchellingNatureAction.NatureAsSpirit, SchellingNatureAction.NatureAsMechanism,
                SchellingNatureAction.KeepSpinoza],
        SchellingNatureStage.ResultSpirit or SchellingNatureStage.ResultMechanism
            or SchellingNatureStage.ResultKept => [SchellingNatureAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(SchellingNatureState state, SchellingNatureAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != SchellingNatureAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(SchellingNatureState state, SchellingNatureAction action)
    {
        switch (action)
        {
            case SchellingNatureAction.NatureAsSpirit:
                state.Standard = SchellingNatureStandard.Spirit;
                state.EdgeCommitted = true;
                state.Stage = SchellingNatureStage.ResultSpirit;
                break;
            case SchellingNatureAction.NatureAsMechanism:
                state.Standard = SchellingNatureStandard.Mechanism;
                state.EdgeCommitted = true;
                state.Stage = SchellingNatureStage.ResultMechanism;
                break;
            case SchellingNatureAction.KeepSpinoza:
                state.Standard = SchellingNatureStandard.Spinoza;
                state.FixedDeclined = true;
                state.Stage = SchellingNatureStage.ResultKept;
                break;
            case SchellingNatureAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = SchellingNatureAction.None;
    }

    public static bool TryCommitPending(SchellingNatureState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == SchellingNatureAction.None) return false;
        SchellingNatureAction action = state.PendingAction;
        bool adopts = action is SchellingNatureAction.NatureAsSpirit or SchellingNatureAction.NatureAsMechanism;
        bool declines = action == SchellingNatureAction.KeepSpinoza;
        if (adopts && !journey.CompletedEdgeIds.Contains(SchellingNatureState.CompletionEdgeId)
            && !graph.TryApply(journey, SchellingNatureState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(SchellingNatureState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(SchellingNatureState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == SchellingNatureState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == SchellingNatureState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && journey.CurrentNodeId == SchellingNatureState.SourceNodeId;
    }

    public static string DescriptionKey(SchellingNatureState state) => state.Stage switch
    {
        SchellingNatureStage.Invitation => "INVITATION",
        SchellingNatureStage.ResultSpirit => "RESULT_SEPARATION",
        SchellingNatureStage.ResultMechanism => "RESULT_BALANCE",
        SchellingNatureStage.ResultKept => "RESULT_PRIVILEGE",
        _ => "INVITATION",
    };

    public static string StageKey(SchellingNatureStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(SchellingNatureAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
