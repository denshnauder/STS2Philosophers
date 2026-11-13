using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum VoltaireToleranceStage { Invitation, ResultProtect, ResultRestrict, ResultBalance }
internal enum VoltaireToleranceAction { None, ProtectSpeech, RestrictSpeech, KeepBalance, Leave }
internal enum VoltaireToleranceConclusion { None, Protect, Restrict, Balance }

/// <summary>
/// Voltaire's in-batch segment inside Montesquieu's room. It records one narrow
/// verdict on a tolerance case and never owns a map edge or a question slot.
/// </summary>
internal sealed class VoltaireToleranceState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public VoltaireToleranceStage Stage { get; set; }
    public VoltaireToleranceAction PendingAction { get; set; }
    public List<VoltaireToleranceAction> ConfirmedActions { get; set; } = [];
    public VoltaireToleranceConclusion Conclusion { get; set; }
    public bool Closed { get; set; }

    public static VoltaireToleranceState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || !Enum.IsDefined(Conclusion) || ConfirmedActions is null || ConfirmedActions.Count > 2
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != VoltaireToleranceAction.None
            || PendingAction != VoltaireToleranceAction.None
                && !VoltaireToleranceFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (VoltaireToleranceAction action in ConfirmedActions)
        {
            if (replay.Closed || !VoltaireToleranceFlow.Options(replay.Stage).Contains(action)) return false;
            VoltaireToleranceFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Conclusion == replay.Conclusion && Closed == replay.Closed;
    }
}

internal static class VoltaireToleranceFlow
{
    public static IReadOnlyList<VoltaireToleranceAction> Options(VoltaireToleranceStage stage) => stage switch
    {
        VoltaireToleranceStage.Invitation =>
            [VoltaireToleranceAction.ProtectSpeech, VoltaireToleranceAction.RestrictSpeech,
                VoltaireToleranceAction.KeepBalance],
        VoltaireToleranceStage.ResultProtect or VoltaireToleranceStage.ResultRestrict
            or VoltaireToleranceStage.ResultBalance => [VoltaireToleranceAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(VoltaireToleranceState state, VoltaireToleranceAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != VoltaireToleranceAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(VoltaireToleranceState state, VoltaireToleranceAction action)
    {
        switch (action)
        {
            case VoltaireToleranceAction.ProtectSpeech:
                state.Conclusion = VoltaireToleranceConclusion.Protect;
                state.Stage = VoltaireToleranceStage.ResultProtect;
                break;
            case VoltaireToleranceAction.RestrictSpeech:
                state.Conclusion = VoltaireToleranceConclusion.Restrict;
                state.Stage = VoltaireToleranceStage.ResultRestrict;
                break;
            case VoltaireToleranceAction.KeepBalance:
                state.Conclusion = VoltaireToleranceConclusion.Balance;
                state.Stage = VoltaireToleranceStage.ResultBalance;
                break;
            case VoltaireToleranceAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = VoltaireToleranceAction.None;
    }

    public static string DescriptionKey(VoltaireToleranceState state) => state.Stage switch
    {
        VoltaireToleranceStage.Invitation => "INVITATION",
        VoltaireToleranceStage.ResultProtect => "RESULT_PROTECT",
        VoltaireToleranceStage.ResultRestrict => "RESULT_RESTRICT",
        VoltaireToleranceStage.ResultBalance => "RESULT_BALANCE",
        _ => "INVITATION",
    };

    public static string StageKey(VoltaireToleranceStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(VoltaireToleranceAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
