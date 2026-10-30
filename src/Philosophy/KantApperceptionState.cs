using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum KantApperceptionStage { Invitation, Evidence, ResultConstitutive, ResultRegulative, ResultKept }
internal enum KantApperceptionAction { None, InspectLedger, Constitutive, Regulative, KeepHume, Leave }
internal enum KantUnityJudgment { None, Constitutive, Regulative }

internal sealed class KantApperceptionState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "DAVID_HUME__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "IMMANUEL_KANT__SELF_AND_OTHER__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_027";
    public const string SecondActArrivalEdgeId = "WESTERN_EDGE_093";

    /// <summary>Hume is reached on the default line or on the act-one question branch.</summary>
    public static bool HasHumeArrival(WesternJourneyState journey) =>
        journey.CompletedEdgeIds.Contains(RequiredEdgeId)
        || journey.CompletedEdgeIds.Contains(SecondActArrivalEdgeId)
        || journey.CompletedEdgeIds.Contains(RouteArrivalEdgeId);
    public const string CompletionEdgeId = "WESTERN_EDGE_028";

    /// <summary>The route that places this room in act two arrives from Descartes.</summary>
    public const string RouteArrivalEdgeId = "WESTERN_EDGE_095";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public KantApperceptionStage Stage { get; set; }
    public KantApperceptionAction PendingAction { get; set; }
    public List<KantApperceptionAction> ConfirmedActions { get; set; } = [];
    public bool EvidenceChecked { get; set; }
    public KantUnityJudgment Judgment { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 3;
    public string EdgeId { get; set; } = CompletionEdgeId;

    public static KantApperceptionState Create(string instanceId) => CreateAt(instanceId, 3, CompletionEdgeId);

    public static KantApperceptionState CreateAt(string instanceId, int actIndex, string edgeId) =>
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
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Judgment)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != KantApperceptionAction.None
            || PendingAction != KantApperceptionAction.None
                && !KantApperceptionFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (KantApperceptionAction action in ConfirmedActions)
        {
            if (replay.Closed || !KantApperceptionFlow.Options(replay.Stage).Contains(action)) return false;
            KantApperceptionFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && EvidenceChecked == replay.EvidenceChecked
            && Judgment == replay.Judgment && EdgeCommitted == replay.EdgeCommitted
            && FixedDeclined == replay.FixedDeclined && Closed == replay.Closed;
    }
}

internal static class KantApperceptionFlow
{
    public static IReadOnlyList<KantApperceptionAction> Options(KantApperceptionStage stage) => stage switch
    {
        KantApperceptionStage.Invitation => [KantApperceptionAction.InspectLedger, KantApperceptionAction.KeepHume],
        KantApperceptionStage.Evidence => [KantApperceptionAction.Constitutive, KantApperceptionAction.Regulative],
        KantApperceptionStage.ResultConstitutive or KantApperceptionStage.ResultRegulative or KantApperceptionStage.ResultKept =>
            [KantApperceptionAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(KantApperceptionState state, KantApperceptionAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != KantApperceptionAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(KantApperceptionState state, KantApperceptionAction action)
    {
        switch (action)
        {
            case KantApperceptionAction.InspectLedger:
                state.EvidenceChecked = true;
                state.Stage = KantApperceptionStage.Evidence;
                break;
            case KantApperceptionAction.Constitutive:
                state.Judgment = KantUnityJudgment.Constitutive;
                state.EdgeCommitted = true;
                state.Stage = KantApperceptionStage.ResultConstitutive;
                break;
            case KantApperceptionAction.Regulative:
                state.Judgment = KantUnityJudgment.Regulative;
                state.EdgeCommitted = true;
                state.Stage = KantApperceptionStage.ResultRegulative;
                break;
            case KantApperceptionAction.KeepHume:
                state.FixedDeclined = true;
                state.Stage = KantApperceptionStage.ResultKept;
                break;
            case KantApperceptionAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = KantApperceptionAction.None;
    }

    public static bool TryCommitPending(KantApperceptionState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == KantApperceptionAction.None) return false;
        KantApperceptionAction action = state.PendingAction;
        bool adopts = action is KantApperceptionAction.Constitutive or KantApperceptionAction.Regulative;
        bool declines = action == KantApperceptionAction.KeepHume;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        int declineAct = Math.Min(3, state.ActIndex + 1);
        if (declines && !journey.DeclinedFixedActs.Contains(declineAct)
            && !graph.TryDecline(journey, "Fixed", declineAct)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(KantApperceptionState state, WesternJourneyState journey)
    {
        int act = state.ActIndex;
        bool routeArrival = act == 2 && journey.CompletedEdgeIds.Contains(KantApperceptionState.RouteArrivalEdgeId);
        if (!state.IsUsable() || journey.RetainedEnding
            || act == 3 && !KantApperceptionState.HasHumeArrival(journey)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        int declined = Math.Min(3, act + 1);
        // Act three opens once act two's fixed event has been taken (position two)
        // and stays open through its own act; act two opens in place.
        int opens = act == 3 ? 2 : act;
        bool atSource = journey.CurrentNodeId == KantApperceptionState.SourceNodeId || routeArrival;
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct >= opens && !adopted
                && !journey.DeclinedFixedActs.Contains(declined) && atSource;
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct >= opens
                && journey.CurrentNodeId == KantApperceptionState.AdoptedNodeId;
        return !adopted && journey.LastFixedAct >= opens && atSource
            && journey.DeclinedFixedActs.Contains(declined);
    }

    public static string DescriptionKey(KantApperceptionState state) => state.Stage switch
    {
        KantApperceptionStage.Invitation => "INVITATION",
        KantApperceptionStage.Evidence => "EVIDENCE",
        KantApperceptionStage.ResultConstitutive => "RESULT_CONSTITUTIVE",
        KantApperceptionStage.ResultRegulative => "RESULT_REGULATIVE",
        KantApperceptionStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(KantApperceptionStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(KantApperceptionAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
