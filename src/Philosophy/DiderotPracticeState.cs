using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum DiderotPracticeStage { Invitation, ResultSystem, ResultSample, ResultRefuse }
internal enum DiderotPracticeAction { None, CollectIntoSystem, KeepSample, RefuseSummary, Leave }
internal enum DiderotPracticeConclusion { None, System, Sample, Refused }

/// <summary>
/// Diderot's in-batch practice segment inside Bacon's room. It records how the
/// observed sample enters (or stays out of) the published system and owns no
/// map edge, fixed slot, or question slot.
/// </summary>
internal sealed class DiderotPracticeState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public DiderotPracticeStage Stage { get; set; }
    public DiderotPracticeAction PendingAction { get; set; }
    public List<DiderotPracticeAction> ConfirmedActions { get; set; } = [];
    public DiderotPracticeConclusion Conclusion { get; set; }
    public bool Closed { get; set; }

    public static DiderotPracticeState Create(string instanceId) => new() { InstanceId = instanceId };

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
            || Closed && PendingAction != DiderotPracticeAction.None
            || PendingAction != DiderotPracticeAction.None
                && !DiderotPracticeFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (DiderotPracticeAction action in ConfirmedActions)
        {
            if (replay.Closed || !DiderotPracticeFlow.Options(replay.Stage).Contains(action)) return false;
            DiderotPracticeFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Conclusion == replay.Conclusion && Closed == replay.Closed;
    }
}

internal static class DiderotPracticeFlow
{
    public static IReadOnlyList<DiderotPracticeAction> Options(DiderotPracticeStage stage) => stage switch
    {
        DiderotPracticeStage.Invitation =>
            [DiderotPracticeAction.CollectIntoSystem, DiderotPracticeAction.KeepSample,
                DiderotPracticeAction.RefuseSummary],
        DiderotPracticeStage.ResultSystem or DiderotPracticeStage.ResultSample
            or DiderotPracticeStage.ResultRefuse => [DiderotPracticeAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(DiderotPracticeState state, DiderotPracticeAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != DiderotPracticeAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(DiderotPracticeState state, DiderotPracticeAction action)
    {
        switch (action)
        {
            case DiderotPracticeAction.CollectIntoSystem:
                state.Conclusion = DiderotPracticeConclusion.System;
                state.Stage = DiderotPracticeStage.ResultSystem;
                break;
            case DiderotPracticeAction.KeepSample:
                state.Conclusion = DiderotPracticeConclusion.Sample;
                state.Stage = DiderotPracticeStage.ResultSample;
                break;
            case DiderotPracticeAction.RefuseSummary:
                state.Conclusion = DiderotPracticeConclusion.Refused;
                state.Stage = DiderotPracticeStage.ResultRefuse;
                break;
            case DiderotPracticeAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = DiderotPracticeAction.None;
    }

    public static string DescriptionKey(DiderotPracticeState state) => state.Stage switch
    {
        DiderotPracticeStage.Invitation => "INVITATION",
        DiderotPracticeStage.ResultSystem => "RESULT_SYSTEM",
        DiderotPracticeStage.ResultSample => "RESULT_SAMPLE",
        DiderotPracticeStage.ResultRefuse => "RESULT_REFUSE",
        _ => "INVITATION",
    };

    public static string StageKey(DiderotPracticeStage stage) => UpperSnake(stage.ToString());
    public static string ActionKey(DiderotPracticeAction action) => UpperSnake(action.ToString());
    private static string UpperSnake(string value) => string.Concat(value.SelectMany((c, i) =>
        i > 0 && char.IsUpper(c) ? new[] { '_', c } : new[] { char.ToUpperInvariant(c) }));
}
