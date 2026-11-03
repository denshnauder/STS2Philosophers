using STS2Philosophers;

internal static class BerkeleyPerceivedMarkChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static BerkeleyPerceivedMarkState Observe(BerkeleyMarkAction channel)
    {
        var state = BerkeleyPerceivedMarkState.Create("BERKELEY_PERCEIVED_MARK:TEST:1");
        Check(BerkeleyPerceivedMarkFlow.TryPrepare(state, channel), "The observation channel must be selectable first.");
        BerkeleyPerceivedMarkFlow.Apply(state, channel);
        return state;
    }

    internal static async Task RunAsync()
    {
        var invitation = BerkeleyPerceivedMarkState.Create("BERKELEY_PERCEIVED_MARK:TEST:1");
        Check(BerkeleyPerceivedMarkFlow.Options(invitation.Stage).Count == 3,
            "Only one of touch, sound or receipt may be offered at entry.");
        Check(!BerkeleyPerceivedMarkFlow.TryPrepare(invitation, BerkeleyMarkAction.RejectMaterialSubstance),
            "A conclusion was offered before any observation.");

        foreach (BerkeleyMarkAction channel in new[]
                 { BerkeleyMarkAction.CheckTouch, BerkeleyMarkAction.CheckSound, BerkeleyMarkAction.CheckReceipt })
        {
            var observed = Observe(channel);
            Check(observed.Stage == BerkeleyMarkStage.Observed && observed.HiddenObject == BerkeleyPerceivedMarkState.HiddenObjectId,
                "An observation must reveal the hidden second object without declaring the room empty.");
            Check(BerkeleyPerceivedMarkFlow.Options(observed.Stage).Count == 3,
                "Exactly three narrow conclusions must follow the observation.");
        }

        var reject = Observe(BerkeleyMarkAction.CheckTouch);
        Check(BerkeleyPerceivedMarkFlow.TryPrepare(reject, BerkeleyMarkAction.RejectMaterialSubstance),
            "Rejecting the unsupported material explanation must be available.");
        BerkeleyPerceivedMarkFlow.Apply(reject, BerkeleyMarkAction.RejectMaterialSubstance);
        Check(reject.Conclusion == BerkeleyConclusion.RejectMaterialSubstance && reject.Stage == BerkeleyMarkStage.ResultReject,
            "The rejection must not become a denial that objects exist.");
        Check(BerkeleyPerceivedMarkFlow.TryPrepare(reject, BerkeleyMarkAction.Leave), "A finished scene must close.");
        BerkeleyPerceivedMarkFlow.Apply(reject, BerkeleyMarkAction.Leave);
        Check(reject.Closed && reject.IsUsable(), "A completed scene must stay replayable.");

        var witness = Observe(BerkeleyMarkAction.CheckSound);
        Check(BerkeleyPerceivedMarkFlow.TryPrepare(witness, BerkeleyMarkAction.RequestSecondObserver),
            "A second observer must be requestable.");
        BerkeleyPerceivedMarkFlow.Apply(witness, BerkeleyMarkAction.RequestSecondObserver);
        Check(witness.Conclusion == BerkeleyConclusion.SecondObserver
            && witness.Stage == BerkeleyMarkStage.ResultWitness, "The witness route must stay distinct.");

        var scope = Observe(BerkeleyMarkAction.CheckReceipt);
        Check(BerkeleyPerceivedMarkFlow.TryPrepare(scope, BerkeleyMarkAction.KeepPerceptionScope),
            "Keeping the perceptual scope must be available.");
        BerkeleyPerceivedMarkFlow.Apply(scope, BerkeleyMarkAction.KeepPerceptionScope);
        Check(scope.Conclusion == BerkeleyConclusion.PerceptionScope
            && scope.Stage == BerkeleyMarkStage.ResultScope, "The scope route must not decide the underlying entity.");

        var tampered = Observe(BerkeleyMarkAction.CheckTouch);
        tampered.Conclusion = BerkeleyConclusion.PerceptionScope;
        Check(!tampered.IsUsable(), "A conclusion without its recorded action must be rejected.");

        var paid = BerkeleyPerceivedMarkState.Create("BERKELEY:COST:1");
        BerkeleyPerceivedMarkFlow.TryPrepare(paid, BerkeleyMarkAction.CheckTouch);
        BerkeleyPerceivedMarkFlow.Apply(paid, paid.PendingAction);
        Check(paid.Cost == 0m, "Observing a channel must not cost anything.");
        BerkeleyPerceivedMarkFlow.TryPrepare(paid, BerkeleyMarkAction.RequestSecondObserver);
        BerkeleyPerceivedMarkFlow.Apply(paid, paid.PendingAction);
        Check(paid.Cost == BerkeleyPerceivedMarkState.SecondObserverGoldCost,
            "The second observer must cost the promised shop-purchase price.");

        decimal gold = 100m;
        int charges = 0;
        async Task<bool> Persist() => await Task.FromResult(true);
        var outcome = await EventGoldCostProtocol.ApplyAsync(paid,
            () => (decimal?)gold, () => { gold -= paid.Cost; charges++; return Task.CompletedTask; }, Persist);
        Check(outcome == EventGoldCostOutcome.Settled && gold == 25m && charges == 1,
            "The second observer must charge exactly once.");
        var again = await EventGoldCostProtocol.ApplyAsync(paid,
            () => (decimal?)gold, () => { gold -= paid.Cost; charges++; return Task.CompletedTask; }, Persist);
        Check(again == EventGoldCostOutcome.Settled && gold == 25m && charges == 1,
            "Replaying the settled charge must not take gold twice.");

        // A record written before this outcome had a price already holds the
        // result it gave. Restoring it must settle, not take 75 gold afterwards.
        var legacy = BerkeleyPerceivedMarkState.Create("BERKELEY:LEGACY:1");
        BerkeleyPerceivedMarkFlow.TryPrepare(legacy, BerkeleyMarkAction.CheckTouch);
        BerkeleyPerceivedMarkFlow.Apply(legacy, legacy.PendingAction);
        BerkeleyPerceivedMarkFlow.TryPrepare(legacy, BerkeleyMarkAction.RequestSecondObserver);
        BerkeleyPerceivedMarkFlow.Apply(legacy, legacy.PendingAction);
        legacy.Version = 1;
        legacy.CostAttempted = false;
        legacy.CostPaid = false;
        legacy.CostGoldBefore = legacy.CostGoldAfter = EventGoldCostProtocol.NoGoldSnapshot;
        legacy.NormalizeAfterLoad();
        decimal legacyGold = 100m;
        int legacyCharges = 0;
        var legacyOutcome = await EventGoldCostProtocol.ApplyAsync(legacy,
            () => (decimal?)legacyGold, () => { legacyGold -= legacy.Cost; legacyCharges++; return Task.CompletedTask; }, Persist);
        Check(legacyOutcome == EventGoldCostOutcome.Settled && legacyGold == 100m && legacyCharges == 0
            && legacy.Version == BerkeleyPerceivedMarkState.CurrentVersion && legacy.IsUsable(),
            "A record written before the price must settle without charging the result it already gave.");

        // An old invitation upgraded into the priced choice is a new decision: it
        // keeps the payment window and is charged exactly once.
        var upgraded = BerkeleyPerceivedMarkState.Create("BERKELEY:UPGRADE:1");
        upgraded.Version = 1;
        upgraded.CostGoldBefore = upgraded.CostGoldAfter = EventGoldCostProtocol.NoGoldSnapshot;
        upgraded.NormalizeAfterLoad();
        Check(!upgraded.CostPaid, "An old invitation must not be settled as already paid.");
        BerkeleyPerceivedMarkFlow.TryPrepare(upgraded, BerkeleyMarkAction.CheckTouch);
        BerkeleyPerceivedMarkFlow.Apply(upgraded, upgraded.PendingAction);
        BerkeleyPerceivedMarkFlow.TryPrepare(upgraded, BerkeleyMarkAction.RequestSecondObserver);
        BerkeleyPerceivedMarkFlow.Apply(upgraded, upgraded.PendingAction);
        decimal upgradedGold = 100m;
        int upgradedCharges = 0;
        var upgradedOutcome = await EventGoldCostProtocol.ApplyAsync(upgraded,
            () => (decimal?)upgradedGold, () => { upgradedGold -= upgraded.Cost; upgradedCharges++; return Task.CompletedTask; }, Persist);
        var upgradedAgain = await EventGoldCostProtocol.ApplyAsync(upgraded,
            () => (decimal?)upgradedGold, () => { upgradedGold -= upgraded.Cost; upgradedCharges++; return Task.CompletedTask; }, Persist);
        Check(upgradedOutcome == EventGoldCostOutcome.Settled && upgradedAgain == EventGoldCostOutcome.Settled
            && upgradedGold == 25m && upgradedCharges == 1,
            "An upgraded old invitation must pay the second observer exactly once.");

        var wrongVersion = Observe(BerkeleyMarkAction.CheckTouch);
        wrongVersion.Version = BerkeleyPerceivedMarkState.CurrentVersion + 1;
        Check(!wrongVersion.IsUsable(), "An unknown state version must be rejected.");

        var pending = Observe(BerkeleyMarkAction.CheckTouch);
        pending.PendingAction = BerkeleyMarkAction.Leave;
        Check(!pending.IsUsable(), "A pending action outside the stage must be rejected.");

        var locke = LockeCargoIdentityState.Create("LOCKE_CARGO_IDENTITY:TEST:1");
        Check(locke.Berkeley is { Closed: false }, "A new Locke record must carry the Berkeley segment.");
        Check(LockeCargoIdentityFlow.Options(LockeCargoStage.ResultMemory).Contains(LockeCargoAction.InspectScreen),
            "The result page must offer the in-batch segment.");
        Check(!LockeCargoIdentityFlow.TryApplyBerkeley(locke, BerkeleyMarkAction.CheckTouch),
            "The segment must not open before the player inspects the screen.");
        locke.Inspecting = true;
        Check(LockeCargoIdentityFlow.TryApplyBerkeley(locke, BerkeleyMarkAction.CheckTouch),
            "The inspection must accept one observation channel.");
        Check(LockeCargoIdentityFlow.TryApplyBerkeley(locke, BerkeleyMarkAction.RejectMaterialSubstance),
            "The inspection must accept a narrow conclusion.");
        Check(LockeCargoIdentityFlow.TryApplyBerkeley(locke, BerkeleyMarkAction.Leave) && !locke.Inspecting,
            "Leaving the segment must return to the Locke record.");
        Check(locke.Berkeley is { Closed: true } && locke.IsUsable(),
            "The finished in-batch segment must stay replayable.");
        Check(!LockeCargoIdentityFlow.TryApplyBerkeley(locke, BerkeleyMarkAction.CheckTouch),
            "A closed segment must not accept a second observation.");
    }
}
