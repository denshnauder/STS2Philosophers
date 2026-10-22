using System.Text.Json;
using STS2Philosophers;

internal static class BaconSampleLedgerChecks
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static PhilosophyRunState Reload(PhilosophyRunState state) => PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));
    private static PhilosophyRunState Fixture(WesternRouteGraph graph, OckhamNamesAction decision)
    {
        var journey = graph.Start(BaconSampleLedgerState.SourceNodeId);
        Check(graph.TryReserveQuestion(journey, 1), "First language question is really reserved.");
        var gorgias = GorgiasSpeechInquiryState.Create("GORGIAS:BACON:1");
        foreach (var action in new[] { GorgiasSpeechInquiryAction.AcceptInvitation, GorgiasSpeechInquiryAction.AssertContents,
            GorgiasSpeechInquiryAction.SeparateBeliefFromProof, GorgiasSpeechInquiryAction.RecordBoundary, GorgiasSpeechInquiryAction.Leave })
            Check(GorgiasSpeechInquiryFlow.TryPrepare(gorgias, action) && GorgiasSpeechInquiryFlow.TryCommitPending(gorgias, journey), "Actual Gorgias predecessor closes.");
        var ockham = OckhamNamesInquiryState.Create("OCKHAM:BACON:2", gorgias.InstanceId);
        foreach (var action in new[] { OckhamNamesAction.Accept, OckhamNamesAction.CheckConcept, decision, OckhamNamesAction.Close })
            Check(OckhamNamesInquiryFlow.TryPrepare(ockham, action)
                && OckhamNamesInquiryFlow.TryCommitPending(ockham, journey, gorgias, graph), "Actual accepted Ockham closes.");
        return Reload(new() { WesternJourney = journey, GorgiasSpeechInquiry = gorgias, OckhamNamesInquiry = ockham,
            BaconSampleLedger = BaconSampleLedgerState.Create("BACON:TEST:3", ockham.InstanceId) });
    }
    private static bool Consistent(PhilosophyRunState s) => BaconSampleLedgerFlow.IsRouteConsistent(s.BaconSampleLedger!,
        s.WesternJourney!, s.OckhamNamesInquiry!, s.GorgiasSpeechInquiry!);
    private static bool CommitPending(PhilosophyRunState s, WesternRouteGraph graph) => BaconSampleLedgerFlow.TryCommitPending(
        s.BaconSampleLedger!, s.WesternJourney!, s.OckhamNamesInquiry!, s.GorgiasSpeechInquiry!, graph);
    private static PhilosophyRunState Commit(PhilosophyRunState s, WesternRouteGraph graph, BaconSampleLedgerAction action)
    {
        Check(BaconSampleLedgerFlow.TryPrepare(s.BaconSampleLedger!, action), $"Prepare {action}.");
        s = Reload(s); Check(CommitPending(s, graph), $"Saved pending {action} commits.");
        Check(!CommitPending(s, graph), "A confirmed operation cannot replay.");
        s = Reload(s); Check(Consistent(s), "Event, samples and real journey remain consistent after saved commit."); return s;
    }
    public static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        foreach (var old in new[] { OckhamNamesAction.Annotate, OckhamNamesAction.UseNamedList, OckhamNamesAction.KeepRule })
        {
            var s = Fixture(graph, old);
            Check(graph.GetEligibleEdges(s.WesternJourney!, "Fixed").Any(e => e.EdgeId == BaconSampleLedgerState.CompletionEdgeId), "A real language predecessor exposes Fixed3.");
            s = Commit(Commit(s, graph, BaconSampleLedgerAction.DeclineInvitation), graph, BaconSampleLedgerAction.Leave);
            Check(!s.BaconSampleLedger!.EdgeCommitted && s.BaconSampleLedger.Samples.Count == 2
                && s.WesternJourney!.DeclinedFixedActs.SetEquals([2, 3]) && s.WesternJourney.RetainedEnding
                && s.WesternJourney.ResolvedQuestionActs.SetEquals([1, 2]) && graph.IsComplete(s.WesternJourney)
                && OckhamNamesInquiryFlow.IsRouteConsistent(s.OckhamNamesInquiry!, BaconSampleLedgerFlow.OckhamHistoryJourney(s.WesternJourney), s.GorgiasSpeechInquiry!),
                "Refusal spends only Fixed3 and preserves the real Ockham choice through a checked historical projection.");
            foreach (var bias in new[] { BaconSampleLedgerAction.CheckTribe, BaconSampleLedgerAction.CheckCave, BaconSampleLedgerAction.CheckMarket, BaconSampleLedgerAction.CheckTheatre })
            foreach (var final in new[] { BaconSampleLedgerAction.PublishLimited, BaconSampleLedgerAction.TakeControlSample, BaconSampleLedgerAction.KeepPrediction })
            {
                s = Fixture(graph, old);
                s = Commit(s, graph, BaconSampleLedgerAction.AcceptInvitation);
                Check(!BaconSampleLedgerFlow.TryPrepare(s.BaconSampleLedger!, final), "No conclusion before an actual sample.");
                s = Commit(s, graph, bias);
                Check(s.BaconSampleLedger!.Samples.Count == 2 && !BaconSampleLedgerFlow.TryPrepare(s.BaconSampleLedger, final), "Naming a bias does not create a counterexample.");
                s = Commit(s, graph, BaconSampleLedgerAction.TakeCounterSample);
                Check(s.BaconSampleLedger!.Samples.SequenceEqual([BaconObservedSample.WarmRich, BaconObservedSample.ColdPoor, BaconObservedSample.WarmPoor]), "Only the recorded counter-sample supplies the negative instance.");
                s = Commit(Commit(s, graph, final), graph, BaconSampleLedgerAction.Leave);
                bool adopts = final != BaconSampleLedgerAction.KeepPrediction;
                Check(s.BaconSampleLedger!.Closed && s.BaconSampleLedger.EdgeCommitted == adopts
                    && s.BaconSampleLedger.Samples.Count == (final == BaconSampleLedgerAction.TakeControlSample ? 4 : 3)
                    && s.WesternJourney!.LastFixedAct == 3 && graph.IsComplete(s.WesternJourney)
                    && s.WesternJourney.CompletedEdgeIds.SetEquals(adopts ? [BaconSampleLedgerState.CompletionEdgeId] : Array.Empty<string>())
                    && s.WesternJourney.ResolvedQuestionActs.SetEquals([1, 2])
                    && s.WesternJourney.CurrentNodeId == (adopts ? BaconSampleLedgerState.AdoptedNodeId : BaconSampleLedgerState.SourceNodeId)
                    && !graph.TryApply(s.WesternJourney, BaconSampleLedgerState.CompletionEdgeId), "True Fixed3 changes the source once, leaves Q3 and the old route intact, and has a legal ending.");
                Check(graph.GetEligibleEdges(s.WesternJourney!, "Fixed").Count == 0, "No fourth fixed opportunity is created.");
            }
        }
        var shared = Fixture(graph, OckhamNamesAction.UseNamedList);
        foreach (string field in new[] { "Closed", "Version", "Stage", "PendingAction", "ConfirmedActions", "Samples", "OckhamInstanceId" })
        {
            var json = JsonSerializer.SerializeToNode(shared.BaconSampleLedger)!.AsObject(); json.Remove(field);
            Check(!json.Deserialize<BaconSampleLedgerState>()!.IsUsable(), $"Missing {field} cannot fabricate a valid sample or invitation.");
        }
        shared.BaconSampleLedger!.Samples.Add(BaconObservedSample.WarmPoor);
        Check(!shared.BaconSampleLedger.IsUsable(), "A forged negative instance cannot authorize a conclusion.");
        shared = Fixture(graph, OckhamNamesAction.UseNamedList); shared.OckhamNamesInquiry!.InstanceId = "OTHER";
        Check(!Consistent(shared), "Another Ockham instance cannot substitute for the real predecessor.");
        shared = Fixture(graph, OckhamNamesAction.UseNamedList); shared.WesternJourney!.ResolvedQuestionActs.Add(3);
        Check(!Consistent(shared), "An already occupied final reflection cannot reopen this terminal content.");
        shared = Fixture(graph, OckhamNamesAction.UseNamedList);
        shared.WesternJourney = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(!Consistent(shared) && graph.GetEligibleEdges(shared.WesternJourney, "Fixed").All(e => e.EdgeId != "WESTERN_EDGE_048"), "Bacon never steals the actual knowledge route's existing Descartes or Anselm opportunities.");
        BaconSampleLedgerTriggerContext good = new(true, true, true, 2, true, 1, BaconSampleLedgerState.SourceNodeId, 2, true, true, true, false, false, false, false, true);
        Check(BaconSampleLedgerTriggerPolicy.ShouldInsert(good), "Valid third-act fixed insertion is reachable.");
        foreach (var bad in new[] { good with { AtSafeMapBoundary = false }, good with { IsSingleplayer = false }, good with { CurrentActIndex = 1 },
            good with { SourceIsCompletedNonBossCombat = false }, good with { CompletedCombatCount = 0 }, good with { CurrentNodeId = "OTHER" },
            good with { LastFixedAct = 1 }, good with { CompletionEdgeEligible = false }, good with { HasRequiredRelic = false },
            good with { PredecessorReady = false }, good with { HasSavedInstance = true }, good with { ModelAvailable = false } })
            Check(!BaconSampleLedgerTriggerPolicy.ShouldInsert(bad), "Missing route, reward boundary or relic facts block insertion.");
        Check(BaconSampleLedgerTriggerPolicy.ShouldInsert(good with { HasSavedInstance = true, SavedInstanceUsable = true,
            SavedRouteConsistent = true, LastFixedAct = 3, PredecessorReady = false, CompletionEdgeEligible = false, SourceIsCompletedNonBossCombat = false }), "A saved result resumes without another completed fight or Fixed3.");
        Combat(); Localization();
        Console.WriteLine("Bacon checks passed: 39 saved paths across three real Ockham results, recorded counter/control samples, Fixed3/Q3 and knowledge-route protection, historical discount preservation, strict comparison receipts and bilingual pages.");
    }
    private static void Combat()
    {
        foreach (int target in new[] { 1, 2 })
        {
            var state = BaconCombatComparisonState.Create();
            Check(!state.ObserveManual("2:4:1", target, target, BaconComparisonKind.Attack) && state.Counter("2:4:1", target, target) == 1,
                "A first real sample cannot grant a comparison reward.");
            Check(BaconCombatComparisonState.TryRestore(state.Encode(), out state)
                && state.ObserveManual("2:4:1", target, target, BaconComparisonKind.Skill)
                && !state.ObserveManual("2:4:1", target, target, BaconComparisonKind.Power)
                && BaconCombatComparisonState.TryRestore(state.Encode(), out state)
                && !state.ObserveManual("2:4:1", target, target, BaconComparisonKind.Skill), "The saved comparison pays at most once per combat.");
            state = BaconCombatComparisonState.Create();
            Check(!state.ObserveManual("2:4:1", target, target, BaconComparisonKind.Skill)
                && !state.ObserveManual("2:4:1", target, target, BaconComparisonKind.Skill)
                && !state.ObserveManual("2:4:1", target, target, BaconComparisonKind.Attack), "Matching first samples cannot be repaired with a third card.");
            state = BaconCombatComparisonState.Create();
            Check(!state.ObserveManual("2:4:1", target, target + 1, BaconComparisonKind.Attack)
                && state.Counter("2:4:1", target, target + 1) == 0, "A missed target turn gives neither reward nor a waiting counter.");
        }
        foreach (string bad in new[] { "broken", "{}", "{\"Version\":99,\"Receipts\":{}}" })
            Check(!BaconCombatComparisonState.TryRestore(bad, out _), "Unknown comparison state remains inert.");
    }
    private static void Localization()
    {
        foreach (string lang in new[] { "eng", "zhs" })
        {
            using var events = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{lang}/events.json"));
            foreach (var stage in Enum.GetValues<BaconSampleLedgerStage>())
            {
                foreach (var bias in stage is BaconSampleLedgerStage.Counterexample or BaconSampleLedgerStage.Decision or BaconSampleLedgerStage.ResultLimited or BaconSampleLedgerStage.ResultControl or BaconSampleLedgerStage.ResultKept
                    ? new[] { BaconInquiryBias.Tribe, BaconInquiryBias.Cave, BaconInquiryBias.Market, BaconInquiryBias.Theatre } : new[] { BaconInquiryBias.None })
                {
                    var state = BaconSampleLedgerState.Create("l10n", "prior"); state.Stage = stage; state.Bias = bias;
                    Check(events.RootElement.TryGetProperty($"BACON_SAMPLE_LEDGER.pages.{BaconSampleLedgerFlow.DescriptionKey(state)}.description", out _), "Every real event page is bilingual.");
                }
                foreach (var action in BaconSampleLedgerFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(events.RootElement.TryGetProperty($"BACON_SAMPLE_LEDGER.pages.{BaconSampleLedgerFlow.StageKey(stage)}.options.{BaconSampleLedgerFlow.ActionKey(action)}.{suffix}", out _), "Every real event option is bilingual.");
            }
            using var relics = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{lang}/relics.json"));
            foreach (string mode in new[] { "limited", "control", "unavailable" })
                Check(relics.RootElement.TryGetProperty($"ARISTOTLE_CATEGORY_TABLET.bacon.{mode}.description", out _), "The replacement and interrupted comparison have bilingual tips.");
        }
    }
}
