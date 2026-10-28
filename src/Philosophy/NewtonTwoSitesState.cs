using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum NewtonTwoSitesStage
{
    Invitation, SiteChoice, FirstReading, OtherSite, Decision,
    ResultConditional, ResultRechecked, ResultLocal, ResultDeclined
}

internal enum NewtonTwoSitesAction
{
    None, Accept, Decline, MeasureDrop, MeasureTide, RecordError,
    RevealOther, ConditionRelation, RecheckOtherSite, KeepLocal, Leave
}

internal enum NewtonSite { None, Drop, Tide }

internal enum NewtonDisposition { None, Conditional, Rechecked, Local }

// ENC-128 event state and pure replay flow.
internal sealed class NewtonTwoSitesState : IJsonOnDeserialized
{
    public const string SourceNodeId = "SOCRATES__KNOWLEDGE_AND_DOUBT__CORE";
    public const string AdoptedNodeId = "ISAAC_NEWTON__KNOWLEDGE_AND_DOUBT__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_049";

    public int Version { get; set; } = 0;
    public string InstanceId { get; set; } = "";
    public string SextusInstanceId { get; set; } = "";
    public string DescartesInstanceId { get; set; } = "";
    public NewtonTwoSitesStage Stage { get; set; } = (NewtonTwoSitesStage)(-1);
    public NewtonTwoSitesAction PendingAction { get; set; } = (NewtonTwoSitesAction)(-1);
    public List<NewtonTwoSitesAction> ConfirmedActions { get; set; } = null!;
    public NewtonSite FirstSite { get; set; } = NewtonSite.None;
    public NewtonSite CalibrationSite { get; set; } = NewtonSite.None;
    public NewtonDisposition Disposition { get; set; } = NewtonDisposition.None;
    public bool FixedSlotResolved { get; set; }
    public bool EdgeCommitted { get; set; }

    private bool? _closed;
    public bool Closed
    {
        get => _closed ?? false;
        set => _closed = value;
    }

    public static NewtonTwoSitesState Create(string instanceId, string sextusId, string descartesId) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        SextusInstanceId = sextusId,
        DescartesInstanceId = descartesId,
        Stage = NewtonTwoSitesStage.Invitation,
        PendingAction = NewtonTwoSitesAction.None,
        ConfirmedActions = new(),
        FirstSite = NewtonSite.None,
        CalibrationSite = NewtonSite.None,
        Disposition = NewtonDisposition.None,
        FixedSlotResolved = false,
        EdgeCommitted = false,
        Closed = false
    };

    public void OnDeserialized() => NormalizeAfterLoad();

    public void NormalizeAfterLoad()
    {
        InstanceId ??= "";
        SextusInstanceId ??= "";
        DescartesInstanceId ??= "";
        if (ConfirmedActions is null || _closed is null)
        {
            Version = 0;
        }
    }

    public bool IsUsable()
    {
        if (Version != 1 || _closed is null) return false;
        if (string.IsNullOrWhiteSpace(InstanceId) || string.IsNullOrWhiteSpace(SextusInstanceId) ||
            string.IsNullOrWhiteSpace(DescartesInstanceId)) return false;
        if (ConfirmedActions is null) return false;
        if (ConfirmedActions.Count > 6) return false;
        if (!Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(FirstSite)
            || !Enum.IsDefined(CalibrationSite) || !Enum.IsDefined(Disposition)) return false;

        var replay = Create(InstanceId, SextusInstanceId, DescartesInstanceId);
        foreach (var action in ConfirmedActions)
        {
            if (!Enum.IsDefined(action)) return false;
            if (replay.Closed) return false;
            var options = NewtonTwoSitesFlow.Options(replay.Stage);
            if (!options.Contains(action)) return false;
            NewtonTwoSitesFlow.Apply(replay, action);
        }

        if (replay.Stage != Stage
            || replay.FirstSite != FirstSite
            || replay.CalibrationSite != CalibrationSite
            || replay.Disposition != Disposition
            || replay.FixedSlotResolved != FixedSlotResolved
            || replay.EdgeCommitted != EdgeCommitted
            || replay.Closed != Closed) return false;

        if (Closed)
        {
            if (PendingAction != NewtonTwoSitesAction.None) return false;
        }
        else
        {
            if (PendingAction != NewtonTwoSitesAction.None
                && !NewtonTwoSitesFlow.Options(Stage).Contains(PendingAction)) return false;
        }

        return true;
    }
}
