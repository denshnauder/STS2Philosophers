using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal sealed record CopernicusForkExit([property: JsonRequired] int Row,
    [property: JsonRequired] int Column, [property: JsonRequired] string PointType);
internal enum CopernicusForkSource { LegacyCombat, QuestionPending, QuestionEvent }
internal sealed record CopernicusForkSnapshot([property: JsonRequired] string RunSeed, [property: JsonRequired] int ActIndex,
    [property: JsonRequired] int SourceRow, [property: JsonRequired] int SourceColumn,
    [property: JsonRequired] int SourceRoomId, [property: JsonRequired] int VisitedCount,
    [property: JsonRequired] int HistoryCount, [property: JsonRequired] List<CopernicusForkExit> Exits)
{
    // Missing in version-one saves means the original combat fork; never rebind it.
    public CopernicusForkSource Source { get; init; }
    public bool IsUsable() => Enum.IsDefined(Source) && !string.IsNullOrWhiteSpace(RunSeed) && ActIndex == 1
        && SourceRow >= 0 && SourceColumn >= 0 && SourceRoomId >= 0 && VisitedCount > 0 && HistoryCount > 0
        && (Source != CopernicusForkSource.QuestionPending || SourceRoomId == 0)
        && Exits is { Count: 2 } && Exits.All(e => e is not null && e.Row > SourceRow && e.Column >= 0
            && !string.IsNullOrWhiteSpace(e.PointType) && e.PointType is not ("Boss" or "Unassigned"))
        && (Exits[0].Column < Exits[1].Column || Exits[0].Column == Exits[1].Column && Exits[0].Row < Exits[1].Row);
}
internal enum CopernicusShiftedCenterStage
{
    Invitation, Reference, OrderFirst, OrderAfterObserver, OrderAfterPlanet,
    LastObserver, LastPlanet, LastApparent, ResidualOriginal, ResidualSolar,
    RouteOriginal, RouteSolar, ResultDeclined, ResultWithdrawn, TravelReady,
}
internal enum CopernicusShiftedCenterAction
{
    None, AcceptInvitation, DeclineInvitation, KeepOldTable, RecenterOnSun,
    PlaceObserver, PlacePlanet, PlaceApparent, RecordResidual, Withdraw, FollowTable, Leave,
}
internal enum CopernicusReference { None, Original, Solar }
internal enum CopernicusObservation { Observer, Planet, Apparent }

