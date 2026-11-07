using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum BerkeleyMarkStage { Invitation, Observed, ResultReject, ResultWitness, ResultScope }
internal enum BerkeleyMarkAction { None, CheckTouch, CheckSound, CheckReceipt, RejectMaterialSubstance, RequestSecondObserver, KeepPerceptionScope, Leave }
internal enum BerkeleyChannel { None, Touch, Sound, Receipt }
internal enum BerkeleyConclusion { None, RejectMaterialSubstance, SecondObserver, PerceptionScope }

/// <summary>
/// Saved state for Berkeley's in-batch observation scene. It records only what the
/// player actually observed (one channel), the hidden second object that broke the
/// first explanation, and the narrow conclusion. It deliberately does not own a map
/// edge: the hosting opportunity is decided by the route, not by this state.
/// </summary>
internal sealed class BerkeleyPerceivedMarkState : IJsonOnDeserialized, IEventGoldCostState
{
    public const int CurrentVersion = 2;
    public const string HiddenObjectId = "SEALED_SECOND_CRATE";

    // The second-observer branch promises one shop purchase opportunity. The mod
    // has no purchase-slot API, so the cost is charged as the gold a single shop
    // purchase would take; the option is hidden when the player cannot pay.
    public const decimal SecondObserverGoldCost = 75m;

    public int Version { get; set; } = CurrentVersion;
    public string InstanceId { get; set; } = string.Empty;
    /// <summary>The act this independent question was opened in; nested legacy segments normalize to two.</summary>
    public int ActIndex { get; set; } = 2;
    public BerkeleyMarkStage Stage { get; set; }
    public BerkeleyMarkAction PendingAction { get; set; }
    public List<BerkeleyMarkAction> ConfirmedActions { get; set; } = [];
    public BerkeleyChannel Channel { get; set; }
    public string? HiddenObject { get; set; }
    public BerkeleyConclusion Conclusion { get; set; }
    public bool Closed { get; set; }
    public bool CostAttempted { get; set; }
    public bool CostPaid { get; set; }
    public decimal CostGoldBefore { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;
    public decimal CostGoldAfter { get; set; } = EventGoldCostProtocol.NoGoldSnapshot;

    public decimal Cost => Conclusion == BerkeleyConclusion.SecondObserver
        ? SecondObserverGoldCost : 0m;

    public static BerkeleyPerceivedMarkState Create(string instanceId) => new() { InstanceId = instanceId };

    public static BerkeleyPerceivedMarkState CreateAt(string instanceId, int actIndex) =>
        new() { InstanceId = instanceId, ActIndex = actIndex };

    public void NormalizeAfterLoad()
    {
        // Version 1 predates this outcome's price. Such a record already holds the
        // result the player was given, so it settles as paid instead of charging a
        // choice that was never shown as priced. New records keep the payment
        // window and are charged exactly once by the shared protocol.
        if (Version < CurrentVersion)
        {
            // Only a record that already holds the priced result settles. An old
            // invitation that is upgraded to the second observer afterwards is a
            // new choice and keeps its payment window.
            if (Conclusion == BerkeleyConclusion.SecondObserver)
            {
                CostAttempted = true;
                CostPaid = true;
            }
            CostGoldBefore = EventGoldCostProtocol.NoGoldSnapshot;
            CostGoldAfter = EventGoldCostProtocol.NoGoldSnapshot;
            Version = CurrentVersion;
        }
        InstanceId ??= string.Empty;
        if (ActIndex is < 1 or > 3) ActIndex = 2;
        ConfirmedActions ??= [];
        HiddenObject ??= null;
    }

    void IJsonOnDeserialized.OnDeserialized() => NormalizeAfterLoad();

    public bool IsUsable()
    {
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(InstanceId)
            || !Enum.IsDefined(Stage) || !Enum.IsDefined(PendingAction)
            || !Enum.IsDefined(Channel) || !Enum.IsDefined(Conclusion)
            || ConfirmedActions is null || ConfirmedActions.Count > 4
            || ConfirmedActions.Any(action => !Enum.IsDefined(action))
            || Closed && PendingAction != BerkeleyMarkAction.None
            || PendingAction != BerkeleyMarkAction.None
                && !BerkeleyPerceivedMarkFlow.Options(Stage).Contains(PendingAction)
            || HiddenObject is not null && HiddenObject != HiddenObjectId) return false;

        var replay = CreateAt(InstanceId, ActIndex);
        foreach (BerkeleyMarkAction action in ConfirmedActions)
        {
            if (replay.Closed || !BerkeleyPerceivedMarkFlow.Options(replay.Stage).Contains(action)) return false;
            BerkeleyPerceivedMarkFlow.Apply(replay, action);
        }
        return Stage == replay.Stage && Channel == replay.Channel && HiddenObject == replay.HiddenObject
            && Conclusion == replay.Conclusion && Closed == replay.Closed;
    }
}

