using STS2Philosophers;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class LutherSignedCareChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static PhilosophyRunState Reload(PhilosophyRunState s) => PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(s));
    private static PhilosophyRunState Fixture(WesternRouteGraph graph)
    {
        var journey = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_019"), "Actual first-act challenge.");
        var predecessor = AugustinePromiseState.Create("AUGUSTINE:TEST:2");
        foreach (var action in new[] { AugustinePromiseAction.AcceptInvitation, AugustinePromiseAction.PromisePublic,
            AugustinePromiseAction.RepairPublic, AugustinePromiseAction.Leave })
            Check(AugustinePromiseFlow.TryPrepare(predecessor, action)
                && AugustinePromiseFlow.TryCommitPending(predecessor, journey, graph), "Actual adopted Augustine closes.");
        return Reload(new PhilosophyRunState { WesternJourney = journey, AugustinePromise = predecessor,
            LutherSignedCare = LutherSignedCareState.Create("LUTHER:TEST:2", predecessor.InstanceId) });
    }
    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph, LutherSignedCareAction action)
    {
        Check(LutherSignedCareFlow.TryPrepare(shared.LutherSignedCare!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(LutherSignedCareFlow.TryCommitPending(shared.LutherSignedCare!, shared.WesternJourney!, shared.AugustinePromise!, graph), $"Saved commit {action}.");
        Check(!LutherSignedCareFlow.TryCommitPending(shared.LutherSignedCare!, shared.WesternJourney!, shared.AugustinePromise!, graph), "No repeated commit.");
        shared = Reload(shared);
        Check(LutherSignedCareFlow.IsRouteConsistent(shared.LutherSignedCare!, shared.WesternJourney!, shared.AugustinePromise!), "Saved route matches history.");
        return shared;
    }
    public static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        var declined = Commit(Commit(Fixture(graph), graph, LutherSignedCareAction.Decline), graph, LutherSignedCareAction.Leave);
        Check(!declined.WesternJourney!.ResolvedQuestionActs.Contains(2), "Refusal preserves the second-act question.");
        foreach (var declaration in new[] { LutherSignedCareAction.ReadText, LutherSignedCareAction.CheckScope, LutherSignedCareAction.SignAll })
        {
            var kept = Fixture(graph);
            foreach (var action in new[] { LutherSignedCareAction.Accept, declaration, LutherSignedCareAction.KeepSignature, LutherSignedCareAction.Leave }) kept = Commit(kept, graph, action);
            Check(kept.LutherSignedCare!.Aid == LutherAid.None && !kept.LutherSignedCare.EdgeCommitted
                && AugustinePromiseFlow.IsRouteConsistent(kept.AugustinePromise!, kept.WesternJourney!), "Keeping the signature retains actual Augustine benefits without claiming aid.");
            foreach (var response in new[] { LutherSignedCareAction.RefuseAndHelp, LutherSignedCareAction.LimitAndHelp })
            foreach (var aid in new[] { LutherSignedCareAction.CarryWater, LutherSignedCareAction.CarryBedding })
            {
                var shared = Fixture(graph);
                foreach (var action in new[] { LutherSignedCareAction.Accept, declaration, response }) shared = Commit(shared, graph, action);
                Check(!shared.LutherSignedCare!.EdgeCommitted && shared.LutherSignedCare.Aid == LutherAid.None,
                    "A declaration of conscience alone cannot switch or claim service.");
                Check(!LutherSignedCareFlow.TryPrepare(shared.LutherSignedCare, declaration), "The original declaration cannot be rewritten after disclosure.");
                shared = Commit(Commit(shared, graph, aid), graph, LutherSignedCareAction.Leave);
                Check(shared.LutherSignedCare!.Closed && shared.LutherSignedCare.EdgeCommitted
                    && shared.WesternJourney!.CurrentNodeId == LutherSignedCareState.TargetNodeId
                    && shared.WesternJourney.LastFixedAct == 2 && shared.WesternJourney.ResolvedQuestionActs.SetEquals([1, 2])
                    && !AugustinePromiseFlow.IsRouteConsistent(shared.AugustinePromise!, shared.WesternJourney)
                    && !graph.TryApply(shared.WesternJourney, "WESTERN_EDGE_021") && graph.CanFinish(shared.WesternJourney),
                    "Only performed aid switches the real question route, stopping old bonus eligibility and excluding 021.");
            }
        }
        var invalid = Fixture(graph); invalid.WesternJourney!.ResolvedQuestionActs.Add(2);
        Check(!LutherSignedCareFlow.IsRouteConsistent(invalid.LutherSignedCare!, invalid.WesternJourney, invalid.AugustinePromise!), "A spent question cannot be reclaimed.");
        invalid = Fixture(graph); invalid.AugustinePromise!.InstanceId = "OTHER";
        Check(!LutherSignedCareFlow.IsRouteConsistent(invalid.LutherSignedCare!, invalid.WesternJourney!, invalid.AugustinePromise), "Exact predecessor identity is required.");
        foreach (string field in new[] { "Version", "Closed", "Stage", "PendingAction", "ConfirmedActions", "AugustineInstanceId" })
        {
            JsonObject node = JsonSerializer.SerializeToNode(Fixture(graph).LutherSignedCare)!.AsObject(); node.Remove(field);
            Check(!node.Deserialize<LutherSignedCareState>()!.IsUsable(), $"Missing {field} remains invalid.");
        }
        Console.WriteLine("Luther draft state checks passed: sixteen saved paths, no aid/no switch, original declaration, actual predecessor, opportunity/021 exclusion and old bonus eligibility; no production event or native delivery claimed.");
    }
}
