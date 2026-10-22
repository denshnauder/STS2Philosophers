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
        TriggerGates();
        Localization();
        Console.WriteLine("Luther checks passed: sixteen saved paths, no aid/no switch, original declaration, actual predecessor, opportunity/021 exclusion and old bonus eligibility; native trigger gates and bilingual event/choice resources.");
    }
    private static void TriggerGates()
    {
        LutherSignedCareTriggerContext eligible = new(true, true, true, 1, true, 1,
            LutherSignedCareState.SourceNodeId, 2, false, true, true, true, true,
            false, false, false, false, true);
        Check(LutherSignedCareTriggerPolicy.ShouldInsert(eligible), "The real completed combat after Augustine admits Luther.");
        foreach (var blocked in new[] { eligible with { AtSafeMapBoundary = false }, eligible with { IsSingleplayer = false },
            eligible with { CurrentActIndex = 0 }, eligible with { CurrentActIndex = 2 },
            eligible with { SourceIsCompletedNonBossCombat = false }, eligible with { CompletedCombatCount = 0 },
            eligible with { LastFixedAct = 1 }, eligible with { QuestionResolved = true },
            eligible with { HasRequiredRelic = false }, eligible with { PredecessorAdoptedAndClosed = false },
            eligible with { PredecessorRouteConsistent = false }, eligible with { EdgeEligible = false },
            eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true } })
            Check(!LutherSignedCareTriggerPolicy.ShouldInsert(blocked), "A missing native/route fact blocks a new invitation.");
        var recovery = eligible with { HasSavedInstance = true, SavedInstanceUsable = true,
            SavedRouteConsistent = true, QuestionResolved = true, EdgeEligible = false,
            PredecessorRouteConsistent = false, SourceIsCompletedNonBossCombat = false };
        Check(LutherSignedCareTriggerPolicy.ShouldInsert(recovery)
            && !LutherSignedCareTriggerPolicy.ShouldInsert(recovery with { SavedInstanceClosed = true })
            && !LutherSignedCareTriggerPolicy.ShouldInsert(recovery with { SavedRouteConsistent = false }),
            "Only the exact open saved route resumes; spent opportunity is not reclaimed.");
    }
    private static void Localization()
    {
        foreach (string language in new[] { "eng", "zhs" })
        {
            using var events = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<LutherSignedCareStage>())
            foreach (var declaration in stage == LutherSignedCareStage.Need
                ? new[] { LutherDeclaration.Read, LutherDeclaration.Checked, LutherDeclaration.Signed } : new[] { LutherDeclaration.None })
            foreach (var response in stage == LutherSignedCareStage.Service
                ? new[] { LutherResponse.Refused, LutherResponse.Limited } : new[] { LutherResponse.None })
            foreach (var aid in stage == LutherSignedCareStage.ResultSwitched
                ? new[] { LutherAid.Water, LutherAid.Bedding } : new[] { LutherAid.None })
            {
                var state = LutherSignedCareState.Create("translation", "predecessor");
                state.Stage = stage; state.Declaration = declaration; state.Response = response; state.Aid = aid;
                Check(events.RootElement.TryGetProperty($"LUTHER_SIGNED_CARE.pages.{LutherSignedCareFlow.DescriptionKey(state)}.description", out _),
                    $"Missing {language} {LutherSignedCareFlow.DescriptionKey(state)}.");
                foreach (var action in LutherSignedCareFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(events.RootElement.TryGetProperty($"LUTHER_SIGNED_CARE.pages.{LutherSignedCareFlow.StageKey(stage)}.options.{LutherSignedCareFlow.ActionKey(action)}.{suffix}", out _),
                        "Every actual option is bilingual.");
            }
            using var relics = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/relics.json"));
            Check(relics.RootElement.TryGetProperty("PLATO_CIVIC_SEAL.luther.description", out _), "Luther's actual cost has a bilingual relic tip.");
        }
    }

}
