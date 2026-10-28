using STS2Philosophers;

internal static class HegelSubstanceSubjectChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture()
    {
        var journey = new WesternJourneyState
        {
            CurrentNodeId = HegelSubstanceSubjectState.SourceNodeId,
            LastFixedAct = 2,
        };
        journey.CompletedEdgeIds.Add(HegelSubstanceSubjectState.RequiredEdgeId);
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            HegelSubstanceSubject = HegelSubstanceSubjectState.Create("HEGEL_SUBSTANCE_SUBJECT:TEST:3"),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        HegelSubstanceSubjectAction action)
    {
        Check(HegelSubstanceSubjectFlow.TryPrepare(shared.HegelSubstanceSubject!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(HegelSubstanceSubjectFlow.TryCommitPending(shared.HegelSubstanceSubject!,
            shared.WesternJourney!, graph), $"Commit {action}.");
        Check(!HegelSubstanceSubjectFlow.TryCommitPending(shared.HegelSubstanceSubject!,
            shared.WesternJourney!, graph), "A confirmed Hegel action cannot replay.");
        shared = Reload(shared);
        Check(HegelSubstanceSubjectFlow.IsRouteConsistent(shared.HegelSubstanceSubject!,
            shared.WesternJourney!), "Hegel survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        PhilosophyRunState shared = Fixture();
        Check(HegelSubstanceSubjectFlow.IsRouteConsistent(shared.HegelSubstanceSubject!, shared.WesternJourney!),
            "Hegel opens at Spinoza after 024.");

        shared = Commit(shared, graph, HegelSubstanceSubjectAction.InspectLedger);
        Check(shared.HegelSubstanceSubject!.LedgerInspected,
            "The fixed ledger is inspected before adoption.");
        shared = Commit(shared, graph, HegelSubstanceSubjectAction.AdoptSubject);
        Check(shared.WesternJourney!.CompletedEdgeIds.Contains(HegelSubstanceSubjectState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == HegelSubstanceSubjectState.AdoptedNodeId
            && shared.WesternJourney.LastFixedAct == 3
            && shared.HegelSubstanceSubject!.EdgeCommitted,
            "Adopting substance as subject completes 025 and enters Hegel.");
        shared = Commit(shared, graph, HegelSubstanceSubjectAction.Leave);
        Check(shared.HegelSubstanceSubject!.Closed, "Hegel closes after Leave.");

        shared = Fixture();
        shared = Commit(shared, graph, HegelSubstanceSubjectAction.KeepSpinoza);
        Check(shared.HegelSubstanceSubject!.FixedDeclined
            && !shared.WesternJourney!.CompletedEdgeIds.Contains(HegelSubstanceSubjectState.CompletionEdgeId)
            && shared.WesternJourney.CurrentNodeId == HegelSubstanceSubjectState.SourceNodeId
            && shared.WesternJourney.LastFixedAct == 3
            && shared.WesternJourney.DeclinedFixedActs.Contains(3),
            "Declining Hegel spends only the act-three fixed slot.");
        shared = Commit(shared, graph, HegelSubstanceSubjectAction.Leave);

        HegelSubstanceSubjectTriggerContext context = new(true, true, true, 2, true, 1,
            HegelSubstanceSubjectState.SourceNodeId, 2, true, true, false, false, false, false, true);
        Check(HegelSubstanceSubjectTriggerPolicy.ShouldInsert(context),
            "Hegel is offered after a real act-three combat.");
        Check(!HegelSubstanceSubjectTriggerPolicy.ShouldInsert(context with { CurrentActIndex = 1 }),
            "Hegel is not an act-two event.");
        Check(!HegelSubstanceSubjectTriggerPolicy.ShouldInsert(context with { RequiredEdgeCompleted = false }),
            "Hegel requires 024.");
        Check(!HegelSubstanceSubjectTriggerPolicy.ShouldInsert(context with { CompletionEdgeEligible = false }),
            "Hegel requires an eligible 025.");
    }
}
