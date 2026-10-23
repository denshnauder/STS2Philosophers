using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum GalileoMeasuredPracticeStage
{
    Invitation, FirstMeasure, SecondShort, SecondLong, Compare, Calibrated, Decision,
    ResultAdopted, ResultRaw, ResultKept, ResultDeclined,
}
internal enum GalileoMeasuredPracticeAction
{
    None, AcceptInvitation, DeclineInvitation, ShortFirst, LongFirst, TakeRemaining,
    CheckClock, PublishRaw, RepeatCorrected, AdoptProtocol, KeepOld, Leave,
}
internal enum GalileoMeasuredSegment { Short, Long }

internal sealed record GalileoMeasuredReading
{
    public GalileoMeasuredSegment Segment { get; set; } = (GalileoMeasuredSegment)(-1);
    public int Distance { get; set; }
    public int RawTicks { get; set; }
    public int? CorrectedTicks { get; set; }
}

internal sealed class GalileoMeasuredPracticeState : IJsonOnDeserialized
{
    private bool? _closed;
    private bool? _adopted;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string DemocritusInstanceId { get; set; } = string.Empty;
    public string EmpedoclesInstanceId { get; set; } = string.Empty;
    public GalileoMeasuredPracticeStage Stage { get; set; } = (GalileoMeasuredPracticeStage)(-1);
    public GalileoMeasuredPracticeAction PendingAction { get; set; } = (GalileoMeasuredPracticeAction)(-1);
    public List<GalileoMeasuredPracticeAction> ConfirmedActions { get; set; } = null!;
    public List<GalileoMeasuredReading> Measurements { get; set; } = null!;
    public int? StationaryTicks { get; set; }
    public bool Adopted { get => _adopted ?? false; set => _adopted = value; }
    public bool Closed { get => _closed ?? false; set => _closed = value; }

