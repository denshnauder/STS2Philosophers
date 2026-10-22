using System.Text.Json;
using STS2Philosophers;

internal static class AquinasBridgeNoticeChecks
{
    private static void Check(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException(why);
    }

    private static (PhilosophyRunState, WesternRouteGraph) NewRun()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start("PLATO__FREEDOM_AND_INSTITUTIONS__CORE");
        Check(graph.TryApply(journey, "WESTERN_EDGE_016"), "Aquinas starts after actual institution 016.");
        return (new PhilosophyRunState { WesternJourney = journey,
            AquinasBridgeNotice = AquinasBridgeNoticeState.Create("AQUINAS:test:3") }, graph);
    }

    private static PhilosophyRunState Commit(PhilosophyRunState run, WesternRouteGraph graph,
        AquinasBridgeNoticeAction action)
    {
        Check(AquinasBridgeNoticeFlow.TryPrepare(run.AquinasBridgeNotice!, action), $"Prepare {action}.");
        run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
        Check(AquinasBridgeNoticeFlow.TryCommitPending(run.AquinasBridgeNotice!, run.WesternJourney!, graph),
            $"Commit {action} after restoring the prepared boundary.");
        Check(!AquinasBridgeNoticeFlow.TryCommitPending(run.AquinasBridgeNotice!, run.WesternJourney!, graph),
            "A committed choice cannot apply twice.");
        run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
        Check(AquinasBridgeNoticeFlow.IsRouteConsistent(run.AquinasBridgeNotice!, run.WesternJourney!),
            "Restoring a committed boundary preserves its exact route.");
        return run;
    }

    public static void Run()
    {
        foreach (AquinasBridgeNoticeAction audit in new[]
            { AquinasBridgeNoticeAction.AuditAuthority, AquinasBridgeNoticeAction.AuditPublication })
        foreach (AquinasBridgeNoticeAction result in new[] { AquinasBridgeNoticeAction.PublishCorrection,
            AquinasBridgeNoticeAction.VerifyException, AquinasBridgeNoticeAction.KeepOldRule })
        {
            (PhilosophyRunState run, WesternRouteGraph graph) = NewRun();
            foreach (var action in new[] { AquinasBridgeNoticeAction.AcceptInvitation, audit, result,
                AquinasBridgeNoticeAction.Leave }) run = Commit(run, graph, action);
            bool adopted = result != AquinasBridgeNoticeAction.KeepOldRule;
            var state = run.AquinasBridgeNotice!;
            Check(state.Closed && state.EdgeCommitted == adopted && state.FixedSlotResolved
                && run.WesternJourney!.LastFixedAct == 3
                && run.WesternJourney.CompletedEdgeIds.Contains("WESTERN_EDGE_045") == adopted
                && run.WesternJourney.CurrentNodeId == (adopted
                    ? AquinasBridgeNoticeState.AdoptedNodeId : AquinasBridgeNoticeState.SourceNodeId),
                "A completed legal action uses one third-act Fixed and retains its actual result.");
            Check(state.Audit == (audit == AquinasBridgeNoticeAction.AuditAuthority
                ? AquinasAudit.Authority : AquinasAudit.Publication), "The unchosen audit is never fabricated.");
        }
        (PhilosophyRunState declined, WesternRouteGraph dg) = NewRun();
        declined = Commit(declined, dg, AquinasBridgeNoticeAction.DeclineInvitation);
        declined = Commit(declined, dg, AquinasBridgeNoticeAction.Leave);
        Check(declined.WesternJourney!.DeclinedFixedActs.Contains(3)
            && declined.AquinasBridgeNotice!.Rule == AquinasRule.Declined,
            "Declining explicitly spends the third-act Fixed without changing doctrine.");

        foreach (string missing in new[] { "prerequisite", "rival", "spent", "language" })
        {
            (PhilosophyRunState run, WesternRouteGraph graph) = NewRun();
            switch (missing)
            {
                case "prerequisite": run.WesternJourney!.CompletedEdgeIds.Remove("WESTERN_EDGE_016"); break;
                case "rival": Check(graph.TryApply(run.WesternJourney!, "WESTERN_EDGE_017"), "Prepare rival 017."); break;
                case "spent": Check(graph.TryDecline(run.WesternJourney!, "Fixed", 3), "Spend third-act Fixed."); break;
                case "language": run.WesternJourney!.CurrentNodeId = "THOMAS_AQUINAS__LANGUAGE_AND_MEANING__CORE"; break;
            }
            Check(!AquinasBridgeNoticeFlow.IsRouteConsistent(run.AquinasBridgeNotice!, run.WesternJourney!),
                $"Reject a conflicting saved route: {missing}.");
        }
        var forged = AquinasBridgeNoticeState.Create("forged");
        forged.Stage = AquinasBridgeNoticeStage.ResultPublished;
        Check(!forged.IsUsable(), "An invented result without its action history is inert.");
        forged = AquinasBridgeNoticeState.Create("unknown");
        forged.Version = 99;
        Check(!forged.IsUsable(), "An unknown state version cannot execute.");

        AquinasBridgeNoticeTriggerContext eligible = new(true, true, true, 2, true, 1,
            AquinasBridgeNoticeState.SourceNodeId, 2, true, false, true, true, true,
            false, false, false, false, true);
        Check(AquinasBridgeNoticeTriggerPolicy.ShouldInsert(eligible), "A completed third-act combat permits invitation.");
        foreach (var blocked in new[] { eligible with { AtSafeMapBoundary = false },
            eligible with { SourceIsCompletedNonBossCombat = false }, eligible with { CompletedCombatCount = 0 },
            eligible with { RequiredEdgeCompleted = false }, eligible with { RivalEdgeCompleted = true },
            eligible with { CompletionEdgeEligible = false }, eligible with { HasRequiredRelic = false },
            eligible with { AristotleAdoptedAndClosed = false }, eligible with { CurrentActIndex = 1 },
            eligible with { LastFixedAct = 3 }, eligible with { HasSavedInstance = true, SavedInstanceClosed = true } })
            Check(!AquinasBridgeNoticeTriggerPolicy.ShouldInsert(blocked), "Unsafe or conflicting invitations stay absent.");
        Check(AquinasBridgeNoticeTriggerPolicy.ShouldInsert(eligible with { HasSavedInstance = true,
            SavedInstanceUsable = true, SavedRouteConsistent = true, SourceIsCompletedNonBossCombat = false,
            CompletionEdgeEligible = false }), "Saved open events resume without another combat.");
        Check(LaterActRelicEligibilityCatalog.IsEligible(LaterActCandidateIds.Aquinas, 2,
            [RouteRelicIds.PlatoCivicSeal]) && !LaterActRelicEligibilityCatalog.IsEligible(
                LaterActCandidateIds.Aquinas, 2, ["ARISTOTLE_CATEGORY_TABLET"]),
            "Institution Aquinas requires the real civic relic, not the language relic.");

        AquinasCombatPracticeState practice = new();
        Check(!practice.ObserveFirstManual("2:3:10", false)
            && !practice.ObserveFirstManual("2:3:10", true), "A mismatching first card spends the chance.");
        Check(AquinasCombatPracticeState.TryRestore(practice.Encode(), out var saved)
            && !saved.ObserveFirstManual("2:3:10", true)
            && saved.ObserveFirstManual("2:4:11", true)
            && !saved.ObserveFirstManual("2:4:11", true), "Saved first-card receipts pay once and allow a new combat.");
        foreach (string bad in new[] { "broken", "{\"Version\":99}",
            "{\"CombatKey\":\"bad key\"}", "{\"CombatKey\":\"\",\"FirstManualSeen\":true}" })
            Check(!AquinasCombatPracticeState.TryRestore(bad, out _), "Malformed receipts stay inert.");

        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument events = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            Check(events.RootElement.TryGetProperty("AQUINAS_BRIDGE_NOTICE.title", out _), "Aquinas title is bilingual.");
            foreach (AquinasBridgeNoticeStage stage in Enum.GetValues<AquinasBridgeNoticeStage>())
            {
                foreach (AquinasAudit audit in stage == AquinasBridgeNoticeStage.BrokenBridge
                    ? new[] { AquinasAudit.Authority, AquinasAudit.Publication } : new[] { AquinasAudit.None })
                {
                    var state = AquinasBridgeNoticeState.Create("copy"); state.Stage = stage; state.Audit = audit;
                    Check(events.RootElement.TryGetProperty($"AQUINAS_BRIDGE_NOTICE.pages.{AquinasBridgeNoticeFlow.DescriptionKey(state)}.description", out _),
                        $"Missing {language} description for {stage}/{audit}.");
                }
                foreach (var action in AquinasBridgeNoticeFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(events.RootElement.TryGetProperty($"AQUINAS_BRIDGE_NOTICE.pages.{AquinasBridgeNoticeFlow.StageKey(stage)}.options.{AquinasBridgeNoticeFlow.ActionKey(action)}.{suffix}", out _),
                        $"Missing {language} option {stage}/{action}/{suffix}.");
            }
            using JsonDocument relics = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/relics.json"));
            foreach (string rule in new[] { "published", "emergency" })
            foreach (string suffix in new[] { "title", "description" })
                Check(relics.RootElement.TryGetProperty($"PLATO_CIVIC_SEAL.aquinas.{rule}.{suffix}", out _),
                    "Aquinas combat practice has bilingual tooltips.");
        }
        Console.WriteLine("Aquinas bridge notice checks passed: seven saved paths, 016/045 and rival 017 gates, first-card receipts and bilingual copy.");
    }
}
