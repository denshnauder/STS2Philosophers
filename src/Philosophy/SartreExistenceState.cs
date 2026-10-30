using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum SartreExistenceStage { Invitation, ResultExistence, ResultFreedom, ResultKept }
internal enum SartreExistenceAction { None, ExistencePrecedesEssence, FreedomInSituation, KeepKierkegaard, Leave }
internal enum SartreExistenceStandard { None, Existence, Freedom, Kierkegaardian }

/// <summary>
/// Kierkegaard's first-act room on the self-and-other line. The player either
/// takes the leap of faith, keeps the ethical universal, or stays with Socrates
/// and gives up the act's fixed slot.
/// </summary>
internal sealed class SartreExistenceState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "SOREN_KIERKEGAARD__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "JEAN_PAUL_SARTRE__SELF_AND_OTHER__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_060";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public SartreExistenceStage Stage { get; set; }
    public SartreExistenceAction PendingAction { get; set; }
    public List<SartreExistenceAction> ConfirmedActions { get; set; } = [];
    public SartreExistenceStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static SartreExistenceState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != SartreExistenceAction.None
            || PendingAction != SartreExistenceAction.None
                && !SartreExistenceFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (SartreExistenceAction action in ConfirmedActions)
        {
            if (replay.Closed || !SartreExistenceFlow.Options(replay.Stage).Contains(action)) return false;
            SartreExistenceFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class SartreExistenceFlow
{
    public static IReadOnlyList<SartreExistenceAction> Options(SartreExistenceStage stage) => stage switch
    {
        SartreExistenceStage.Invitation =>
            [SartreExistenceAction.ExistencePrecedesEssence, SartreExistenceAction.FreedomInSituation,
                SartreExistenceAction.KeepKierkegaard],
        SartreExistenceStage.ResultExistence or SartreExistenceStage.ResultFreedom
            or SartreExistenceStage.ResultKept => [SartreExistenceAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(SartreExistenceState state, SartreExistenceAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != SartreExistenceAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(SartreExistenceState state, SartreExistenceAction action)
    {
        switch (action)
        {
            case SartreExistenceAction.ExistencePrecedesEssence:
                state.Standard = SartreExistenceStandard.Existence;
                state.EdgeCommitted = true;
                state.Stage = SartreExistenceStage.ResultExistence;
                break;
            case SartreExistenceAction.FreedomInSituation:
                state.Standard = SartreExistenceStandard.Freedom;
                state.EdgeCommitted = true;
                state.Stage = SartreExistenceStage.ResultFreedom;
                break;
            case SartreExistenceAction.KeepKierkegaard:
                state.Standard = SartreExistenceStandard.Kierkegaardian;
                state.FixedDeclined = true;
                state.Stage = SartreExistenceStage.ResultKept;
                break;
            case SartreExistenceAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = SartreExistenceAction.None;
    }

    public static bool TryCommitPending(SartreExistenceState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == SartreExistenceAction.None) return false;
        SartreExistenceAction action = state.PendingAction;
        bool adopts = action is SartreExistenceAction.ExistencePrecedesEssence or SartreExistenceAction.FreedomInSituation;
        bool declines = action == SartreExistenceAction.KeepKierkegaard;
        if (adopts && !journey.CompletedEdgeIds.Contains(SartreExistenceState.CompletionEdgeId)
            && !graph.TryApply(journey, SartreExistenceState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(3)
            && !graph.TryDecline(journey, "Fixed", 3)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(SartreExistenceState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(SartreExistenceState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 2 && !adopted && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == SartreExistenceState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 3
                && journey.CurrentNodeId == SartreExistenceState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 3 && journey.DeclinedFixedActs.Contains(3)
            && journey.CurrentNodeId == SartreExistenceState.SourceNodeId;
    }

    public static string DescriptionKey(SartreExistenceState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(SartreExistenceStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(SartreExistenceAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
