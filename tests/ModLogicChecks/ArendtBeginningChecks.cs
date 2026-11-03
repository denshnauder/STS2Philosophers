using STS2Philosophers;

internal static class ArendtBeginningChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Fixture(WesternRouteGraph graph, string required = ArendtBeginningState.MillRequiredEdgeId)
    {
        string source = ArendtBeginningFlow.SourceNodeIdOf(required);
        WesternJourneyState journey = graph.Start(WollstonecraftAdmissionState.SourceNodeId);
        journey.CurrentNodeId = source;
        journey.LastFixedAct = 2;
        journey.RouteId = required == ArendtBeginningState.FoucaultRequiredEdgeId
            ? "GENEALOGY_AND_POWER" : "SYMPATHY_AND_PUBLIC_ACTION";
        journey.RouteRules = true;
        ((HashSet<string>)journey.CompletedEdgeIds).Add(required);
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            ArendtBeginning = ArendtBeginningState.Create("ARENDT_BEGINNING:TEST:3", required),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        ArendtBeginningAction action)
    {
        Check(ArendtBeginningFlow.TryPrepare(shared.ArendtBeginning!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(ArendtBeginningFlow.TryCommitPending(shared.ArendtBeginning!, shared.WesternJourney!, graph),
            $"Commit {action}.");
        shared = Reload(shared);
        Check(ArendtBeginningFlow.IsRouteConsistent(shared.ArendtBeginning!, shared.WesternJourney!),
            "Arendt survives reload.");
        return shared;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        Check(ArendtBeginningFlow.IsRouteConsistent(Fixture(graph).ArendtBeginning!,
            Fixture(graph).WesternJourney!), "Arendt opens at Mill after 175.");
        var foucault = Fixture(graph, ArendtBeginningState.FoucaultRequiredEdgeId);
        Check(ArendtBeginningFlow.IsRouteConsistent(foucault.ArendtBeginning!, foucault.WesternJourney!),
            "Arendt also opens at Foucault after 181.");

        // Whether anyone answers is fixed when the room opens.
        var answered = Fixture(graph).ArendtBeginning!.Answered;
        Check(Reload(Fixture(graph)).ArendtBeginning!.Answered == answered,
            "The answer must survive a reload unchanged.");
        Check(ArendtBeginningFlow.AnsweredOf("ARENDT_BEGINNING:A:3")
            == ArendtBeginningFlow.AnsweredOf("ARENDT_BEGINNING:A:3"),
            "The answer is a stable function of the instance id.");

        // Finishing alone earns supplies and begins nothing public.
        PhilosophyRunState alone = Commit(Fixture(graph), graph, ArendtBeginningAction.ObserveWaypoint);
        alone = Commit(alone, graph, ArendtBeginningAction.FinishAlone);
        Check(alone.ArendtBeginning!.Judgment == ArendtJudgment.Alone
            && alone.WesternJourney!.CompletedEdgeIds.Contains(ArendtBeginningState.MillCompletionEdgeId),
            "Finishing the tools alone enters Arendt's node and completes 176.");
        alone = Commit(alone, graph, ArendtBeginningAction.Leave);
        Check(alone.ArendtBeginning!.Closed, "Arendt closes after Leave.");

        // An unanswered invitation is recorded as an attempt, not as a success.
        PhilosophyRunState invited = Commit(Fixture(graph), graph, ArendtBeginningAction.ObserveWaypoint);
        invited = Commit(invited, graph, ArendtBeginningAction.InviteProposal);
        Check(invited.ArendtBeginning!.Judgment == ArendtJudgment.Invited
            && invited.WesternJourney!.CompletedEdgeIds.Contains(ArendtBeginningState.MillCompletionEdgeId),
            "Inviting a proposal enters the node whatever the others answer.");
        string key = ArendtBeginningFlow.DescriptionKey(invited.ArendtBeginning);
        Check(key == (invited.ArendtBeginning.Answered ? "RESULT_INVITED_RESPONDED" : "RESULT_INVITED_SILENT"),
            "The result must say whether anyone actually answered.");

        // Signing the standing order keeps the old arrangement.
        PhilosophyRunState signed = Commit(Fixture(graph), graph, ArendtBeginningAction.ObserveWaypoint);
        signed = Commit(signed, graph, ArendtBeginningAction.SignOrder);
        Check(signed.ArendtBeginning!.Judgment == ArendtJudgment.Signed
            && signed.WesternJourney!.CompletedEdgeIds.Contains(ArendtBeginningState.MillCompletionEdgeId),
            "Signing the order completes the room and records the risk.");

        PhilosophyRunState tampered = Fixture(graph);
        tampered.ArendtBeginning!.ConfirmedActions.Add(ArendtBeginningAction.InviteProposal);
        Check(!tampered.ArendtBeginning.IsUsable(), "An outcome without its recorded actions must be rejected.");
        Check(ArendtBeginningState.Create("ARENDT_BEGINNING:TEST:3", "WESTERN_EDGE_999").RequiredEdgeId
            == ArendtBeginningState.MillRequiredEdgeId,
            "An unknown line falls back to the Mill edge and is then rejected by the route check.");
    }
}
