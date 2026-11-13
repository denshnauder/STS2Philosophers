using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum RousseauConsentStage { Invitation, Convention, ResultUniversal, ResultPersonal, ResultImposed, ResultKept }
internal enum RousseauConsentAction { None, InspectConvention, UniversalException, KeepPersonal, Impose, KeepAugustine, Leave }

internal sealed class RousseauConsentState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "AUGUSTINE_OF_HIPPO__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string AdoptedNodeId = "ROUSSEAU__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_020";
    public const string CompletionEdgeId = "WESTERN_EDGE_021";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public RousseauConsentStage Stage { get; set; }
    public RousseauConsentAction PendingAction { get; set; }
    public List<RousseauConsentAction> ConfirmedActions { get; set; } = [];
    public bool ConventionKnown { get; set; }
    public bool ExcludedCompanion { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static RousseauConsentState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != RousseauConsentAction.None
            || PendingAction != RousseauConsentAction.None
                && !RousseauConsentFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = Create(InstanceId);
        foreach (RousseauConsentAction action in ConfirmedActions)
        {
            if (replay.Closed || !RousseauConsentFlow.Options(replay.Stage).Contains(action)) return false;
            RousseauConsentFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && ConventionKnown == replay.ConventionKnown
            && ExcludedCompanion == replay.ExcludedCompanion && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class RousseauConsentFlow
{
    public static IReadOnlyList<RousseauConsentAction> Options(RousseauConsentStage stage) => stage switch
    {
        RousseauConsentStage.Invitation => [RousseauConsentAction.InspectConvention, RousseauConsentAction.KeepAugustine],
        RousseauConsentStage.Convention => [RousseauConsentAction.UniversalException, RousseauConsentAction.KeepPersonal, RousseauConsentAction.Impose],
        RousseauConsentStage.ResultUniversal or RousseauConsentStage.ResultPersonal
            or RousseauConsentStage.ResultImposed or RousseauConsentStage.ResultKept =>
            [RousseauConsentAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(RousseauConsentState state, RousseauConsentAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != RousseauConsentAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(RousseauConsentState state, RousseauConsentAction action)
    {
        switch (action)
        {
            case RousseauConsentAction.InspectConvention:
                state.ConventionKnown = false;
                state.ExcludedCompanion = true;
                state.Stage = RousseauConsentStage.Convention;
                break;
            case RousseauConsentAction.UniversalException:
                state.EdgeCommitted = true;
                state.Stage = RousseauConsentStage.ResultUniversal;
                break;
            case RousseauConsentAction.KeepPersonal:
                state.EdgeCommitted = true;
                state.Stage = RousseauConsentStage.ResultPersonal;
                break;
            case RousseauConsentAction.Impose:
                state.EdgeCommitted = true;
                state.Stage = RousseauConsentStage.ResultImposed;
                break;
            case RousseauConsentAction.KeepAugustine:
                state.QuestionDeclined = true;
                state.Stage = RousseauConsentStage.ResultKept;
                break;
            case RousseauConsentAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = RousseauConsentAction.None;
    }

    public static bool TryCommitPending(RousseauConsentState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == RousseauConsentAction.None) return false;
        RousseauConsentAction action = state.PendingAction;
        bool adopts = action is RousseauConsentAction.UniversalException
            or RousseauConsentAction.KeepPersonal or RousseauConsentAction.Impose;
        bool declines = action == RousseauConsentAction.KeepAugustine;
        if (adopts && !journey.CompletedEdgeIds.Contains(RousseauConsentState.CompletionEdgeId)
            && !graph.TryApply(journey, RousseauConsentState.CompletionEdgeId)) return false;
        if (declines && !journey.ResolvedQuestionActs.Contains(2)
            && !graph.TryDecline(journey, "Question", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(RousseauConsentState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || !journey.CompletedEdgeIds.Contains(RousseauConsentState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(RousseauConsentState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct is 2 or 3 && !adopted
                && !journey.ResolvedQuestionActs.Contains(2)
                && journey.CurrentNodeId == RousseauConsentState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct is 2 or 3
                && journey.ResolvedQuestionActs.Contains(2)
                && journey.CurrentNodeId == RousseauConsentState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct is 2 or 3
            && journey.CurrentNodeId == RousseauConsentState.SourceNodeId
            && journey.ResolvedQuestionActs.Contains(2);
    }

    public static string DescriptionKey(RousseauConsentState state) => state.Stage switch
    {
        RousseauConsentStage.Invitation => "INVITATION",
        RousseauConsentStage.Convention => "CONVENTION",
        RousseauConsentStage.ResultUniversal => "RESULT_UNIVERSAL",
        RousseauConsentStage.ResultPersonal => "RESULT_PERSONAL",
        RousseauConsentStage.ResultImposed => "RESULT_IMPOSED",
        RousseauConsentStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(RousseauConsentStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(RousseauConsentAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
