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

    internal static void Run()
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
