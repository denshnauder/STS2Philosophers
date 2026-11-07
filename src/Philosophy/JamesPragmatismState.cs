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
    /// <summary>The act this independent question was opened in; legacy nested segments normalize to two.</summary>
    public int ActIndex { get; set; } = 2;
    public JamesPragmatismStage Stage { get; set; }
    public JamesPragmatismAction PendingAction { get; set; }
    public List<JamesPragmatismAction> ConfirmedActions { get; set; } = [];
    public JamesPragmatismStandard Standard { get; set; }
    public bool Closed { get; set; }

    public static JamesPragmatismState Create(string instanceId) => new() { InstanceId = instanceId };

    public static JamesPragmatismState CreateAt(string instanceId, int actIndex) =>
        new() { InstanceId = instanceId, ActIndex = actIndex };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        if (ActIndex is < 1 or > 3) ActIndex = 2;
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
        var replay = CreateAt(InstanceId, ActIndex);
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

    public static bool TryCommitPending(JamesPragmatismState state)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction == JamesPragmatismAction.None
            || !Options(state.Stage).Contains(state.PendingAction)) return false;
        Apply(state, state.PendingAction);
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
