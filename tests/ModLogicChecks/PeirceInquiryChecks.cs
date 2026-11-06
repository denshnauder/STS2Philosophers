using STS2Philosophers;

internal static class PeirceInquiryChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static WesternJourneyState Journey() => new()
    {
        CurrentNodeId = PeirceInquiryState.SourceNodeId,
        LastFixedAct = 2,
    };

    internal static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var state = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:1");
        Check(PeirceInquiryFlow.Options(state.Stage).Count == 3,
            "Schelling must offer visible spirit, mechanism, or keeping Hume.");
        Check(PeirceInquiryFlow.TryPrepare(state, PeirceInquiryAction.TestByConsequences),
            "Nature as spirit must be selectable.");
        var journey = Journey();
        Check(PeirceInquiryFlow.IsRouteConsistent(state, journey),
            "The room must sit on the second-act fixed slot.");
        Check(PeirceInquiryFlow.TryCommitPending(state, journey, graph),
            "Nature as spirit must take the second-act fixed link.");
        Check(state.Standard == PeirceInquiryStandard.Consequences && state.EdgeCommitted
            && state.Stage == PeirceInquiryStage.ResultTest,
            "Spirit must record its standard and stage.");
        Check(journey.CompletedEdgeIds.Contains(PeirceInquiryState.CompletionEdgeId)
            && journey.CurrentNodeId == PeirceInquiryState.AdoptedNodeId && journey.LastFixedAct == 3,
            "Adopting must move the journey to Schelling and consume act two.");

        var kept = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:2");
        PeirceInquiryFlow.TryPrepare(kept, PeirceInquiryAction.KeepHume);
        var declined = Journey();
        Check(PeirceInquiryFlow.TryCommitPending(kept, declined, graph),
            "Keeping Hume must decline the act's fixed slot.");
        Check(declined.DeclinedFixedActs.Contains(2)
            && declined.CurrentNodeId == PeirceInquiryState.SourceNodeId,
            "Declining must leave the player on Hume's node.");

        var jamesHost = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:J");
        PeirceInquiryFlow.TryPrepare(jamesHost, PeirceInquiryAction.TestByConsequences);
        var jamesJourney = new WesternJourneyState
        {
            CurrentNodeId = PeirceInquiryState.SourceNodeId,
            LastFixedAct = 2,
        };
        PeirceInquiryFlow.TryCommitPending(jamesHost, jamesJourney, graph);
        Check(PeirceInquiryFlow.TryPrepare(jamesHost, PeirceInquiryAction.AskJames),
            "The result page must offer James' in-batch segment.");
        PeirceInquiryFlow.Apply(jamesHost, PeirceInquiryAction.AskJames);
        JamesPragmatismState james = jamesHost.James!;
        Check(jamesHost.InspectingJames && james.IsUsable(),
            "Asking James must open a usable in-batch segment.");
        Check(JamesPragmatismFlow.TryPrepare(james, JamesPragmatismAction.TestPublicly),
            "James must offer a public test or a private hypothesis.");
        JamesPragmatismFlow.Apply(james, JamesPragmatismAction.TestPublicly);
        Check(james.Standard == JamesPragmatismStandard.Public
            && james.Stage == JamesPragmatismStage.ResultPublic,
            "The public test must record its standard and stage.");
        JamesPragmatismFlow.TryPrepare(james, JamesPragmatismAction.Leave);
        JamesPragmatismFlow.Apply(james, JamesPragmatismAction.Leave);
        Check(james.Closed, "Leaving James must close the segment.");

        var tampered = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:3");
        tampered.Standard = PeirceInquiryStandard.Community;
        Check(!tampered.IsUsable(), "A standard without its recorded action must be rejected.");

        CheckClosedJamesReentryRecovery();
        CheckLegacyOpenJamesSegmentContinues();
    }

    private static PeirceInquiryState RoundTrip(PeirceInquiryState state)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(state);
        return System.Text.Json.JsonSerializer.Deserialize<PeirceInquiryState>(json)!;
    }

    private static void CheckClosedJamesReentryRecovery()
    {
        var state = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:JSON1");
        PeirceInquiryFlow.TryPrepare(state, PeirceInquiryAction.TestByConsequences);
        PeirceInquiryFlow.Apply(state, PeirceInquiryAction.TestByConsequences);
        PeirceInquiryFlow.TryPrepare(state, PeirceInquiryAction.AskJames);
        PeirceInquiryFlow.Apply(state, PeirceInquiryAction.AskJames);
        JamesPragmatismFlow.TryPrepare(state.James!, JamesPragmatismAction.TestPublicly);
        JamesPragmatismFlow.Apply(state.James!, JamesPragmatismAction.TestPublicly);
        JamesPragmatismFlow.TryPrepare(state.James!, JamesPragmatismAction.Leave);
        JamesPragmatismFlow.Apply(state.James!, JamesPragmatismAction.Leave);
        Check(state.James!.Closed && state.InspectingJames,
            "The old deadlock must be reproduced before recovery.");

        PeirceInquiryState restored = RoundTrip(state);
        Check(!restored.InspectingJames && restored.James is { Closed: true },
            "A closed nested segment must clear the stale inspection flag on load.");
        Check(restored.IsUsable(), "The recovered closed-segment state must remain usable.");
        Check(!PeirceInquiryFlow.TryPrepare(restored, PeirceInquiryAction.AskJames),
            "A closed nested segment must not be re-entered.");

        var pending = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:JSON2");
        PeirceInquiryFlow.TryPrepare(pending, PeirceInquiryAction.TestByConsequences);
        PeirceInquiryFlow.Apply(pending, PeirceInquiryAction.TestByConsequences);
        PeirceInquiryFlow.TryPrepare(pending, PeirceInquiryAction.AskJames);
        PeirceInquiryFlow.Apply(pending, PeirceInquiryAction.AskJames);
        JamesPragmatismFlow.TryPrepare(pending.James!, JamesPragmatismAction.TestPublicly);
        JamesPragmatismFlow.Apply(pending.James!, JamesPragmatismAction.TestPublicly);
        JamesPragmatismFlow.TryPrepare(pending.James!, JamesPragmatismAction.Leave);
        JamesPragmatismFlow.Apply(pending.James!, JamesPragmatismAction.Leave);
        pending.PendingAction = PeirceInquiryAction.AskJames;
        pending.InspectingJames = true;
        int confirmedBefore = pending.ConfirmedActions.Count;

        PeirceInquiryState restoredPending = RoundTrip(pending);
        Check(PeirceInquiryFlow.TryCancelClosedJamesReentry(restoredPending),
            "A restored pending AskJames on a closed segment must be cancelled.");
        Check(restoredPending.PendingAction == PeirceInquiryAction.None
            && !restoredPending.InspectingJames
            && restoredPending.ConfirmedActions.Count == confirmedBefore,
            "Cancelling must return to the parent result without recording an action.");
        Check(restoredPending.IsUsable(), "The cancelled state must remain usable.");
    }

    private static void CheckLegacyOpenJamesSegmentContinues()
    {
        var state = PeirceInquiryState.Create("PEIRCE_INQUIRY:TEST:JSON3");
        PeirceInquiryFlow.TryPrepare(state, PeirceInquiryAction.TestByConsequences);
        PeirceInquiryFlow.Apply(state, PeirceInquiryAction.TestByConsequences);
        PeirceInquiryFlow.TryPrepare(state, PeirceInquiryAction.AskJames);
        PeirceInquiryFlow.Apply(state, PeirceInquiryAction.AskJames);
        JamesPragmatismFlow.TryPrepare(state.James!, JamesPragmatismAction.TestPublicly);
        JamesPragmatismFlow.Apply(state.James!, JamesPragmatismAction.TestPublicly);

        PeirceInquiryState restored = RoundTrip(state);
        Check(restored.InspectingJames && restored.James is { Closed: false },
            "An open legacy nested segment must keep its inspection flag.");
        Check(PeirceInquiryFlow.Options(restored.Stage).Contains(PeirceInquiryAction.AskJames),
            "The parent must still offer AskJames for an open nested segment.");
        Check(PeirceInquiryFlow.TryPrepare(restored, PeirceInquiryAction.AskJames),
            "An open legacy nested segment must remain re-enterable.");
    }
}