    public static GalileoMeasuredPracticeState Create(string id, string democritusId, string empedoclesId) => new()
    {
        Version = 1, InstanceId = id, DemocritusInstanceId = democritusId,
        EmpedoclesInstanceId = empedoclesId, Stage = GalileoMeasuredPracticeStage.Invitation,
        PendingAction = GalileoMeasuredPracticeAction.None, ConfirmedActions = [], Measurements = [],
        Adopted = false, Closed = false,
    };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty; DemocritusInstanceId ??= string.Empty; EmpedoclesInstanceId ??= string.Empty;
        // A later save must not turn missing false-valued fields into a fresh grant.
        if (!_closed.HasValue || !_adopted.HasValue) Version = 0;
    }
    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(DemocritusInstanceId)
            || string.IsNullOrWhiteSpace(EmpedoclesInstanceId) || !_closed.HasValue || !_adopted.HasValue
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || ConfirmedActions is null
            || ConfirmedActions.Count > 7 || Measurements is null || Measurements.Count > 2
            || Measurements.Any(reading => reading is null)) return false;
        var replay = Create(InstanceId, DemocritusInstanceId, EmpedoclesInstanceId);
        foreach (var action in ConfirmedActions)
        {
            if (replay.Closed || !GalileoMeasuredPracticeFlow.Options(replay.Stage).Contains(action)) return false;
            GalileoMeasuredPracticeFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Adopted == replay.Adopted && Closed == replay.Closed
            && StationaryTicks == replay.StationaryTicks && Measurements.SequenceEqual(replay.Measurements)
            && (PendingAction == GalileoMeasuredPracticeAction.None
                || !Closed && GalileoMeasuredPracticeFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class GalileoMeasuredPracticeFlow
{
    public static IReadOnlyList<GalileoMeasuredPracticeAction> Options(GalileoMeasuredPracticeStage stage) => stage switch
    {
        GalileoMeasuredPracticeStage.Invitation => [GalileoMeasuredPracticeAction.AcceptInvitation, GalileoMeasuredPracticeAction.DeclineInvitation],
        GalileoMeasuredPracticeStage.FirstMeasure => [GalileoMeasuredPracticeAction.ShortFirst, GalileoMeasuredPracticeAction.LongFirst],
        GalileoMeasuredPracticeStage.SecondShort or GalileoMeasuredPracticeStage.SecondLong => [GalileoMeasuredPracticeAction.TakeRemaining],
        GalileoMeasuredPracticeStage.Compare => [GalileoMeasuredPracticeAction.CheckClock, GalileoMeasuredPracticeAction.PublishRaw],
        GalileoMeasuredPracticeStage.Calibrated => [GalileoMeasuredPracticeAction.RepeatCorrected],
        GalileoMeasuredPracticeStage.Decision => [GalileoMeasuredPracticeAction.AdoptProtocol, GalileoMeasuredPracticeAction.KeepOld],
        >= GalileoMeasuredPracticeStage.ResultAdopted and <= GalileoMeasuredPracticeStage.ResultDeclined => [GalileoMeasuredPracticeAction.Leave],
        _ => [],
    };

    private static GalileoMeasuredReading Read(GalileoMeasuredSegment segment) => new()
    {
        Segment = segment, Distance = segment == GalileoMeasuredSegment.Short ? 1 : 4,
        RawTicks = segment == GalileoMeasuredSegment.Short ? 3 : 5,
    };

    internal static void Apply(GalileoMeasuredPracticeState state, GalileoMeasuredPracticeAction action)
    {
        switch (action)
        {
            case GalileoMeasuredPracticeAction.AcceptInvitation: state.Stage = GalileoMeasuredPracticeStage.FirstMeasure; break;
            case GalileoMeasuredPracticeAction.DeclineInvitation: state.Stage = GalileoMeasuredPracticeStage.ResultDeclined; break;
            case GalileoMeasuredPracticeAction.ShortFirst:
                state.Measurements.Add(Read(GalileoMeasuredSegment.Short)); state.Stage = GalileoMeasuredPracticeStage.SecondLong; break;
            case GalileoMeasuredPracticeAction.LongFirst:
                state.Measurements.Add(Read(GalileoMeasuredSegment.Long)); state.Stage = GalileoMeasuredPracticeStage.SecondShort; break;
            case GalileoMeasuredPracticeAction.TakeRemaining:
                state.Measurements.Add(Read(state.Stage == GalileoMeasuredPracticeStage.SecondShort
                    ? GalileoMeasuredSegment.Short : GalileoMeasuredSegment.Long));
                state.Stage = GalileoMeasuredPracticeStage.Compare; break;
            case GalileoMeasuredPracticeAction.CheckClock:
                state.StationaryTicks = 1; state.Stage = GalileoMeasuredPracticeStage.Calibrated; break;
            case GalileoMeasuredPracticeAction.RepeatCorrected:
                foreach (var reading in state.Measurements) reading.CorrectedTicks = reading.RawTicks - 1;
                state.Stage = GalileoMeasuredPracticeStage.Decision; break;
            case GalileoMeasuredPracticeAction.PublishRaw: state.Stage = GalileoMeasuredPracticeStage.ResultRaw; break;
            case GalileoMeasuredPracticeAction.AdoptProtocol:
                state.Adopted = true; state.Stage = GalileoMeasuredPracticeStage.ResultAdopted; break;
            case GalileoMeasuredPracticeAction.KeepOld: state.Stage = GalileoMeasuredPracticeStage.ResultKept; break;
            case GalileoMeasuredPracticeAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action); state.PendingAction = GalileoMeasuredPracticeAction.None;
    }

    public static bool IsRouteConsistent(GalileoMeasuredPracticeState state, WesternJourneyState journey,
        DemocritusArrangementState democritus, EmpedoclesRootsInquiryState empedocles)
    {
        if (!state.IsUsable() || !democritus.IsUsable() || !democritus.Closed
            || democritus.Stage != DemocritusArrangementStage.ResultAdopted
            || !empedocles.IsUsable() || !empedocles.Closed
            || !empedocles.ConfirmedActions.Contains(EmpedoclesRootsInquiryAction.AcceptInvitation)
            || state.DemocritusInstanceId != democritus.InstanceId || state.EmpedoclesInstanceId != empedocles.InstanceId
            || !journey.CompletedEdgeIds.Contains(DemocritusArrangementState.PrerequisiteEdgeId)) return false;
        var graph = WesternRouteGraph.LoadEmbedded();
        return DemocritusArrangementFlow.IsJourneyConsistent(democritus, journey, graph)
            && EmpedoclesRootsInquiryFlow.IsJourneyConsistent(empedocles, journey, graph);
    }

    public static bool TryPrepare(GalileoMeasuredPracticeState state, GalileoMeasuredPracticeAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != GalileoMeasuredPracticeAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action; return true;
    }

    public static bool TryCommitPending(GalileoMeasuredPracticeState state, WesternJourneyState journey,
        DemocritusArrangementState democritus, EmpedoclesRootsInquiryState empedocles)
    {
        if (!IsRouteConsistent(state, journey, democritus, empedocles) || state.Closed
            || state.PendingAction == GalileoMeasuredPracticeAction.None) return false;
        Apply(state, state.PendingAction);
        return IsRouteConsistent(state, journey, democritus, empedocles);
    }

    public static string StageKey(GalileoMeasuredPracticeStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(GalileoMeasuredPracticeAction action) => UpperSnake(action.ToString());
    public static string DescriptionKey(GalileoMeasuredPracticeState state) =>
        state.Stage == GalileoMeasuredPracticeStage.ResultAdopted && state.Closed
            ? "RESULT_ADOPTED_CONFIRMED" : StageKey(state.Stage);
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
