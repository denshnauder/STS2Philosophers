using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum BaconSampleLedgerStage { Invitation, Bias, Counterexample, Decision, ResultLimited, ResultControl, ResultKept, ResultDeclined }
internal enum BaconSampleLedgerAction { None, AcceptInvitation, DeclineInvitation, CheckTribe, CheckCave, CheckMarket, CheckTheatre, TakeCounterSample, PublishLimited, TakeControlSample, KeepPrediction, Leave }
internal enum BaconInquiryBias { None, Tribe, Cave, Market, Theatre }
internal enum BaconReportDisposition { None, Limited, Control, Kept }
internal enum BaconObservedSample { WarmRich, ColdPoor, WarmPoor, NormalPoor }

internal sealed class BaconSampleLedgerState : IJsonOnDeserialized
{
    public const string SourceNodeId = "ARISTOTLE__LANGUAGE_AND_MEANING__CORE";
    public const string AdoptedNodeId = "FRANCIS_BACON__KNOWLEDGE_AND_DOUBT__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_048";
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string OckhamInstanceId { get; set; } = string.Empty;
    public BaconSampleLedgerStage Stage { get; set; } = (BaconSampleLedgerStage)(-1);
    public BaconSampleLedgerAction PendingAction { get; set; } = (BaconSampleLedgerAction)(-1);
    public List<BaconSampleLedgerAction> ConfirmedActions { get; set; } = null!;
    public List<BaconObservedSample> Samples { get; set; } = null!;
    public BaconInquiryBias Bias { get; set; }
    public BaconReportDisposition Disposition { get; set; }
    public bool FixedSlotResolved { get; set; }
    public bool EdgeCommitted { get; set; }
    public bool Inspecting { get; set; }
    public DiderotPracticeState? Diderot { get; set; }
    public bool Closed { get => _closed ?? false; set => _closed = value; }
    public static BaconSampleLedgerState Create(string instanceId, string ockhamInstanceId) => new()
    {
        Version = 1, InstanceId = instanceId, OckhamInstanceId = ockhamInstanceId,
        Stage = BaconSampleLedgerStage.Invitation, PendingAction = BaconSampleLedgerAction.None,
        ConfirmedActions = [], Samples = [BaconObservedSample.WarmRich, BaconObservedSample.ColdPoor], Closed = false,
        Diderot = DiderotPracticeState.Create($"{instanceId}:DIDEROT"),
    };
    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty; OckhamInstanceId ??= string.Empty;
        Diderot?.NormalizeAfterLoad();
        if (!_closed.HasValue) Version = 0;
    }
    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();
    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(OckhamInstanceId)
            || !_closed.HasValue || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || Inspecting && (Diderot is null || Diderot.Closed)
            || Diderot is not null && !Diderot.IsUsable()
            || ConfirmedActions is null || ConfirmedActions.Count > 5 || Samples is null) return false;
        var replay = Create(InstanceId, OckhamInstanceId);
        foreach (var action in ConfirmedActions)
        {
            if (replay.Closed || !BaconSampleLedgerFlow.Options(replay.Stage).Contains(action)) return false;
            BaconSampleLedgerFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Bias == replay.Bias && Disposition == replay.Disposition
            && Samples.SequenceEqual(replay.Samples) && FixedSlotResolved == replay.FixedSlotResolved
            && EdgeCommitted == replay.EdgeCommitted && Closed == replay.Closed
            && (PendingAction == BaconSampleLedgerAction.None
                || !Closed && BaconSampleLedgerFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class BaconSampleLedgerFlow
{
    public static IReadOnlyList<BaconSampleLedgerAction> Options(BaconSampleLedgerStage stage) => stage switch
    {
        BaconSampleLedgerStage.Invitation => [BaconSampleLedgerAction.AcceptInvitation, BaconSampleLedgerAction.DeclineInvitation],
        BaconSampleLedgerStage.Bias => [BaconSampleLedgerAction.CheckTribe, BaconSampleLedgerAction.CheckCave, BaconSampleLedgerAction.CheckMarket, BaconSampleLedgerAction.CheckTheatre],
        BaconSampleLedgerStage.Counterexample => [BaconSampleLedgerAction.TakeCounterSample],
        BaconSampleLedgerStage.Decision => [BaconSampleLedgerAction.PublishLimited, BaconSampleLedgerAction.TakeControlSample, BaconSampleLedgerAction.KeepPrediction],
        >= BaconSampleLedgerStage.ResultLimited and <= BaconSampleLedgerStage.ResultDeclined => [BaconSampleLedgerAction.Leave],
        _ => [],
    };
    internal static void Apply(BaconSampleLedgerState state, BaconSampleLedgerAction action)
    {
        switch (action)
        {
            case BaconSampleLedgerAction.AcceptInvitation: state.Stage = BaconSampleLedgerStage.Bias; break;
            case BaconSampleLedgerAction.DeclineInvitation:
                state.Stage = BaconSampleLedgerStage.ResultDeclined; state.FixedSlotResolved = true; break;
            case BaconSampleLedgerAction.CheckTribe: ChooseBias(state, BaconInquiryBias.Tribe); break;
            case BaconSampleLedgerAction.CheckCave: ChooseBias(state, BaconInquiryBias.Cave); break;
            case BaconSampleLedgerAction.CheckMarket: ChooseBias(state, BaconInquiryBias.Market); break;
            case BaconSampleLedgerAction.CheckTheatre: ChooseBias(state, BaconInquiryBias.Theatre); break;
            case BaconSampleLedgerAction.TakeCounterSample:
                state.Samples.Add(BaconObservedSample.WarmPoor); state.Stage = BaconSampleLedgerStage.Decision; break;
            case BaconSampleLedgerAction.PublishLimited:
                state.Disposition = BaconReportDisposition.Limited; state.Stage = BaconSampleLedgerStage.ResultLimited;
                state.FixedSlotResolved = state.EdgeCommitted = true; break;
            case BaconSampleLedgerAction.TakeControlSample:
                state.Samples.Add(BaconObservedSample.NormalPoor); state.Disposition = BaconReportDisposition.Control;
                state.Stage = BaconSampleLedgerStage.ResultControl; state.FixedSlotResolved = state.EdgeCommitted = true; break;
            case BaconSampleLedgerAction.KeepPrediction:
                state.Disposition = BaconReportDisposition.Kept; state.Stage = BaconSampleLedgerStage.ResultKept;
                state.FixedSlotResolved = true; break;
            case BaconSampleLedgerAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action); state.PendingAction = BaconSampleLedgerAction.None;
    }
    private static void ChooseBias(BaconSampleLedgerState state, BaconInquiryBias bias)
    { state.Bias = bias; state.Stage = BaconSampleLedgerStage.Counterexample; }

    // A read-only projection validates an already completed predecessor at its own
    // historical position. It never rewinds or replaces the actual run journey.
    public static WesternJourneyState OckhamHistoryJourney(WesternJourneyState journey) => new()
    {
        CurrentNodeId = BaconSampleLedgerState.SourceNodeId, LastFixedAct = 2,
        SeenThinkerIds = journey.SeenThinkerIds.Where(id => id != "FRANCIS_BACON").ToHashSet(StringComparer.Ordinal),
        CompletedEdgeIds = journey.CompletedEdgeIds.Where(id => id != BaconSampleLedgerState.CompletionEdgeId).ToHashSet(StringComparer.Ordinal),
        ResolvedQuestionActs = new(journey.ResolvedQuestionActs),
        DeclinedFixedActs = journey.DeclinedFixedActs.Where(act => act != 3).ToHashSet(),
    };
    public static bool IsRouteConsistent(BaconSampleLedgerState state, WesternJourneyState journey,
        OckhamNamesInquiryState ockham, GorgiasSpeechInquiryState gorgias)
    {
        if (!state.IsUsable() || !ockham.IsUsable() || !ockham.Closed || !ockham.QuestionReserved
            || !ockham.SecondFixedDeclined || state.OckhamInstanceId != ockham.InstanceId
            || ockham.Disposition is not (OckhamNamesDisposition.Annotated or OckhamNamesDisposition.Named or OckhamNamesDisposition.Kept)
            || !OckhamNamesInquiryFlow.IsRouteConsistent(ockham, OckhamHistoryJourney(journey), gorgias)
            || journey.ResolvedQuestionActs.Contains(3)) return false;
        bool edge = journey.CompletedEdgeIds.Contains(BaconSampleLedgerState.CompletionEdgeId);
        bool seen = journey.SeenThinkerIds.Contains("FRANCIS_BACON");
        if (!state.FixedSlotResolved)
            return !edge && !seen && !journey.RetainedEnding && journey.LastFixedAct >= 2
                && journey.CurrentNodeId == BaconSampleLedgerState.SourceNodeId && !journey.DeclinedFixedActs.Contains(3);
        return journey.LastFixedAct == 3 && (state.EdgeCommitted
            ? edge && seen && !journey.RetainedEnding && !journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == BaconSampleLedgerState.AdoptedNodeId
            : !edge && !seen && journey.RetainedEnding && journey.DeclinedFixedActs.Contains(3)
                && journey.CurrentNodeId == BaconSampleLedgerState.SourceNodeId);
    }
    public static bool TryPrepare(BaconSampleLedgerState state, BaconSampleLedgerAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != BaconSampleLedgerAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action; return true;
    }
    public static bool TryCommitPending(BaconSampleLedgerState state, WesternJourneyState journey,
        OckhamNamesInquiryState ockham, GorgiasSpeechInquiryState gorgias, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey, ockham, gorgias) || state.Closed || state.PendingAction == BaconSampleLedgerAction.None) return false;
        var action = state.PendingAction;
        bool adopts = action is BaconSampleLedgerAction.PublishLimited or BaconSampleLedgerAction.TakeControlSample;
        bool declines = action is BaconSampleLedgerAction.DeclineInvitation or BaconSampleLedgerAction.KeepPrediction;
        if (adopts && !graph.TryApply(journey, BaconSampleLedgerState.CompletionEdgeId)
            || declines && (!graph.TryDecline(journey, "Fixed", 3) || !graph.TryRetainEnding(journey))) return false;
        Apply(state, action); return IsRouteConsistent(state, journey, ockham, gorgias);
    }
    public static string StageKey(BaconSampleLedgerStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(BaconSampleLedgerAction action) => UpperSnake(action.ToString());
    public static string DescriptionKey(BaconSampleLedgerState state) =>
        state.Stage is BaconSampleLedgerStage.Counterexample or BaconSampleLedgerStage.Decision
            or BaconSampleLedgerStage.ResultLimited or BaconSampleLedgerStage.ResultControl or BaconSampleLedgerStage.ResultKept
            ? $"{StageKey(state.Stage)}_{UpperSnake(state.Bias.ToString())}" : StageKey(state.Stage);
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((character, index) =>
        index > 0 && char.IsUpper(character) ? new[] { '_', character } : new[] { char.ToUpperInvariant(character) }));
}
