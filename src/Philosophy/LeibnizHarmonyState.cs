using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum LeibnizHarmonyStage
{
    Invitation,
    ClockChoice,
    Revelation,
    Decision,
    ResultIndependent,
    ResultCoordinated,
    ResultKept,
    ResultDeclined
}

internal enum LeibnizHarmonyAction
{
    None,
    Accept,
    Decline,
    DelayOne,
    ObserveBoth,
    Continue,
    Independent,
    Coordinated,
    KeepSpinoza,
    Leave
}

internal enum LeibnizClockProbe { None, DelayOne, ObserveBoth }

internal enum LeibnizDisposition { None, Independent, Coordinated, Kept }

internal sealed class LeibnizHarmonyState : IJsonOnDeserialized
{
    public const string SourceNodeId = "BARUCH_SPINOZA__BEING_AND_CHANGE__CORE";
    public const string AdoptedNodeId = "GOTTFRIED_WILHELM_LEIBNIZ__BEING_AND_CHANGE__HARMONY";
    public const string CompletionEdgeId = "WESTERN_EDGE_050";
    public const string PredecessorEdgeId = "WESTERN_EDGE_023";
    public const string EventModelId = "LeibnizIndependentClocks";
    public const string LocalizationRoot = "LEIBNIZ_INDEPENDENT_CLOCKS";

    private bool? _closed;

    public int Version { get; set; }
    public string InstanceId { get; set; } = "";
    public string SpinozaInstanceId { get; set; } = "";
    public LeibnizHarmonyStage Stage { get; set; } = (LeibnizHarmonyStage)(-1);
    public LeibnizHarmonyAction PendingAction { get; set; } = (LeibnizHarmonyAction)(-1);
    public List<LeibnizHarmonyAction> ConfirmedActions { get; set; } = null!;
    public LeibnizClockProbe Probe { get; set; }
    public LeibnizDisposition Disposition { get; set; }
    public bool FixedSlotResolved { get; set; }
    public bool EdgeCommitted { get; set; }

    public bool Closed
    {
        get => _closed ?? false;
        set => _closed = value;
    }

    public static string BuildInstanceId(ulong runSeed) => $"{LocalizationRoot}:{runSeed}:3";

    public static LeibnizHarmonyState Create(string instanceId, string spinozaInstanceId) => new()
    {
        Version = 1,
        InstanceId = instanceId,
        SpinozaInstanceId = spinozaInstanceId,
        Stage = LeibnizHarmonyStage.Invitation,
        PendingAction = LeibnizHarmonyAction.None,
        ConfirmedActions = new List<LeibnizHarmonyAction>(),
        Probe = LeibnizClockProbe.None,
        Disposition = LeibnizDisposition.None,
        FixedSlotResolved = false,
        EdgeCommitted = false,
        Closed = false
    };

    public void OnDeserialized() => NormalizeAfterLoad();

    public void NormalizeAfterLoad()
    {
        // Preserve corrupt payloads so IsUsable rejects them instead of fabricating fresh state.
        InstanceId ??= "";
        SpinozaInstanceId ??= "";
        if (ConfirmedActions is null || _closed is null)
        {
            Version = 0;
        }
    }

    public bool IsUsable()
    {
        if (Version != 1) return false;
        if (!IsValidId(InstanceId) || !IsValidId(SpinozaInstanceId)) return false;
        if (_closed is null) return false;
        if (!Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        if (!Enum.IsDefined(Probe) || !Enum.IsDefined(Disposition)) return false;
        if (ConfirmedActions is null || ConfirmedActions.Count > 8) return false;

        var replay = Create(InstanceId, SpinozaInstanceId);
        foreach (var action in ConfirmedActions)
        {
            if (replay.Closed) return false;
            if (!LeibnizHarmonyFlow.Options(replay.Stage).Contains(action)) return false;
            LeibnizHarmonyFlow.Apply(replay, action);
        }

        if (replay.Stage != Stage) return false;
        if (replay.Probe != Probe) return false;
        if (replay.Disposition != Disposition) return false;
        if (replay.FixedSlotResolved != FixedSlotResolved) return false;
        if (replay.EdgeCommitted != EdgeCommitted) return false;
        if (replay.Closed != Closed) return false;

        if (Closed) return PendingAction == LeibnizHarmonyAction.None;
        return PendingAction == LeibnizHarmonyAction.None
            || LeibnizHarmonyFlow.Options(Stage).Contains(PendingAction);
    }

    private static bool IsValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 150;
}
