using System.Text.Json;
using STS2Philosophers;

internal static class AugustinePromiseChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static (PhilosophyRunState, WesternRouteGraph) NewRun()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start(AugustinePromiseState.SourceNodeId);
        Check(graph.TryReserveQuestion(journey, 1)
            && graph.TryCompleteReservedQuestion(journey, AugustinePromiseState.RequiredEdgeId),
            "The actual first-act 019 question commits before the Augustine event.");
        return (new PhilosophyRunState
        {
            WesternJourney = journey,
            AugustinePromise = AugustinePromiseState.Create("AUGUSTINE:test:2"),
        }, graph);
    }

    private static PhilosophyRunState Commit(PhilosophyRunState run, WesternRouteGraph graph,
        AugustinePromiseAction action)
    {
        AugustinePromiseState state = run.AugustinePromise!;
        Check(AugustinePromiseFlow.TryPrepare(state, action), $"Can prepare {action}.");
        run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
        state = run.AugustinePromise!;
        Check(AugustinePromiseFlow.TryCommitPending(state, run.WesternJourney!, graph),
            $"Can commit {action} after a prepared save.");
        Check(!AugustinePromiseFlow.TryCommitPending(state, run.WesternJourney!, graph),
            "A confirmed action cannot replay.");
        run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
        Check(AugustinePromiseFlow.IsRouteConsistent(run.AugustinePromise!, run.WesternJourney!),
            "The confirmed route survives a save/reload.");
        return run;
    }

    public static void Run()
    {
        AugustinePromiseAction[] pledges = [AugustinePromiseAction.PromisePublic,
            AugustinePromiseAction.PromisePrivate, AugustinePromiseAction.RemainUndeclared];
        AugustinePromiseAction[] works = [AugustinePromiseAction.RepairPublic,
            AugustinePromiseAction.RepairPrivate, AugustinePromiseAction.UseNoCrew];
        foreach (AugustinePromiseAction pledge in pledges)
        foreach (AugustinePromiseAction work in works)
        {
            (PhilosophyRunState run, WesternRouteGraph graph) = NewRun();
            foreach (AugustinePromiseAction action in new[]
                { AugustinePromiseAction.AcceptInvitation, pledge, work, AugustinePromiseAction.Leave })
                run = Commit(run, graph, action);
            AugustinePromiseState state = run.AugustinePromise!;
            bool adopted = work != AugustinePromiseAction.UseNoCrew;
            Check(state.Closed && state.FixedSlotResolved && state.EdgeCommitted == adopted
                && run.WesternJourney!.LastFixedAct == 2
                && run.WesternJourney.CompletedEdgeIds.Contains(AugustinePromiseState.CompletionEdgeId) == adopted
                && run.WesternJourney.CurrentNodeId == (adopted
                    ? AugustinePromiseState.AdoptedNodeId : AugustinePromiseState.SourceNodeId),
                "The performed work, not the promise, determines 020 and the combat practice.");
            Check(state.Pledge == (pledge switch
                {
                    AugustinePromiseAction.PromisePublic => AugustinePledge.Public,
                    AugustinePromiseAction.PromisePrivate => AugustinePledge.Private,
                    _ => AugustinePledge.Undeclared,
                }) && state.Work == (work switch
                {
                    AugustinePromiseAction.RepairPublic => AugustineWork.Public,
                    AugustinePromiseAction.RepairPrivate => AugustineWork.Private,
                    _ => AugustineWork.Unused,
                }), "Pledge and action remain distinct saved facts.");
        }
        (PhilosophyRunState declined, WesternRouteGraph declinedGraph) = NewRun();
        declined = Commit(declined, declinedGraph, AugustinePromiseAction.DeclineInvitation);
        declined = Commit(declined, declinedGraph, AugustinePromiseAction.Leave);
        Check(declined.AugustinePromise!.Closed && !declined.AugustinePromise.EdgeCommitted
            && declined.WesternJourney!.DeclinedFixedActs.Contains(2),
            "Declining consumes only the second-act Fixed opportunity.");

        (PhilosophyRunState missing019, _) = NewRun();
        missing019.WesternJourney!.CompletedEdgeIds.Remove(AugustinePromiseState.RequiredEdgeId);
        Check(!AugustinePromiseFlow.IsRouteConsistent(missing019.AugustinePromise!, missing019.WesternJourney),
            "A saved event cannot fabricate the first-act prerequisite.");
        (PhilosophyRunState forged, _) = NewRun();
        forged.AugustinePromise!.Stage = AugustinePromiseStage.ResultPublic;
        Check(!forged.AugustinePromise.IsUsable(), "A forged outcome without actions is inert.");

        AugustinePromiseTriggerContext eligible = new(true, true, true, 1, true, 1,
            AugustinePromiseState.SourceNodeId, 1, true, true, true,
            false, false, false, false, true);
        Check(AugustinePromiseTriggerPolicy.ShouldInsert(eligible),
            "The first completed nonboss battle after 019 admits 020.");
        Check(!AugustinePromiseTriggerPolicy.ShouldInsert(eligible with { RequiredEdgeCompleted = false })
            && !AugustinePromiseTriggerPolicy.ShouldInsert(eligible with { CompletionEdgeEligible = false })
            && !AugustinePromiseTriggerPolicy.ShouldInsert(eligible with { SourceIsCompletedNonBossCombat = false })
            && !AugustinePromiseTriggerPolicy.ShouldInsert(eligible with { HasRequiredRelic = false })
            && !AugustinePromiseTriggerPolicy.ShouldInsert(eligible with { CurrentActIndex = 2 }),
            "No 019, spent Fixed, noncombat entry, wrong relic or act may admit 020.");
        Check(AugustinePromiseTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true, SavedRouteConsistent = true,
                SourceIsCompletedNonBossCombat = false, CompletionEdgeEligible = false }),
            "A saved open event may resume without reusing a battle receipt.");
        AristotleCivicInquiryTriggerContext aristotle = new(true, true, true, 1, 1,
            AugustinePromiseState.SourceNodeId, 1, true, false, false, false, true, true);
        Check(!AristotleCivicInquiryTriggerPolicy.ShouldInsert(aristotle)
            && AristotleCivicInquiryTriggerPolicy.ShouldInsert(aristotle with
                { AugustinePrerequisiteCompleted = false })
            && AristotleCivicInquiryTriggerPolicy.ShouldInsert(aristotle with
                { HasSavedInstance = true, SavedInstanceUsable = true }),
            "019 offers Augustine before new Aristotle 016; an old Aristotle instance still resumes.");

        AugustineCombatPracticeState practice = new();
        Check(practice.TryClaim("1:4:12") && !practice.TryClaim("1:4:12")
            && AugustineCombatPracticeState.TryRestore(practice.Encode(), out AugustineCombatPracticeState restored)
            && !restored.TryClaim("1:4:12") && restored.TryClaim("1:5:13"),
            "A native combat key pays once, survives save/load and permits the next combat.");
        foreach (string bad in new[] { "broken", "{\"Version\":99}", "{\"LastGrantedCombatKey\":\"bad key\"}" })
            Check(!AugustineCombatPracticeState.TryRestore(bad, out _),
                "Unknown or malformed combat receipt is inert.");

        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument events = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            JsonElement text = events.RootElement;
            Check(text.TryGetProperty("AUGUSTINE_PROMISE.title", out _), "Augustine title is bilingual.");
            foreach (AugustinePromiseStage stage in Enum.GetValues<AugustinePromiseStage>())
            {
                foreach (AugustinePledge pledge in stage is AugustinePromiseStage.NewFact or AugustinePromiseStage.ResultPublic
                    or AugustinePromiseStage.ResultPrivate or AugustinePromiseStage.ResultUnused
                    ? new[] { AugustinePledge.Public, AugustinePledge.Private, AugustinePledge.Undeclared }
                    : new[] { AugustinePledge.None })
                {
                    AugustinePromiseState state = AugustinePromiseState.Create("localization");
                    state.Stage = stage;
                    state.Pledge = pledge;
                    string key = $"AUGUSTINE_PROMISE.pages.{AugustinePromiseFlow.DescriptionKey(state)}.description";
                    Check(text.TryGetProperty(key, out _), $"Missing {language} {key}.");
                }
                string page = $"AUGUSTINE_PROMISE.pages.{AugustinePromiseFlow.StageKey(stage)}";
                foreach (AugustinePromiseAction action in AugustinePromiseFlow.Options(stage))
                {
                    string key = $"{page}.options.{AugustinePromiseFlow.ActionKey(action)}";
                    Check(text.TryGetProperty($"{key}.title", out _)
                        && text.TryGetProperty($"{key}.description", out _),
                        $"Missing {language} {key}.");
                }
            }
            using JsonDocument relics = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/relics.json"));
            foreach (string branch in new[] { "public", "private" })
                Check(relics.RootElement.TryGetProperty($"PLATO_CIVIC_SEAL.augustine.{branch}.title", out _)
                    && relics.RootElement.TryGetProperty($"PLATO_CIVIC_SEAL.augustine.{branch}.description", out _),
                    "The new practice has bilingual relic tips.");
        }
        Console.WriteLine("Augustine promise checks passed: ten saved event paths, 019/020/016 exclusivity, combat receipts and bilingual copy.");
    }
}
