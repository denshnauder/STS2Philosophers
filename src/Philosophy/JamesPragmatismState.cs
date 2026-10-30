using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum JamesPragmatismStage { Invitation, ResultPublic, ResultPrivate }
internal enum JamesPragmatismAction { None, TestPublicly, KeepPrivate, Leave }
internal enum JamesPragmatismStandard { None, Public, Private }

/// <summary>
/// James' in-batch segment inside Peirce's room. It adds no graph node or edge:
/// the player either tests a belief in public or keeps it private, and the
/// segment closes back into Peirce's result page.
/// </summary>
internal sealed class JamesPragmatismState : IJsonOnDeserialized
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    public JamesPragmatismStage Stage { get; set; }
    public JamesPragmatismAction PendingAction { get; set; }
    public List<JamesPragmatismAction> ConfirmedActions { get; set; } = [];
    public JamesPragmatismStandard Standard { get; set; }
    public bool Closed { get; set; }

    public static JamesPragmatismState Create(string instanceId) => new() { InstanceId = instanceId };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction) || !Enum.IsDefined(Standard)
            || ConfirmedActions is null || ConfirmedActions.Count > 3
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != JamesPragmatismAction.None
            || PendingAction != JamesPragmatismAction.None
                && !JamesPragmatismFlow.Options(Stage).Contains(PendingAction)) return false;
        var replay = Create(InstanceId);
        foreach (JamesPragmatismAction action in ConfirmedActions)
        {
            if (replay.Closed || !JamesPragmatismFlow.Options(replay.Stage).Contains(action)) return false;
            JamesPragmatismFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Standard == replay.Standard && Closed == replay.Closed;
    }
}

internal static class JamesPragmatismFlow
{
    public static IReadOnlyList<JamesPragmatismAction> Options(JamesPragmatismStage stage) => stage switch
    {
        JamesPragmatismStage.Invitation => [JamesPragmatismAction.TestPublicly, JamesPragmatismAction.KeepPrivate],
        JamesPragmatismStage.ResultPublic or JamesPragmatismStage.ResultPrivate => [JamesPragmatismAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(JamesPragmatismState state, JamesPragmatismAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != JamesPragmatismAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static void Apply(JamesPragmatismState state, JamesPragmatismAction action)
    {
        switch (action)
        {
            case JamesPragmatismAction.TestPublicly:
                state.Standard = JamesPragmatismStandard.Public;
                state.Stage = JamesPragmatismStage.ResultPublic;
                break;
            case JamesPragmatismAction.KeepPrivate:
                state.Standard = JamesPragmatismStandard.Private;
                state.Stage = JamesPragmatismStage.ResultPrivate;
                break;
            case JamesPragmatismAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = JamesPragmatismAction.None;
    }

    public static string StageKey(JamesPragmatismStage stage) => stage switch
    {
        JamesPragmatismStage.Invitation => "INVITATION",
        JamesPragmatismStage.ResultPublic => "RESULT_PUBLIC",
        JamesPragmatismStage.ResultPrivate => "RESULT_PRIVATE",
        _ => "INVITATION",
    };

    public static string ActionKey(JamesPragmatismAction action) => action switch
    {
        JamesPragmatismAction.TestPublicly => "TEST_PUBLICLY",
        JamesPragmatismAction.KeepPrivate => "KEEP_PRIVATE",
        JamesPragmatismAction.Leave => "LEAVE",
        _ => "NONE",
    };
}