internal sealed class CopernicusShiftedCenterState : IJsonOnDeserialized
{
    private bool? _closed;
    public int Version { get; set; }
    public string InstanceId { get; set; } = string.Empty;
    public string DemocritusInstanceId { get; set; } = string.Empty;
    public string EmpedoclesInstanceId { get; set; } = string.Empty;
    public CopernicusForkSnapshot Fork { get; set; } = null!;
    public CopernicusShiftedCenterStage Stage { get; set; } = (CopernicusShiftedCenterStage)(-1);
    public CopernicusShiftedCenterAction PendingAction { get; set; } = (CopernicusShiftedCenterAction)(-1);
    public List<CopernicusShiftedCenterAction> ConfirmedActions { get; set; } = null!;
    public CopernicusReference Reference { get; set; }
    public List<CopernicusObservation> Order { get; set; } = null!;
    public bool ResidualRecorded { get; set; }
    public int SelectedExitIndex { get; set; } = -1;
    public bool Closed { get => _closed ?? false; set => _closed = value; }
    // This is an intention. Native coordinates, history and room state prove arrival.
    [JsonIgnore] public bool TravelPending => Closed && SelectedExitIndex >= 0;
    public static CopernicusShiftedCenterState Create(string id, string democritusId, string empedoclesId, CopernicusForkSnapshot fork) => new()
    {
        Version = fork.Source == CopernicusForkSource.LegacyCombat ? 1 : 2, InstanceId = id, DemocritusInstanceId = democritusId, EmpedoclesInstanceId = empedoclesId, Fork = fork,
        Stage = CopernicusShiftedCenterStage.Invitation, PendingAction = CopernicusShiftedCenterAction.None,
        ConfirmedActions = [], Order = [CopernicusObservation.Apparent, CopernicusObservation.Planet, CopernicusObservation.Observer], Closed = false,
    };
    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty; DemocritusInstanceId ??= string.Empty; EmpedoclesInstanceId ??= string.Empty;
        if (!_closed.HasValue) Version = 0;
    }
    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();
    public bool IsUsable()
    {
        if ((Version != 1 || Fork?.Source != CopernicusForkSource.LegacyCombat)
                && (Version != 2 || Fork?.Source is not (CopernicusForkSource.QuestionPending or CopernicusForkSource.QuestionEvent))
            || string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(DemocritusInstanceId)
            || string.IsNullOrWhiteSpace(EmpedoclesInstanceId) || Fork?.IsUsable() != true || !_closed.HasValue
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || ConfirmedActions is null
            || ConfirmedActions.Count > 8 || Order is null
            || Fork.Source == CopernicusForkSource.QuestionPending
                && (ConfirmedActions.Count != 0 || PendingAction != CopernicusShiftedCenterAction.None || Closed)) return false;
        var replay = Create(InstanceId, DemocritusInstanceId, EmpedoclesInstanceId, Fork);
        foreach (var action in ConfirmedActions)
        {
            if (replay.Closed || !CopernicusShiftedCenterFlow.Options(replay.Stage).Contains(action)) return false;
            CopernicusShiftedCenterFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Reference == replay.Reference && Order.SequenceEqual(replay.Order)
            && ResidualRecorded == replay.ResidualRecorded && SelectedExitIndex == replay.SelectedExitIndex && Closed == replay.Closed
            && (PendingAction == CopernicusShiftedCenterAction.None
                || !Closed && CopernicusShiftedCenterFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class CopernicusShiftedCenterFlow
{
    public static IReadOnlyList<CopernicusShiftedCenterAction> Options(CopernicusShiftedCenterStage stage) => stage switch
    {
        CopernicusShiftedCenterStage.Invitation => [CopernicusShiftedCenterAction.AcceptInvitation, CopernicusShiftedCenterAction.DeclineInvitation],
        CopernicusShiftedCenterStage.Reference => [CopernicusShiftedCenterAction.KeepOldTable, CopernicusShiftedCenterAction.RecenterOnSun],
        CopernicusShiftedCenterStage.OrderFirst => [CopernicusShiftedCenterAction.PlaceObserver, CopernicusShiftedCenterAction.PlacePlanet],
        CopernicusShiftedCenterStage.OrderAfterObserver => [CopernicusShiftedCenterAction.PlacePlanet, CopernicusShiftedCenterAction.PlaceApparent],
        CopernicusShiftedCenterStage.OrderAfterPlanet => [CopernicusShiftedCenterAction.PlaceObserver, CopernicusShiftedCenterAction.PlaceApparent],
        CopernicusShiftedCenterStage.LastObserver => [CopernicusShiftedCenterAction.PlaceObserver],
        CopernicusShiftedCenterStage.LastPlanet => [CopernicusShiftedCenterAction.PlacePlanet],
        CopernicusShiftedCenterStage.LastApparent => [CopernicusShiftedCenterAction.PlaceApparent],
        CopernicusShiftedCenterStage.ResidualOriginal or CopernicusShiftedCenterStage.ResidualSolar => [CopernicusShiftedCenterAction.RecordResidual, CopernicusShiftedCenterAction.Withdraw],
        CopernicusShiftedCenterStage.RouteOriginal or CopernicusShiftedCenterStage.RouteSolar => [CopernicusShiftedCenterAction.FollowTable, CopernicusShiftedCenterAction.Withdraw],
        CopernicusShiftedCenterStage.ResultDeclined or CopernicusShiftedCenterStage.ResultWithdrawn => [CopernicusShiftedCenterAction.Leave],
        _ => [],
    };
    internal static void Apply(CopernicusShiftedCenterState state, CopernicusShiftedCenterAction action)
    {
        switch (action)
        {
            case CopernicusShiftedCenterAction.AcceptInvitation: state.Stage = CopernicusShiftedCenterStage.Reference; break;
            case CopernicusShiftedCenterAction.DeclineInvitation: state.Stage = CopernicusShiftedCenterStage.ResultDeclined; break;
            case CopernicusShiftedCenterAction.KeepOldTable:
                state.Reference = CopernicusReference.Original; state.Stage = CopernicusShiftedCenterStage.ResidualOriginal; break;
            case CopernicusShiftedCenterAction.RecenterOnSun:
                state.Reference = CopernicusReference.Solar; state.Order.Clear(); state.Stage = CopernicusShiftedCenterStage.OrderFirst; break;
            case CopernicusShiftedCenterAction.PlaceObserver: Place(state, CopernicusObservation.Observer); break;
            case CopernicusShiftedCenterAction.PlacePlanet: Place(state, CopernicusObservation.Planet); break;
            case CopernicusShiftedCenterAction.PlaceApparent: Place(state, CopernicusObservation.Apparent); break;
            case CopernicusShiftedCenterAction.RecordResidual:
                state.ResidualRecorded = true;
                state.Stage = state.Reference == CopernicusReference.Original ? CopernicusShiftedCenterStage.RouteOriginal : CopernicusShiftedCenterStage.RouteSolar; break;
            case CopernicusShiftedCenterAction.Withdraw: state.Stage = CopernicusShiftedCenterStage.ResultWithdrawn; break;
            case CopernicusShiftedCenterAction.FollowTable:
                state.SelectedExitIndex = state.Reference == CopernicusReference.Original ? 0 : 1;
                state.Stage = CopernicusShiftedCenterStage.TravelReady; state.Closed = true; break;
            case CopernicusShiftedCenterAction.Leave: state.Closed = true; break;
        }
        state.ConfirmedActions.Add(action); state.PendingAction = CopernicusShiftedCenterAction.None;
    }
    private static void Place(CopernicusShiftedCenterState state, CopernicusObservation observation)
    {
        state.Order.Add(observation);
        state.Stage = state.Order.Count switch
        {
            1 => observation == CopernicusObservation.Observer ? CopernicusShiftedCenterStage.OrderAfterObserver : CopernicusShiftedCenterStage.OrderAfterPlanet,
            2 => Enum.GetValues<CopernicusObservation>().Single(o => !state.Order.Contains(o)) switch
            {
                CopernicusObservation.Observer => CopernicusShiftedCenterStage.LastObserver,
                CopernicusObservation.Planet => CopernicusShiftedCenterStage.LastPlanet,
                _ => CopernicusShiftedCenterStage.LastApparent,
            },
            _ => CopernicusShiftedCenterStage.ResidualSolar,
        };
    }
    public static bool IsRouteConsistent(CopernicusShiftedCenterState state, WesternJourneyState journey,
        DemocritusArrangementState democritus, EmpedoclesRootsInquiryState empedocles, WesternRouteGraph graph) =>
        state.IsUsable() && democritus.IsUsable() && democritus.Closed && democritus.Stage == DemocritusArrangementStage.ResultAdopted
        && empedocles.IsUsable() && empedocles.Closed
        && empedocles.ConfirmedActions.Contains(EmpedoclesRootsInquiryAction.AcceptInvitation)
        && state.DemocritusInstanceId == democritus.InstanceId && state.EmpedoclesInstanceId == empedocles.InstanceId
        && EmpedoclesRootsInquiryFlow.IsJourneyConsistent(empedocles, journey, graph)
        && journey.CompletedEdgeIds.Contains(DemocritusArrangementState.PrerequisiteEdgeId);
    public static bool TryPrepare(CopernicusShiftedCenterState state, CopernicusShiftedCenterAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != CopernicusShiftedCenterAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action; return true;
    }
    public static bool TryCommitPending(CopernicusShiftedCenterState state, WesternJourneyState journey,
        DemocritusArrangementState democritus, EmpedoclesRootsInquiryState empedocles, WesternRouteGraph graph)
    {
        if (!IsRouteConsistent(state, journey, democritus, empedocles, graph) || state.Closed
            || state.PendingAction == CopernicusShiftedCenterAction.None) return false;
        Apply(state, state.PendingAction); return IsRouteConsistent(state, journey, democritus, empedocles, graph);
    }
    public static string StageKey(CopernicusShiftedCenterStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(CopernicusShiftedCenterAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
