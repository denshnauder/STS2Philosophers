using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum MachiavelliFortuneAfterStage { Invitation, ResultConfirmed, ResultKept }
internal enum MachiavelliFortuneAfterAction { None, KeepPractice, LetFortuneChange, Leave }

/// <summary>
/// Machiavelli's third-act closing on the fortune-and-virtue line. The line
/// already stands on Machiavelli after the act-two flood preparation; this room
/// owns the act-three fixed slot without taking another handover edge, so it
/// cannot create a fourth fixed room or move the line to a new figure.
/// </summary>
internal sealed class MachiavelliFortuneAfterState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "NICCOLO_MACHIAVELLI__HISTORY_AND_POWER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_046";
    public const string RouteId = "FORTUNE_AND_VIRTUE";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public MachiavelliFortuneAfterStage Stage { get; set; }
    public MachiavelliFortuneAfterAction PendingAction { get; set; }
    public List<MachiavelliFortuneAfterAction> ConfirmedActions { get; set; } = [];
    public bool FixedSlotResolved { get; set; }
    public bool Confirmed { get; set; }
    public bool Closed { get; set; }

    public static MachiavelliFortuneAfterState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != MachiavelliFortuneAfterAction.None
            || PendingAction != MachiavelliFortuneAfterAction.None
                && !MachiavelliFortuneAfterFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (MachiavelliFortuneAfterAction action in ConfirmedActions)
        {
            if (replay.Closed || !MachiavelliFortuneAfterFlow.Options(replay.Stage).Contains(action)) return false;
            MachiavelliFortuneAfterFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && FixedSlotResolved == replay.FixedSlotResolved
            && Confirmed == replay.Confirmed && Closed == replay.Closed;
    }
}

internal static class MachiavelliFortuneAfterFlow
{
    public static IReadOnlyList<MachiavelliFortuneAfterAction> Options(MachiavelliFortuneAfterStage stage) => stage switch
    {
        MachiavelliFortuneAfterStage.Invitation =>
            [MachiavelliFortuneAfterAction.KeepPractice, MachiavelliFortuneAfterAction.LetFortuneChange],
        MachiavelliFortuneAfterStage.ResultConfirmed or MachiavelliFortuneAfterStage.ResultKept =>
            [MachiavelliFortuneAfterAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(MachiavelliFortuneAfterState state, MachiavelliFortuneAfterAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != MachiavelliFortuneAfterAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(MachiavelliFortuneAfterState state, MachiavelliFortuneAfterAction action)
    {
        switch (action)
        {
            case MachiavelliFortuneAfterAction.KeepPractice:
                state.Confirmed = true;
                state.FixedSlotResolved = true;
                state.Stage = MachiavelliFortuneAfterStage.ResultConfirmed;
                break;
            case MachiavelliFortuneAfterAction.LetFortuneChange:
                state.Confirmed = false;
                state.FixedSlotResolved = true;
                state.Stage = MachiavelliFortuneAfterStage.ResultKept;
                break;
            case MachiavelliFortuneAfterAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = MachiavelliFortuneAfterAction.None;
    }

    public static bool IsRouteConsistent(MachiavelliFortuneAfterState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || journey.RouteId != MachiavelliFortuneAfterState.RouteId
            || journey.CurrentNodeId != MachiavelliFortuneAfterState.SourceNodeId
            || !journey.CompletedEdgeIds.Contains(MachiavelliFortuneAfterState.RequiredEdgeId)
            || journey.CompletedEdgeIds.Contains("WESTERN_EDGE_040")) return false;
        if (!state.FixedSlotResolved)
            return !state.Closed && journey.LastFixedAct is 2 or 3
                && !journey.DeclinedFixedActs.Contains(3);
        if (journey.LastFixedAct != 3) return false;
        return state.Confirmed
            ? !journey.DeclinedFixedActs.Contains(3)
            : journey.DeclinedFixedActs.Contains(3);
    }

    public static bool TryCommitPending(MachiavelliFortuneAfterState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.Closed
            || state.PendingAction == MachiavelliFortuneAfterAction.None) return false;
        MachiavelliFortuneAfterAction action = state.PendingAction;
        bool committed = action switch
        {
            MachiavelliFortuneAfterAction.KeepPractice => graph.TryConfirmFixedSlot(journey, 3),
            MachiavelliFortuneAfterAction.LetFortuneChange => graph.TryDecline(journey, "Fixed", 3),
            MachiavelliFortuneAfterAction.Leave => true,
            _ => false,
        };
        if (!committed) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static string StageKey(MachiavelliFortuneAfterStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(MachiavelliFortuneAfterAction action) => UpperSnake(action.ToString());
    public static string DescriptionKey(MachiavelliFortuneAfterState state) => state.Stage switch
    {
        MachiavelliFortuneAfterStage.Invitation => "INVITATION",
        MachiavelliFortuneAfterStage.ResultConfirmed => "RESULT_CONFIRMED",
        MachiavelliFortuneAfterStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
