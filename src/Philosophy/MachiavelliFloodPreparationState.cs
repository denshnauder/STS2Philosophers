using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum MachiavelliFloodPreparationStage { Invitation, Preparation, Flood, ResultAdopted, ResultKept, ResultDeclined }
internal enum MachiavelliFloodPreparationAction { None, AcceptInvitation, DeclineInvitation, ReinforceBank, CoordinateEvacuation, AdoptPrepared, KeepRousseau, Leave }
internal enum MachiavelliPreparation { None, Bank, Coordination }

internal sealed class MachiavelliFloodPreparationState : IJsonOnDeserialized
{
    public const string SourceNodeId = "ROUSSEAU__HISTORY_AND_POWER__CORE";
    public const string AdoptedNodeId = "NICCOLO_MACHIAVELLI__HISTORY_AND_POWER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_046";
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public MachiavelliFloodPreparationStage Stage { get; set; } = (MachiavelliFloodPreparationStage)(-1);
    public MachiavelliFloodPreparationAction PendingAction { get; set; } = (MachiavelliFloodPreparationAction)(-1);
    public List<MachiavelliFloodPreparationAction> ConfirmedActions { get; set; } = null!;
    public MachiavelliPreparation Preparation { get; set; }
    public bool FixedSlotResolved { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Closed { get => _closed ?? false; set => _closed = value; }
    public static MachiavelliFloodPreparationState Create(string instanceId) => new()
    {
        Version = 1, InstanceId = instanceId, Stage = MachiavelliFloodPreparationStage.Invitation,
        PendingAction = MachiavelliFloodPreparationAction.None, ConfirmedActions = [], Closed = false,
    };
    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }
    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();
    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || !_closed.HasValue
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || ConfirmedActions is null || ConfirmedActions.Count > 4) return false;
        var replay = Create(InstanceId);
        foreach (var action in ConfirmedActions)
        {
            if (replay.Closed || !MachiavelliFloodPreparationFlow.Options(replay.Stage).Contains(action)) return false;
            MachiavelliFloodPreparationFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Preparation == replay.Preparation && FixedSlotResolved == replay.FixedSlotResolved
            && EdgeCommitted == replay.EdgeCommitted && Closed == replay.Closed
            && (PendingAction == MachiavelliFloodPreparationAction.None
                || !Closed && MachiavelliFloodPreparationFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class MachiavelliFloodPreparationFlow
{
    public static IReadOnlyList<MachiavelliFloodPreparationAction> Options(MachiavelliFloodPreparationStage stage) => stage switch
    {
        MachiavelliFloodPreparationStage.Invitation => [MachiavelliFloodPreparationAction.AcceptInvitation, MachiavelliFloodPreparationAction.DeclineInvitation],
        MachiavelliFloodPreparationStage.Preparation => [MachiavelliFloodPreparationAction.ReinforceBank, MachiavelliFloodPreparationAction.CoordinateEvacuation],
        MachiavelliFloodPreparationStage.Flood => [MachiavelliFloodPreparationAction.AdoptPrepared, MachiavelliFloodPreparationAction.KeepRousseau],
        >= MachiavelliFloodPreparationStage.ResultAdopted and <= MachiavelliFloodPreparationStage.ResultDeclined => [MachiavelliFloodPreparationAction.Leave],
        _ => [],
    };
    internal static void Apply(MachiavelliFloodPreparationState state, MachiavelliFloodPreparationAction action)
    {
        switch (action)
        {
            case MachiavelliFloodPreparationAction.AcceptInvitation: state.Stage = MachiavelliFloodPreparationStage.Preparation; break;
            case MachiavelliFloodPreparationAction.DeclineInvitation:
                state.Stage = MachiavelliFloodPreparationStage.ResultDeclined; state.FixedSlotResolved = true; break;
            case MachiavelliFloodPreparationAction.ReinforceBank:
                state.Preparation = MachiavelliPreparation.Bank; state.Stage = MachiavelliFloodPreparationStage.Flood; break;
            case MachiavelliFloodPreparationAction.CoordinateEvacuation:
                state.Preparation = MachiavelliPreparation.Coordination; state.Stage = MachiavelliFloodPreparationStage.Flood; break;
            case MachiavelliFloodPreparationAction.AdoptPrepared:
                state.Stage = MachiavelliFloodPreparationStage.ResultAdopted; state.FixedSlotResolved = state.EdgeCommitted = true; break;
            case MachiavelliFloodPreparationAction.KeepRousseau:
                state.Stage = MachiavelliFloodPreparationStage.ResultKept; state.FixedSlotResolved = true; break;
            case MachiavelliFloodPreparationAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action); state.PendingAction = MachiavelliFloodPreparationAction.None;
    }
    public static bool IsRouteConsistent(MachiavelliFloodPreparationState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || !journey.SeenThinkerIds.Contains("ROUSSEAU")
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_040")) return false;
        bool edge = journey.CompletedEdgeIds.Contains(MachiavelliFloodPreparationState.CompletionEdgeId);
        if (!state.FixedSlotResolved)
            return !edge && !journey.RetainedEnding && journey.LastFixedAct == 1
                && journey.CurrentNodeId == MachiavelliFloodPreparationState.SourceNodeId
                && !journey.DeclinedFixedActs.Contains(2)
                && !journey.SeenThinkerIds.Contains("NICCOLO_MACHIAVELLI");
        return (journey.LastFixedAct == 2 && !journey.RetainedEnding
                || state.Closed && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3))
            && (state.EdgeCommitted
                ? edge && journey.CurrentNodeId == MachiavelliFloodPreparationState.AdoptedNodeId
                    && journey.SeenThinkerIds.Contains("NICCOLO_MACHIAVELLI") && !journey.DeclinedFixedActs.Contains(2)
                : !edge && journey.CurrentNodeId == MachiavelliFloodPreparationState.SourceNodeId
                    && journey.DeclinedFixedActs.Contains(2));
    }
    public static bool TryPrepare(MachiavelliFloodPreparationState state, MachiavelliFloodPreparationAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != MachiavelliFloodPreparationAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action; return true;
    }
    public static bool TryCommitPending(MachiavelliFloodPreparationState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.Closed || state.PendingAction == MachiavelliFloodPreparationAction.None) return false;
        var action = state.PendingAction;
        bool adopts = action == MachiavelliFloodPreparationAction.AdoptPrepared;
        bool declines = action is MachiavelliFloodPreparationAction.DeclineInvitation or MachiavelliFloodPreparationAction.KeepRousseau;
        if (adopts && !graph.TryApply(journey, MachiavelliFloodPreparationState.CompletionEdgeId)
            || declines && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action); return IsRouteConsistent(state, journey);
    }
    public static string StageKey(MachiavelliFloodPreparationStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(MachiavelliFloodPreparationAction action) => UpperSnake(action.ToString());
    public static string DescriptionKey(MachiavelliFloodPreparationState state) =>
        state.Stage is MachiavelliFloodPreparationStage.Flood or MachiavelliFloodPreparationStage.ResultAdopted or MachiavelliFloodPreparationStage.ResultKept
            ? $"{StageKey(state.Stage)}_{UpperSnake(state.Preparation.ToString())}" : StageKey(state.Stage);
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
