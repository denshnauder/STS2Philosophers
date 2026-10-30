using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum KantUniversalizationStage { Invitation, Maxim, ResultUniversal, ResultEmergency, ResultPrivate, ResultKept }
internal enum KantUniversalizationAction { None, StateMaxim, UniversalRule, PublicEmergencyException, PrivateException, KeepRousseau, Leave }
internal enum KantMaxim { None, Universal, EmergencyOnly, SelfOnly }
internal enum KantUniversalizationLoss { None, Exemption, VerificationSlot }

internal sealed class KantUniversalizationState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;
    public const string SourceNodeId = "ROUSSEAU__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string AdoptedNodeId = "IMMANUEL_KANT__FREEDOM_AND_INSTITUTIONS__CORE";
    public const string RequiredEdgeId = "WESTERN_EDGE_021";
    public const string CompletionEdgeId = "WESTERN_EDGE_022";

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public KantUniversalizationStage Stage { get; set; }
    public KantUniversalizationAction PendingAction { get; set; }
    public List<KantUniversalizationAction> ConfirmedActions { get; set; } = [];
    public KantMaxim Maxim { get; set; }
    public KantUniversalizationLoss Loss { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool FixedDeclined { get; set; }
    public bool Closed { get; set; }

    public int ActIndex { get; set; } = 3;
    public string EdgeId { get; set; } = CompletionEdgeId;
    public bool RoutePlaced { get; set; }
    public string RouteSourceNodeId { get; set; } = string.Empty;

    public static KantUniversalizationState Create(string instanceId) => CreateAt(instanceId, 3, CompletionEdgeId);

    public static KantUniversalizationState CreateAt(string instanceId, int actIndex, string edgeId) =>
        new() { InstanceId = instanceId, ActIndex = actIndex, EdgeId = edgeId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        EdgeId ??= CompletionEdgeId;
        RouteSourceNodeId ??= string.Empty;
        if (ActIndex is < 1 or > 3) ActIndex = 3;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Maxim) || !Enum.IsDefined(Loss)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != KantUniversalizationAction.None
            || PendingAction != KantUniversalizationAction.None
                && !KantUniversalizationFlow.Options(Stage).Contains(PendingAction)) return false;

        var replay = CreateAt(InstanceId, ActIndex, EdgeId);
        foreach (KantUniversalizationAction action in ConfirmedActions)
        {
            if (replay.Closed || !KantUniversalizationFlow.Options(replay.Stage).Contains(action)) return false;
            KantUniversalizationFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Maxim == replay.Maxim && Loss == replay.Loss
            && EdgeCommitted == replay.EdgeCommitted && FixedDeclined == replay.FixedDeclined
            && Closed == replay.Closed;
    }
}

internal static class KantUniversalizationFlow
{
    public static IReadOnlyList<KantUniversalizationAction> Options(KantUniversalizationStage stage) => stage switch
    {
        KantUniversalizationStage.Invitation => [KantUniversalizationAction.StateMaxim, KantUniversalizationAction.KeepRousseau],
        KantUniversalizationStage.Maxim => [KantUniversalizationAction.UniversalRule, KantUniversalizationAction.PublicEmergencyException, KantUniversalizationAction.PrivateException],
        KantUniversalizationStage.ResultUniversal or KantUniversalizationStage.ResultEmergency
            or KantUniversalizationStage.ResultPrivate or KantUniversalizationStage.ResultKept =>
            [KantUniversalizationAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(KantUniversalizationState state, KantUniversalizationAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != KantUniversalizationAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(KantUniversalizationState state, KantUniversalizationAction action)
    {
        switch (action)
        {
            case KantUniversalizationAction.StateMaxim:
                state.Stage = KantUniversalizationStage.Maxim;
                break;
            case KantUniversalizationAction.UniversalRule:
                state.Maxim = KantMaxim.Universal;
                state.Loss = KantUniversalizationLoss.Exemption;
                state.EdgeCommitted = true;
                state.Stage = KantUniversalizationStage.ResultUniversal;
                break;
            case KantUniversalizationAction.PublicEmergencyException:
                state.Maxim = KantMaxim.EmergencyOnly;
                state.Loss = KantUniversalizationLoss.VerificationSlot;
                state.EdgeCommitted = true;
                state.Stage = KantUniversalizationStage.ResultEmergency;
                break;
            case KantUniversalizationAction.PrivateException:
                state.Maxim = KantMaxim.SelfOnly;
                state.Loss = KantUniversalizationLoss.None;
                state.EdgeCommitted = true;
                state.Stage = KantUniversalizationStage.ResultPrivate;
                break;
            case KantUniversalizationAction.KeepRousseau:
                state.FixedDeclined = true;
                state.Stage = KantUniversalizationStage.ResultKept;
                break;
            case KantUniversalizationAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = KantUniversalizationAction.None;
    }

    public static bool TryCommitPending(KantUniversalizationState state, WesternJourneyState journey, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey) || state.PendingAction == KantUniversalizationAction.None) return false;
        KantUniversalizationAction action = state.PendingAction;
        bool adopts = action is KantUniversalizationAction.UniversalRule
            or KantUniversalizationAction.PublicEmergencyException or KantUniversalizationAction.PrivateException;
        bool declines = action == KantUniversalizationAction.KeepRousseau;
        if (adopts && !journey.CompletedEdgeIds.Contains(state.EdgeId)
            && !graph.TryApply(journey, state.EdgeId)) return false;
        if (declines && !journey.DeclinedFixedActs.Contains(state.ActIndex)
            && !graph.TryDecline(journey, "Fixed", state.ActIndex)) return false;
        Apply(state, action);
        return IsRouteConsistent(state, journey);
    }

    public static bool IsRouteConsistent(KantUniversalizationState state, WesternJourneyState journey)
    {
        int act = state.ActIndex;
        bool onRoute = state.RoutePlaced;
        bool atSource = journey.CurrentNodeId == KantUniversalizationState.SourceNodeId
            || state.RouteSourceNodeId.Length > 0 && journey.CurrentNodeId == state.RouteSourceNodeId;
        if (!state.IsUsable() || journey.RetainedEnding
            || !onRoute && !journey.CompletedEdgeIds.Contains(KantUniversalizationState.RequiredEdgeId)) return false;
        bool adopted = journey.CompletedEdgeIds.Contains(state.EdgeId);
        if (!state.EdgeCommitted && !state.FixedDeclined)
            return journey.LastFixedAct >= (onRoute ? act : 2) && !adopted
                && !journey.DeclinedFixedActs.Contains(act)
                && (onRoute || atSource);
        if (state.EdgeCommitted)
            return !state.FixedDeclined && adopted && journey.LastFixedAct == act
                && (onRoute || journey.CurrentNodeId == KantUniversalizationState.AdoptedNodeId);
        return !adopted && journey.LastFixedAct == act
            && (onRoute || atSource)
            && journey.DeclinedFixedActs.Contains(act);
    }

    public static string DescriptionKey(KantUniversalizationState state) => state.Stage switch
    {
        KantUniversalizationStage.Invitation => "INVITATION",
        KantUniversalizationStage.Maxim => "MAXIM",
        KantUniversalizationStage.ResultUniversal => "RESULT_UNIVERSAL",
        KantUniversalizationStage.ResultEmergency => "RESULT_EMERGENCY",
        KantUniversalizationStage.ResultPrivate => "RESULT_PRIVATE",
        KantUniversalizationStage.ResultKept => "RESULT_KEPT",
        _ => "INVITATION",
    };

    public static string StageKey(KantUniversalizationStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(KantUniversalizationAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
