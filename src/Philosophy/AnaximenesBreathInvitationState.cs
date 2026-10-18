namespace STS2Philosophers;

internal enum AnaximenesBreathInvitationStage { Invitation, Accepted, Declined }
internal enum AnaximenesBreathInvitationAction { None, Accept, Decline, Leave }

internal sealed class AnaximenesBreathInvitationState
{
    public const string RequiredNodeId = "HERACLITUS__BEING_AND_CHANGE__CORE";
    public int Version { get; set; } = 1;
    public string InstanceId { get; set; } = string.Empty;
    public AnaximenesBreathInvitationStage Stage { get; set; }
    public AnaximenesBreathInvitationAction PendingAction { get; set; }
    public List<AnaximenesBreathInvitationAction> ConfirmedActions { get; set; } = [];
    public bool Closed { get; set; }

    public static AnaximenesBreathInvitationState Create(string id) => new() { InstanceId = id };

    public void NormalizeAfterLoad()
    {
        InstanceId ??= string.Empty;
        ConfirmedActions ??= [];
    }

    public bool IsUsable()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(InstanceId) || ConfirmedActions is null
            || ConfirmedActions.Count > 2 || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)) return false;
        var stage = AnaximenesBreathInvitationStage.Invitation;
        bool closed = false;
        foreach (AnaximenesBreathInvitationAction action in ConfirmedActions)
        {
            if (closed || !AnaximenesBreathInvitationFlow.Options(stage).Contains(action)) return false;
            stage = AnaximenesBreathInvitationFlow.Next(stage, action);
            closed = action == AnaximenesBreathInvitationAction.Leave;
        }
        return stage == Stage && closed == Closed
            && (PendingAction == AnaximenesBreathInvitationAction.None
                || !Closed && AnaximenesBreathInvitationFlow.Options(Stage).Contains(PendingAction));
    }
}

internal static class AnaximenesBreathInvitationFlow
{
    public static IReadOnlyList<AnaximenesBreathInvitationAction> Options(AnaximenesBreathInvitationStage stage) => stage switch
    {
        AnaximenesBreathInvitationStage.Invitation =>
            [AnaximenesBreathInvitationAction.Accept, AnaximenesBreathInvitationAction.Decline],
        AnaximenesBreathInvitationStage.Accepted or AnaximenesBreathInvitationStage.Declined =>
            [AnaximenesBreathInvitationAction.Leave],
        _ => [],
    };

    public static AnaximenesBreathInvitationStage Next(
        AnaximenesBreathInvitationStage stage, AnaximenesBreathInvitationAction action) => action switch
    {
        AnaximenesBreathInvitationAction.Accept => AnaximenesBreathInvitationStage.Accepted,
        AnaximenesBreathInvitationAction.Decline => AnaximenesBreathInvitationStage.Declined,
        _ => stage,
    };

    public static bool TryPrepare(AnaximenesBreathInvitationState state, AnaximenesBreathInvitationAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != AnaximenesBreathInvitationAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(AnaximenesBreathInvitationState state, bool hasPracticeRelic)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction == AnaximenesBreathInvitationAction.None)
            return false;
        var action = state.PendingAction;
        if (action == AnaximenesBreathInvitationAction.Accept && !hasPracticeRelic
            || action == AnaximenesBreathInvitationAction.Decline && hasPracticeRelic
            || action == AnaximenesBreathInvitationAction.Leave
                && (state.Stage == AnaximenesBreathInvitationStage.Accepted) != hasPracticeRelic)
            return false;
        state.Stage = Next(state.Stage, action);
        state.Closed = action == AnaximenesBreathInvitationAction.Leave;
        state.ConfirmedActions.Add(action);
        state.PendingAction = AnaximenesBreathInvitationAction.None;
        return true;
    }

    public static string StageKey(AnaximenesBreathInvitationStage stage) => stage.ToString().ToUpperInvariant();
    public static string ActionKey(AnaximenesBreathInvitationAction action) => action.ToString().ToUpperInvariant();
}
