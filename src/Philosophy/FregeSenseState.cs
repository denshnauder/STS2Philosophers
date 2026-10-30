using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum FregeSenseStage { Invitation, ResultSense, ResultScript, ResultKept }
internal enum FregeSenseAction { None, SenseAndReference, ConceptScript, KeepAristotle, Leave }
internal enum FregeSenseStandard { None, Sense, Script, Aristotelian }

/// <summary>
/// Kierkegaard's first-act room on the self-and-other line. The player either
/// takes the leap of faith, keeps the ethical universal, or stays with Socrates
/// and gives up the act's fixed slot.
/// </summary>
internal sealed class FregeSenseState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ARISTOTLE__LANGUAGE_AND_MEANING__CORE";
    public const string AdoptedNodeId = "GOTTLOB_FREGE__LANGUAGE_AND_MEANING__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_077";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public FregeSenseStage Stage { get; set; }
    public FregeSenseAction PendingAction { get; set; }
    public List<FregeSenseAction> ConfirmedActions { get; set; } = [];
    public FregeSenseStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public static FregeSenseState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != FregeSenseAction.None
            || PendingAction != FregeSenseAction.None
                && !FregeSenseFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (FregeSenseAction action in ConfirmedActions)
        {
            if (replay.Closed || !FregeSenseFlow.Options(replay.Stage).Contains(action)) return false;
            FregeSenseFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class FregeSenseFlow
{
    public static IReadOnlyList<FregeSenseAction> Options(FregeSenseStage stage) => stage switch
    {
        FregeSenseStage.Invitation =>
            [FregeSenseAction.SenseAndReference, FregeSenseAction.ConceptScript,
                FregeSenseAction.KeepAristotle],
        FregeSenseStage.ResultSense or FregeSenseStage.ResultScript
            or FregeSenseStage.ResultKept => [FregeSenseAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(FregeSenseState state, FregeSenseAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != FregeSenseAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(FregeSenseState state, FregeSenseAction action)
    {
        switch (action)
        {
            case FregeSenseAction.SenseAndReference:
                state.Standard = FregeSenseStandard.Sense;
                state.EdgeCommitted = true;
                state.Stage = FregeSenseStage.ResultSense;
                break;
            case FregeSenseAction.ConceptScript:
                state.Standard = FregeSenseStandard.Script;
                state.EdgeCommitted = true;
                state.Stage = FregeSenseStage.ResultScript;
                break;
            case FregeSenseAction.KeepAristotle:
                state.Standard = FregeSenseStandard.Aristotelian;
                state.FixedDeclined = true;
                state.Stage = FregeSenseStage.ResultKept;
                break;
            case FregeSenseAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = FregeSenseAction.None;
    }

    public static bool TryCommitPending(FregeSenseState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == FregeSenseAction.None) return false;
        FregeSenseAction action = state.PendingAction;
        bool adopts = action is FregeSenseAction.SenseAndReference or FregeSenseAction.ConceptScript;
        bool declines = action == FregeSenseAction.KeepAristotle;
        if (adopts && !journey.CompletedEdgeIds.Contains(FregeSenseState.CompletionEdgeId)
            && !graph.TryApply(journey, FregeSenseState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(2)
            && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(FregeSenseState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(FregeSenseState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct == 1 && !adopted && !journey.DeclinedFixedActs.Contains(2)
                && journey.CurrentNodeId == FregeSenseState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == 2
                && journey.CurrentNodeId == FregeSenseState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 2 && journey.DeclinedFixedActs.Contains(2)
            && journey.CurrentNodeId == FregeSenseState.SourceNodeId;
    }

    public static string DescriptionKey(FregeSenseState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(FregeSenseStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(FregeSenseAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
