using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum SpinozaCauseNetworkStage
{
    Invitation,
    Attribution,
    WeatherRevealed,
    Decision,
    ResultIntervened,
    ResultReordered,
    ResultCorrected,
    ResultDeclined
}

internal enum SpinozaCauseNetworkAction
{
    None,
    Accept,
    Decline,
    BlameEnemy,
    TraceChains,
    Continue,
    Intervene,
    Reorder,
    CorrectRecord,
    Leave
}

internal enum SpinozaAttribution { None, BlameEnemy, TraceChains }

internal enum SpinozaDisposition { None, Intervened, Reordered, Corrected }

internal sealed class SpinozaCauseNetworkState : IJsonOnDeserialized
{
    public const string SourceNodeId = "DESCARTES__SELF_AND_OTHER__CORE";
    public const string AdoptedNodeId = "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE";
    public const string CompletionEdgeId = "WESTERN_EDGE_023";

    // The line that reaches Spinoza decides which node it stands on and which edge
    // carries it to him: the nature line enters at Heraclitus, not at Descartes.
    public string SourceNode { get; set; } = SourceNodeId;
    public string AdoptEdge { get; set; } = CompletionEdgeId;
    public string SourceThinker { get; set; } = "DESCARTES";

    private bool? _closed;

    public int Version { get; set; }
    public string InstanceId { get; set; } = "";
    public SpinozaCauseNetworkStage Stage { get; set; } = (SpinozaCauseNetworkStage)(-1);
    public SpinozaCauseNetworkAction PendingAction { get; set; } = (SpinozaCauseNetworkAction)(-1);
    public List<SpinozaCauseNetworkAction> ConfirmedActions { get; set; } = null!;
    public SpinozaAttribution FirstAttribution { get; set; }
    public SpinozaDisposition Disposition { get; set; }
    public bool FixedSlotResolved { get; set; }
    public bool EdgeCommitted { get; set; }

    public bool Closed
    {
        get => _closed ?? false;
        set => _closed = value;
    }

    public static SpinozaCauseNetworkState Create(string instanceId) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        Stage = SpinozaCauseNetworkStage.Invitation,
        PendingAction = SpinozaCauseNetworkAction.None,
        ConfirmedActions = new List<SpinozaCauseNetworkAction>(),
        FirstAttribution = SpinozaAttribution.None,
        Disposition = SpinozaDisposition.None,
        FixedSlotResolved = false,
        EdgeCommitted = false,
        Closed = false
    };

    public void OnDeserialized() => NormalizeAfterLoad();

    public void NormalizeAfterLoad()
    {
        InstanceId ??= "";
        if (ConfirmedActions is null || _closed is null)
        {
            Version = 0;
        }
    }

    public bool IsUsable()
    {
        if (Version != 1) return false;
        if (string.IsNullOrWhiteSpace(InstanceId) || InstanceId.Length > 150) return false;
        if (_closed is null) return false;
        if (!Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        if (!Enum.IsDefined(FirstAttribution) || !Enum.IsDefined(Disposition)) return false;
        if (ConfirmedActions is null || ConfirmedActions.Count > 5) return false;

        var replay = Create(InstanceId);
        foreach (var action in ConfirmedActions)
        {
            if (replay.Closed) return false;
            if (!SpinozaCauseNetworkFlow.Options(replay.Stage).Contains(action)) return false;
            SpinozaCauseNetworkFlow.Apply(replay, action);
        }

        if (replay.Stage != Stage) return false;
        if (replay.FirstAttribution != FirstAttribution) return false;
        if (replay.Disposition != Disposition) return false;
        if (replay.FixedSlotResolved != FixedSlotResolved) return false;
        if (replay.EdgeCommitted != EdgeCommitted) return false;
        if (replay.Closed != Closed) return false;

        if (Closed) return PendingAction == SpinozaCauseNetworkAction.None;
        return PendingAction == SpinozaCauseNetworkAction.None
            || SpinozaCauseNetworkFlow.Options(Stage).Contains(PendingAction);
    }

}
