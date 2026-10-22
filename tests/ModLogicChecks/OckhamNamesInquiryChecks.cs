using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class OckhamNamesInquiryChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static PhilosophyRunState Reload(PhilosophyRunState state) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));
    private static PhilosophyRunState Fixture(WesternRouteGraph graph)
    {
        WesternJourneyState journey = graph.Start(OckhamNamesInquiryState.RequiredNodeId);
        Check(graph.TryReserveQuestion(journey, 1), "Real first-act language question reserved.");
        GorgiasSpeechInquiryState gorgias = GorgiasSpeechInquiryState.Create("GORGIAS:TEST:1");
        foreach (var action in new[] { GorgiasSpeechInquiryAction.AcceptInvitation,
            GorgiasSpeechInquiryAction.AssertContents, GorgiasSpeechInquiryAction.SeparateBeliefFromProof,
            GorgiasSpeechInquiryAction.RecordBoundary, GorgiasSpeechInquiryAction.Leave })
            Check(GorgiasSpeechInquiryFlow.TryPrepare(gorgias, action)
                && GorgiasSpeechInquiryFlow.TryCommitPending(gorgias, journey), "Real Gorgias history closes.");
        return Reload(new PhilosophyRunState { WesternJourney = journey, GorgiasSpeechInquiry = gorgias,
            OckhamNamesInquiry = OckhamNamesInquiryState.Create("OCKHAM:TEST:2", gorgias.InstanceId) });
    }
    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph, OckhamNamesAction action)
    {
        Check(OckhamNamesInquiryFlow.TryPrepare(shared.OckhamNamesInquiry!, action), $"Prepare {action}.");
        shared = Reload(shared);
        Check(OckhamNamesInquiryFlow.TryCommitPending(shared.OckhamNamesInquiry!, shared.WesternJourney!,
            shared.GorgiasSpeechInquiry!, graph), $"Commit {action} after saved preparation.");
        Check(!OckhamNamesInquiryFlow.TryCommitPending(shared.OckhamNamesInquiry!, shared.WesternJourney!,
            shared.GorgiasSpeechInquiry!, graph), "A confirmed action is inert.");
        shared = Reload(shared);
        Check(OckhamNamesInquiryFlow.IsRouteConsistent(shared.OckhamNamesInquiry!, shared.WesternJourney!,
            shared.GorgiasSpeechInquiry!), "Event and route agree after reload.");
        return shared;
    }
    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        var shared = Commit(Commit(Fixture(graph), graph, OckhamNamesAction.Decline), graph, OckhamNamesAction.Close);
        Check(shared.WesternJourney!.LastFixedAct == 1 && !shared.WesternJourney.ResolvedQuestionActs.Contains(2),
            "Declining preserves both second-act opportunities.");
        foreach (var audit in new[] { OckhamNamesAction.CheckWorkers, OckhamNamesAction.CheckConcept, OckhamNamesAction.CheckInscription })
        foreach (var decision in new[] { OckhamNamesAction.Annotate, OckhamNamesAction.UseNamedList, OckhamNamesAction.KeepRule })
        {
            shared = Fixture(graph);
            foreach (var action in new[] { OckhamNamesAction.Accept, audit, decision, OckhamNamesAction.Close })
                shared = Commit(shared, graph, action);
            Check(shared.OckhamNamesInquiry!.Closed && shared.OckhamNamesInquiry.Audit != OckhamNamesAudit.None
                && shared.WesternJourney!.DeclinedFixedActs.SetEquals([2])
                && shared.WesternJourney.ResolvedQuestionActs.SetEquals([1, 2])
                && shared.WesternJourney.CompletedEdgeIds.Count == 0
                && shared.WesternJourney.CurrentNodeId == OckhamNamesInquiryState.RequiredNodeId,
                "Exactly one local critique, no fabricated 029/030 or doctrine replacement.");
        }
        shared = Fixture(graph);
        foreach (string field in new[] { "Closed", "ConfirmedActions", "GorgiasInstanceId", "Stage", "PendingAction" })
        {
            JsonObject saved = JsonSerializer.SerializeToNode(shared.OckhamNamesInquiry)!.AsObject();
            saved.Remove(field);
            Check(!saved.Deserialize<OckhamNamesInquiryState>()!.IsUsable(), $"Missing {field} cannot reopen an event.");
        }
        shared.OckhamNamesInquiry!.Disposition = OckhamNamesDisposition.Named;
        Check(!shared.OckhamNamesInquiry.IsUsable(), "A forged reward without action history is rejected.");
        shared = Fixture(graph); shared.GorgiasSpeechInquiry!.InstanceId = "OTHER";
        Check(!OckhamNamesInquiryFlow.IsRouteConsistent(shared.OckhamNamesInquiry!, shared.WesternJourney!, shared.GorgiasSpeechInquiry),
            "A different predecessor instance cannot authorize this event.");
        shared = Fixture(graph); shared.WesternJourney!.ResolvedQuestionActs.Add(2);
        Check(!OckhamNamesInquiryFlow.TryPrepare(shared.OckhamNamesInquiry!, OckhamNamesAction.UseNamedList)
            && !OckhamNamesInquiryFlow.IsRouteConsistent(shared.OckhamNamesInquiry!, shared.WesternJourney!, shared.GorgiasSpeechInquiry!),
            "An out-of-order choice and occupied question cannot be reused.");
        OckhamNamesInquiryTriggerContext eligible = new(true, true, true, 1, true, 1,
            OckhamNamesInquiryState.RequiredNodeId, 1, true, false, true, true, true, false, false, false, false, true);
        Check(OckhamNamesInquiryTriggerPolicy.ShouldInsert(eligible), "Real language critique is reachable.");
        foreach (var blocked in new[] { eligible with { AtSafeMapBoundary = false }, eligible with { IsSingleplayer = false },
            eligible with { CurrentActIndex = 0 }, eligible with { CurrentActIndex = 2 },
            eligible with { SourceIsCompletedNonBossCombat = false }, eligible with { LastFixedAct = 2 },
            eligible with { SecondQuestionResolved = true }, eligible with { HasRequiredRelic = false },
            eligible with { GorgiasClosed = false }, eligible with { GorgiasRouteConsistent = false },
            eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true } })
            Check(!OckhamNamesInquiryTriggerPolicy.ShouldInsert(blocked), "Missing native/route fact blocks insertion.");
        Check(OckhamNamesInquiryTriggerPolicy.ShouldInsert(eligible with { HasSavedInstance = true, SavedInstanceUsable = true,
            SavedRouteConsistent = true, LastFixedAct = 2, SecondQuestionResolved = true, GorgiasRouteConsistent = false,
            SourceIsCompletedNonBossCombat = false }), "Exact saved event resumes without reclaiming its question.");
        CheckPractice();
        CheckLocalization();
        Console.WriteLine("Ockham checks passed: ten saved paths, real language predecessor/opportunity gates, strict history, named-card discounts and bilingual pages.");
    }
    private static void CheckPractice()
    {
        WesternEntryPracticeState practice = new();
        practice.BeginCombat(WesternEntryPracticeState.LanguageAndMeaning); practice.BeginTurn(1);
        WesternEntryPlayResolution Play(string receipt, string model, decimal cost, bool named, bool x = false)
        {
            var quote = practice.GetCostChange(model, WesternPracticeCardKind.Attack, cost, x, false, namedCategory: named);
            return practice.RecordManualPlay(receipt, model, WesternPracticeCardKind.Attack, cost, x, false, false,
                appliedCostEffect: quote.Effect, namedCategory: named);
        }
        Check(Play("first", "CARD.A", 2, true).Counted, "First card names the rule without receiving a discount.");
        Check(practice.GetCostChange("CARD.B", WesternPracticeCardKind.Skill, 2, false, false, namedCategory: true).Effect == WesternEntryCostEffect.None
            && practice.GetCostChange("CARD.A", WesternPracticeCardKind.Attack, 3, false, false, namedCategory: true).Apply(3) == 1,
            "Same cost with different name fails; same name with a different cost qualifies.");
        practice = WesternEntryPracticeStateCodec.Restore(WesternEntryPracticeStateCodec.Encode(practice), WesternEntryPracticeState.LanguageAndMeaning);
        Check(Play("second", "CARD.A", 2, true).Counted && practice.CategoryDiscountsRemaining == 1
            && !Play("second", "CARD.A", 2, true).Counted && practice.CategoryDiscountsRemaining == 1,
            "Saved first name and unique payment receipt preserve remaining discounts.");
        Play("third", "CARD.A", 1, true);
        Check(practice.CategoryDiscountsRemaining == 0
            && practice.GetCostChange("CARD.A", WesternPracticeCardKind.Attack, 2, false, false, namedCategory: true).Effect == WesternEntryCostEffect.None,
            "Exactly two named discounts, clamped to zero.");
        practice.BeginTurn(2); Play("x", "CARD.X", 4, true, true);
        Check(practice.CategoryBucket == -1, "X cannot establish the name.");
        Play("zero", "CARD.ZERO", 0, true);
        Check(practice.CategoryDiscountsRemaining == 0, "A zero-cost first card forgoes the chance.");
        practice.BeginTurn(3); Play("old", "CARD.A", 2, false);
        Check(practice.GetCostChange("CARD.B", WesternPracticeCardKind.Skill, 3, false, false).Apply(3) == 2,
            "The original broad 2+ rule remains unchanged without the named flag.");
        JsonObject legacy = JsonNode.Parse(WesternEntryPracticeStateCodec.Encode(practice))!.AsObject();
        legacy["State"]!.AsObject().Remove("CategoryCardModelId");
        practice = WesternEntryPracticeStateCodec.Restore(legacy.ToJsonString(), WesternEntryPracticeState.LanguageAndMeaning);
        Check(practice.GetCostChange("CARD.A", WesternPracticeCardKind.Attack, 2, false, false, namedCategory: true).Effect == WesternEntryCostEffect.None
            && practice.GetCostChange("CARD.B", WesternPracticeCardKind.Skill, 3, false, false).Apply(3) == 2,
            "Legacy saves preserve their old discount and cannot invent a missing first-card name.");
    }
    private static void CheckLocalization()
    {
        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument file = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (var stage in Enum.GetValues<OckhamNamesStage>())
            {
                foreach (var audit in stage == OckhamNamesStage.Discrepancy
                    ? new[] { OckhamNamesAudit.Workers, OckhamNamesAudit.Concept, OckhamNamesAudit.Inscription } : new[] { OckhamNamesAudit.None })
                {
                    var state = OckhamNamesInquiryState.Create("translation", "gorgias"); state.Stage = stage; state.Audit = audit;
                    Check(file.RootElement.TryGetProperty($"OCKHAM_NAMES_INQUIRY.pages.{OckhamNamesInquiryFlow.DescriptionKey(state)}.description", out _), "Every page is bilingual.");
                }
                foreach (var action in OckhamNamesInquiryFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(file.RootElement.TryGetProperty($"OCKHAM_NAMES_INQUIRY.pages.{OckhamNamesInquiryFlow.StageKey(stage)}.options.{OckhamNamesInquiryFlow.ActionKey(action)}.{suffix}", out _), "Every option is bilingual.");
            }
            using JsonDocument relic = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/relics.json"));
            Check(relic.RootElement.TryGetProperty("ARISTOTLE_CATEGORY_TABLET.ockham.description", out _), "The replacement rule has a bilingual relic tip.");
        }
    }
}
