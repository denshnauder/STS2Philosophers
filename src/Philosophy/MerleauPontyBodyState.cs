using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum MerleauPontyBodyStage { Invitation, ResultBody, ResultObjective, ResultKept }
internal enum MerleauPontyBodyAction { None, BodyAsSubject, ObjectiveBody, KeepHusserl, Leave }
internal enum MerleauPontyBodyStandard { None, Body, Objective, Husserlian }

/// <summary>
/// Heidegger's second-act room on the phenomenology-and-interpretation line.
/// The player either reads being from its situation, describes Dasein's
/// structures, or keeps Husserl and gives up the act's fixed slot.
/// </summary>
internal sealed class MerleauPontyBodyState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "EDMUND_HUSSERL__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "MAURICE_MERLEAU_PONTY__SELF_AND_OTHER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_074";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public MerleauPontyBodyStage Stage { get; set; }
    public MerleauPontyBodyAction PendingAction { get; set; }
    public List<MerleauPontyBodyAction> ConfirmedActions { get; set; } = [];
    public MerleauPontyBodyStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 1;
    public string EdgeId { get; set; } = CompletionEdgeId;

    public static MerleauPontyBodyState Create(string instanceId) => CreateAt(instanceId, 1, CompletionEdgeId);

    public static MerleauPontyBodyState CreateAt(string instanceId, int actIndex, string edgeId) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= CompletionEdgeId;
        if (ActIndex is < 1 or > 3) ActIndex = 1;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != MerleauPontyBodyAction.None
            || PendingAction != MerleauPontyBodyAction.None
                && !MerleauPontyBodyFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (MerleauPontyBodyAction action in ConfirmedActions)
        {
            if (replay.Closed || !MerleauPontyBodyFlow.Options(replay.Stage).Contains(action)) return false;
            MerleauPontyBodyFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class MerleauPontyBodyFlow
{
    public static IReadOnlyList<MerleauPontyBodyAction> Options(MerleauPontyBodyStage stage) => stage switch
    {
        MerleauPontyBodyStage.Invitation =>
            [MerleauPontyBodyAction.BodyAsSubject, MerleauPontyBodyAction.ObjectiveBody,
                MerleauPontyBodyAction.KeepHusserl],
        MerleauPontyBodyStage.ResultBody or MerleauPontyBodyStage.ResultObjective
            or MerleauPontyBodyStage.ResultKept =>
            [MerleauPontyBodyAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(MerleauPontyBodyState state, MerleauPontyBodyAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != MerleauPontyBodyAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(MerleauPontyBodyState state, MerleauPontyBodyAction action)
    {
        switch (action)
        {
            case MerleauPontyBodyAction.BodyAsSubject:
                state.Standard = MerleauPontyBodyStandard.Body;
                state.EdgeCommitted = true;
                state.Stage = MerleauPontyBodyStage.ResultBody;
                break;
            case MerleauPontyBodyAction.ObjectiveBody:
                state.Standard = MerleauPontyBodyStandard.Objective;
                state.EdgeCommitted = true;
                state.Stage = MerleauPontyBodyStage.ResultObjective;
                break;
            case MerleauPontyBodyAction.KeepHusserl:
                state.Standard = MerleauPontyBodyStandard.Husserlian;
                state.FixedDeclined = true;
                state.Stage = MerleauPontyBodyStage.ResultKept;
                break;
            case MerleauPontyBodyAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = MerleauPontyBodyAction.None;
    }

    public static bool TryCommitPending(MerleauPontyBodyState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == MerleauPontyBodyAction.None) return false;
        MerleauPontyBodyAction action = state.PendingAction;
        bool adopts = action is MerleauPontyBodyAction.BodyAsSubject or MerleauPontyBodyAction.ObjectiveBody;
        bool declines = action == MerleauPontyBodyAction.KeepHusserl;
        if (adopts && !journey.CompletedEdgeIds.Contains(MerleauPontyBodyState.CompletionEdgeId)
            && !graph.TryApply(journey, MerleauPontyBodyState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(MerleauPontyBodyState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(MerleauPontyBodyState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == MerleauPontyBodyState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == MerleauPontyBodyState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && journey.CurrentNodeId == MerleauPontyBodyState.SourceNodeId;
    }

    public static string DescriptionKey(MerleauPontyBodyState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(MerleauPontyBodyStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(MerleauPontyBodyAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