internal static class BerkeleyPerceivedMarkFlow
{
    public static IReadOnlyList<BerkeleyMarkAction> Options(BerkeleyMarkStage stage) => stage switch
    {
        BerkeleyMarkStage.Invitation =>
            [BerkeleyMarkAction.CheckTouch, BerkeleyMarkAction.CheckSound, BerkeleyMarkAction.CheckReceipt],
        BerkeleyMarkStage.Observed =>
            [BerkeleyMarkAction.RejectMaterialSubstance, BerkeleyMarkAction.RequestSecondObserver,
                BerkeleyMarkAction.KeepPerceptionScope],
        BerkeleyMarkStage.ResultReject or BerkeleyMarkStage.ResultWitness or BerkeleyMarkStage.ResultScope =>
            [BerkeleyMarkAction.Leave],
        _ => [],
    };

    public static bool TryPrepare(BerkeleyPerceivedMarkState state, BerkeleyMarkAction action)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction != BerkeleyMarkAction.None
            || !Options(state.Stage).Contains(action)) return false;
        state.PendingAction = action;
        return true;
    }

    public static bool TryCommitPending(BerkeleyPerceivedMarkState state)
    {
        if (!state.IsUsable() || state.Closed || state.PendingAction == BerkeleyMarkAction.None
            || !Options(state.Stage).Contains(state.PendingAction)) return false;
        Apply(state, state.PendingAction);
        return true;
    }

    public static void Apply(BerkeleyPerceivedMarkState state, BerkeleyMarkAction action)
    {
        switch (action)
        {
            case BerkeleyMarkAction.CheckTouch:
                state.Channel = BerkeleyChannel.Touch;
                state.HiddenObject = BerkeleyPerceivedMarkState.HiddenObjectId;
                state.Stage = BerkeleyMarkStage.Observed;
                break;
            case BerkeleyMarkAction.CheckSound:
                state.Channel = BerkeleyChannel.Sound;
                state.HiddenObject = BerkeleyPerceivedMarkState.HiddenObjectId;
                state.Stage = BerkeleyMarkStage.Observed;
                break;
            case BerkeleyMarkAction.CheckReceipt:
                state.Channel = BerkeleyChannel.Receipt;
                state.HiddenObject = BerkeleyPerceivedMarkState.HiddenObjectId;
                state.Stage = BerkeleyMarkStage.Observed;
                break;
            case BerkeleyMarkAction.RejectMaterialSubstance:
                state.Conclusion = BerkeleyConclusion.RejectMaterialSubstance;
                state.Stage = BerkeleyMarkStage.ResultReject;
                break;
            case BerkeleyMarkAction.RequestSecondObserver:
                state.Conclusion = BerkeleyConclusion.SecondObserver;
                state.Stage = BerkeleyMarkStage.ResultWitness;
                break;
            case BerkeleyMarkAction.KeepPerceptionScope:
                state.Conclusion = BerkeleyConclusion.PerceptionScope;
                state.Stage = BerkeleyMarkStage.ResultScope;
                break;
            case BerkeleyMarkAction.Leave:
                state.Closed = true;
                break;
        }
        state.ConfirmedActions.Add(action);
        state.PendingAction = BerkeleyMarkAction.None;
    }

    public static string DescriptionKey(BerkeleyPerceivedMarkState state) => state.Stage switch
    {
        BerkeleyMarkStage.Invitation => "INVITATION",
        BerkeleyMarkStage.Observed => "OBSERVED",
        BerkeleyMarkStage.ResultReject => "RESULT_REJECT",
        BerkeleyMarkStage.ResultWitness => "RESULT_WITNESS",
        BerkeleyMarkStage.ResultScope => "RESULT_SCOPE",
        _ => "INVITATION",
    };
}
