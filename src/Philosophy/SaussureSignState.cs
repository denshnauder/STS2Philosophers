using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum SaussureSignStage { Invitation, ResultLangue, ResultSign, ResultKept }
internal enum SaussureSignAction { None, LanguageParole, SignArbitrary, KeepAristotle, Leave }
internal enum SaussureSignStandard { None, Langue, Sign, Aristotelian }

/// <summary>
/// Saussure's first-act resolution on the language line. Aristotle's language
/// entry is the source; the act's single resolution is a question edge
/// (switch to structural linguistics) or keeping Aristotle.
/// </summary>
internal sealed class SaussureSignState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ARISTOTLE__LANGUAGE_AND_MEANING__CORE";
    public const string AdoptedNodeId = "FERDINAND_DE_SAUSSURE__LANGUAGE_AND_MEANING__CORE";
    public const string RequiredEdgeId = "";
    public const string CompletionEdgeId = "WESTERN_EDGE_080";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public SaussureSignStage Stage { get; set; }
    public SaussureSignAction PendingAction { get; set; }
    public List<SaussureSignAction> ConfirmedActions { get; set; } = [];
    public SaussureSignStandard Standard { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool QuestionDeclined { get; set; }
    public bool Closed { get; set; }

    public static SaussureSignState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != SaussureSignAction.None
            || PendingAction != SaussureSignAction.None
                && !SaussureSignFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (SaussureSignAction action in ConfirmedActions)
        {
            if (replay.Closed || !SaussureSignFlow.Options(replay.Stage).Contains(action)) return false;
            SaussureSignFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && EdgeCommitted == replay.EdgeCommitted
            && QuestionDeclined == replay.QuestionDeclined && Closed == replay.Closed;
    }
}

internal static class SaussureSignFlow
{
    public static IReadOnlyList<SaussureSignAction> Options(SaussureSignStage stage) => stage switch
    {
        SaussureSignStage.Invitation =>
            [SaussureSignAction.LanguageParole, SaussureSignAction.SignArbitrary,
                SaussureSignAction.KeepAristotle],
        SaussureSignStage.ResultLangue or SaussureSignStage.ResultSign
            or SaussureSignStage.ResultKept => [SaussureSignAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(SaussureSignState state, SaussureSignAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != SaussureSignAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(SaussureSignState state, SaussureSignAction action)
    {
        switch (action)
        {
            case SaussureSignAction.LanguageParole:
                state.Standard = SaussureSignStandard.Langue;
                state.EdgeCommitted = true;
                state.Stage = SaussureSignStage.ResultLangue;
                break;
            case SaussureSignAction.SignArbitrary:
                state.Standard = SaussureSignStandard.Sign;
                state.EdgeCommitted = true;
                state.Stage = SaussureSignStage.ResultSign;
                break;
            case SaussureSignAction.KeepAristotle:
                state.Standard = SaussureSignStandard.Aristotelian;
                state.QuestionDeclined = true;
                state.Stage = SaussureSignStage.ResultKept;
                break;
            case SaussureSignAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = SaussureSignAction.None;
    }

    public static bool TryCommitPending(SaussureSignState state, WesternJourneyState journey,
        WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == SaussureSignAction.None) return false;
        SaussureSignAction action = state.PendingAction;
        bool adopts = action is SaussureSignAction.LanguageParole or SaussureSignAction.SignArbitrary;
        bool declines = action == SaussureSignAction.KeepAristotle;
        if (adopts && !journey.CompletedEdgeIds.Contains(SaussureSignState.CompletionEdgeId)
            && !graph.TryApply(journey, SaussureSignState.CompletionEdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(2)
            && !graph.TryDecline(journey, "Fixed", 2)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(SaussureSignState state, WesternJourneyState journey)
    {
        if (!state.IsUsable() || journey.RetainedEnding
            || SaussureSignState.RequiredEdgeId.Length > 0
                && !journey.CompletedEdgeIds.Contains(SaussureSignState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(SaussureSignState.CompletionEdgeId);
        if (!state.EdgeCommitted && !state.QuestionDeclined)
            return journey.LastFixedAct == 1 && !adopted && !journey.DeclinedFixedActs.Contains(2)
                && journey.CurrentNodeId == SaussureSignState.SourceNodeId;
        if (state.EdgeCommitted)
            return !state.QuestionDeclined && adopted && journey.LastFixedAct == 2
                && journey.CurrentNodeId == SaussureSignState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct == 2
            && journey.DeclinedFixedActs.Contains(2)
            && journey.CurrentNodeId == SaussureSignState.SourceNodeId;
    }

    public static string DescriptionKey(SaussureSignState state) => UpperSnake(state.Stage.ToString());

    public static string StageKey(SaussureSignStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(SaussureSignAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
