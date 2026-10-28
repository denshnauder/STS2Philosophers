using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class SpinozaCauseNetworkChecks
{
    public static void Run()
    {
        RunTriggerChecks();
        RunEventChecks();
        RunPracticeChecks();
        Console.WriteLine("Spinoza cause network checks passed.");
    }

    private static void Check(bool condition, string boundary)
    {
        if (!condition) throw new InvalidOperationException("Spinoza: " + boundary);
    }

    private static void RunTriggerChecks()
    {
        var fresh = new SpinozaCauseNetworkTriggerContext(
            RunInProgress: true, AtNativeQuestionEntry: true, IsSingleplayer: true, CurrentActIndex: 1,
            HasRequiredRelic: true, HasSourceDoctrine: true, HasOpenRouteQuestion: false,
            CurrentNodeId: SpinozaCauseNetworkState.SourceNodeId, LastFixedAct: 1,
            FixedActTwoResolved: false, RetainedEnding: false, HasEligibleFixedEdge: true,
            HasCompletedActCombat: true, HasSeenSpinoza: false, HasSavedInstance: false,
            SavedInstanceUsable: false, SavedInstanceClosed: false, SavedInstanceConsistent: false,
            ModelAvailable: true);
        Check(SpinozaCauseNetworkTriggerPolicy.ShouldInsert(fresh), "legitimate unused Fixed2");
        foreach (var denied in new[]
        {
            fresh with { RunInProgress = false },
            fresh with { AtNativeQuestionEntry = false },
            fresh with { CurrentActIndex = 0 },
            fresh with { IsSingleplayer = false },
            fresh with { HasRequiredRelic = false },
            fresh with { HasSourceDoctrine = false },
            fresh with { HasOpenRouteQuestion = true },
            fresh with { CurrentNodeId = "DESCARTES__KNOWLEDGE_AND_DOUBT__CORE" },
            fresh with { LastFixedAct = 2 },
            fresh with { FixedActTwoResolved = true },
            fresh with { RetainedEnding = true },
            fresh with { HasEligibleFixedEdge = false },
            fresh with { HasCompletedActCombat = false },
            fresh with { HasSeenSpinoza = true },
            fresh with { ModelAvailable = false },
        }) Check(!SpinozaCauseNetworkTriggerPolicy.ShouldInsert(denied), "reject unsafe fresh entry");

        var saved = fresh with
        {
            HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceConsistent = true,
            CurrentNodeId = SpinozaCauseNetworkState.AdoptedNodeId, LastFixedAct = 2,
            FixedActTwoResolved = true, HasEligibleFixedEdge = false,
            HasCompletedActCombat = false, HasSeenSpinoza = true,
        };
        Check(SpinozaCauseNetworkTriggerPolicy.ShouldInsert(saved), "recover exact committed instance");
        Check(!SpinozaCauseNetworkTriggerPolicy.ShouldInsert(saved with { SavedInstanceClosed = true }), "closed instance");
        Check(!SpinozaCauseNetworkTriggerPolicy.ShouldInsert(saved with { SavedInstanceUsable = false }), "invalid instance");
        Check(!SpinozaCauseNetworkTriggerPolicy.ShouldInsert(saved with { SavedInstanceConsistent = false }), "foreign instance");
        Check(!SpinozaCauseNetworkTriggerPolicy.ShouldInsert(saved with { AtNativeQuestionEntry = false }), "recovery still needs real node");
        Console.WriteLine("Spinoza native question eligibility checks passed.");
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static (PhilosophyRunState, WesternRouteGraph) Fixture()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start(SpinozaCauseNetworkState.SourceNodeId);
        Check(journey.LastFixedAct == 1, "the source node is reached after the first Fixed slot.");

        PhilosophyRunState shared = new()
        {
            WesternJourney = journey,
            CurrentDoctrine = new CurrentDoctrine { ThinkerId = "DESCARTES", DoctrineId = "DESCARTES_THINKING_LENS" },
            SpinozaCauseNetwork = SpinozaCauseNetworkState.Create("SPINOZA:TEST:2"),
        };
        return (Reload(shared), graph);
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        SpinozaCauseNetworkAction action)
    {
        SpinozaCauseNetworkState state = shared.SpinozaCauseNetwork!;
        Check(SpinozaCauseNetworkFlow.TryPrepare(state, action), $"Prepare {action}.");
        shared = Reload(shared);
        state = shared.SpinozaCauseNetwork!;
        Check(SpinozaCauseNetworkFlow.TryCommitPending(state, shared.WesternJourney!, graph),
            $"Commit {action} after a prepared save.");
        Check(!SpinozaCauseNetworkFlow.TryCommitPending(state, shared.WesternJourney!, graph),
            "The confirmed action cannot replay.");
        shared = Reload(shared);
        Check(SpinozaCauseNetworkFlow.IsRouteConsistent(shared.SpinozaCauseNetwork!, shared.WesternJourney!),
            "The event and route survive save/reload together.");
        return shared;
    }

    private static void RunEventChecks()
    {
        // Decline + Leave: no edge, no adoption, second Fixed declined without retaining the ending.
        (PhilosophyRunState declined, WesternRouteGraph declinedGraph) = Fixture();
        declined = Commit(declined, declinedGraph, SpinozaCauseNetworkAction.Decline);
        declined = Commit(declined, declinedGraph, SpinozaCauseNetworkAction.Leave);
        Check(declined.SpinozaCauseNetwork!.Closed && declined.SpinozaCauseNetwork.IsUsable(),
            "Declined event closes as a usable state.");
        Check(declined.WesternJourney!.LastFixedAct == 2
            && declined.WesternJourney.DeclinedFixedActs.Contains(2)
            && !declined.WesternJourney.RetainedEnding
            && !declined.WesternJourney.CompletedEdgeIds.Contains(SpinozaCauseNetworkState.CompletionEdgeId)
            && !declined.WesternJourney.SeenThinkerIds.Contains("BARUCH_SPINOZA")
            && declined.WesternJourney.CurrentNodeId == SpinozaCauseNetworkState.SourceNodeId,
            "Declining keeps the source node and spends the second Fixed slot.");
        Check(!declined.WesternJourney.ResolvedQuestionActs.Contains(1)
            && !declined.WesternJourney.ResolvedQuestionActs.Contains(2),
            "Declining consumes no Question slot.");

        SpinozaAttribution[] attributions = [SpinozaAttribution.BlameEnemy, SpinozaAttribution.TraceChains];
        SpinozaCauseNetworkAction[] decisions =
        [
            SpinozaCauseNetworkAction.Intervene,
            SpinozaCauseNetworkAction.Reorder,
            SpinozaCauseNetworkAction.CorrectRecord,
        ];
        foreach (SpinozaAttribution attribution in attributions)
        foreach (SpinozaCauseNetworkAction decision in decisions)
        {
            (PhilosophyRunState shared, WesternRouteGraph graph) = Fixture();
            shared = Commit(shared, graph, SpinozaCauseNetworkAction.Accept);
            shared = Commit(shared, graph, attribution == SpinozaAttribution.BlameEnemy
                ? SpinozaCauseNetworkAction.BlameEnemy
                : SpinozaCauseNetworkAction.TraceChains);
            Check(shared.SpinozaCauseNetwork!.FirstAttribution == attribution,
                "The attribution choice is recorded.");
            Check(SpinozaCauseNetworkFlow.DescriptionKey(shared.SpinozaCauseNetwork) == "WEATHER_REVEALED_"
                + (attribution == SpinozaAttribution.BlameEnemy ? "BLAME_ENEMY" : "TRACE_CHAINS"),
                "The reveal selects the actual text matching the first attribution.");
            shared = Commit(shared, graph, SpinozaCauseNetworkAction.Continue);
            shared = Commit(shared, graph, decision);
            shared = Commit(shared, graph, SpinozaCauseNetworkAction.Leave);

            SpinozaCauseNetworkState state = shared.SpinozaCauseNetwork!;
            WesternJourneyState journey = shared.WesternJourney!;
            Check(state.Closed && state.IsUsable(), "The completed event stays usable.");
            Check(journey.LastFixedAct == 2
                && !journey.ResolvedQuestionActs.Contains(1)
                && !journey.ResolvedQuestionActs.Contains(2),
                "The event takes the second Fixed slot without consuming a Question.");

            bool adopted = decision is SpinozaCauseNetworkAction.Intervene
                or SpinozaCauseNetworkAction.Reorder;
            if (adopted)
            {
                Check(journey.CurrentNodeId == SpinozaCauseNetworkState.AdoptedNodeId
                    && journey.CompletedEdgeIds.Contains(SpinozaCauseNetworkState.CompletionEdgeId)
                    && journey.SeenThinkerIds.Contains("BARUCH_SPINOZA")
                    && !journey.RetainedEnding,
                    "Intervening or reordering adopts Spinoza through the completion edge.");
                Check(state.Disposition == (decision == SpinozaCauseNetworkAction.Intervene
                        ? SpinozaDisposition.Intervened
                        : SpinozaDisposition.Reordered),
                    "The adopted disposition is recorded.");
            }
            else
            {
                Check(journey.CurrentNodeId == SpinozaCauseNetworkState.SourceNodeId
                    && !journey.CompletedEdgeIds.Contains(SpinozaCauseNetworkState.CompletionEdgeId)
                    && !journey.SeenThinkerIds.Contains("BARUCH_SPINOZA")
                    && journey.DeclinedFixedActs.Contains(2)
                    && !journey.RetainedEnding,
                    "Correcting the record declines the second Fixed without retaining the ending.");
                Check(state.Disposition == SpinozaDisposition.Corrected,
                    "The corrected disposition is recorded.");
            }
        }

        // Absence of Spinoza on an old shared save stays absent.
        (PhilosophyRunState absent, _) = Fixture();
        absent.SpinozaCauseNetwork = null;
        Check(Reload(absent).SpinozaCauseNetwork is null,
            "An old save without Spinoza stays without Spinoza.");

        // A legal pending action is usable and rejects a double prepare.
        (PhilosophyRunState pending, _) = Fixture();
        Check(SpinozaCauseNetworkFlow.TryPrepare(pending.SpinozaCauseNetwork!, SpinozaCauseNetworkAction.Accept),
            "A legal action prepares.");
        pending = Reload(pending);
        Check(pending.SpinozaCauseNetwork!.IsUsable(), "A pending legal action is usable.");
        Check(!SpinozaCauseNetworkFlow.TryPrepare(pending.SpinozaCauseNetwork, SpinozaCauseNetworkAction.Decline),
            "A pending action rejects a second prepare.");

        // Missing required fields are rejected.
        foreach (string field in new[] { "Version", "Closed", "ConfirmedActions", "Stage", "PendingAction" })
        {
            (PhilosophyRunState shared, _) = Fixture();
            JsonObject node = JsonNode.Parse(JsonSerializer.Serialize(shared))!.AsObject();
            node["SpinozaCauseNetwork"]!.AsObject().Remove(field);
            Check(!PhilosophyRunStateCodec.Decode(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()))).SpinozaCauseNetwork!.IsUsable(),
                $"A state missing {field} is rejected.");
        }

        // Invalid version, phantom history and forged attribution are rejected.
        (PhilosophyRunState badVersion, _) = Fixture();
        badVersion.SpinozaCauseNetwork!.Version = 99;
        Check(!Reload(badVersion).SpinozaCauseNetwork!.IsUsable(), "An unknown version is rejected.");

        (PhilosophyRunState phantom, _) = Fixture();
        phantom.SpinozaCauseNetwork!.ConfirmedActions.Add(SpinozaCauseNetworkAction.Leave);
        Check(!Reload(phantom).SpinozaCauseNetwork!.IsUsable(), "A phantom history is rejected.");

        (PhilosophyRunState forged, _) = Fixture();
        forged.SpinozaCauseNetwork!.FirstAttribution = SpinozaAttribution.TraceChains;
        Check(!Reload(forged).SpinozaCauseNetwork!.IsUsable(), "A forged attribution is rejected.");

        // A state bound to a different route is inconsistent.
        (PhilosophyRunState mismatched, _) = Fixture();
        mismatched.WesternJourney!.CurrentNodeId = "DESCARTES__KNOWLEDGE_AND_DOUBT__CORE";
        Check(!SpinozaCauseNetworkFlow.IsRouteConsistent(mismatched.SpinozaCauseNetwork!,
            mismatched.WesternJourney!),
            "A state bound to another route node is inconsistent.");

        Console.WriteLine("Spinoza cause network event checks passed: 7 paths, adoption and save integrity.");
    }

    private static void RunPracticeChecks()
    {
        const string key = "1:4:98";
        SpinozaCombatPracticeState weak = new();
        Check(weak.BeginCombat(key), "A first valid combat binds the receipt.");
        Check(weak.Observe(key, SpinozaDisposition.Intervened, false, true, false) == SpinozaPracticeReward.None,
            "The intervention branch does not draw for Skills.");
        Check(weak.Observe(key, SpinozaDisposition.Intervened, true, false, false) == SpinozaPracticeReward.None,
            "An attack without a single enemy does not claim Weak.");
        Check(weak.Observe(key, SpinozaDisposition.Intervened, true, false, true) == SpinozaPracticeReward.Weak,
            "The first single-enemy attack claims Weak even after a non-single attack.");
        string claimedWeak = weak.Encode();
        Check(SpinozaCombatPracticeState.TryRestore(claimedWeak, out var reloadedWeak)
            && reloadedWeak.Observe(key, SpinozaDisposition.Intervened, true, false, true) == SpinozaPracticeReward.None,
            "A spent Weak receipt survives reload and cannot pay again.");
        Check(!reloadedWeak.BeginCombat(key)
            && reloadedWeak.Observe(key, SpinozaDisposition.Intervened, true, false, true) == SpinozaPracticeReward.None,
            "A repeated combat-start callback cannot reset the Weak receipt.");
        Check(reloadedWeak.BeginCombat("1:5:99")
            && reloadedWeak.Observe("1:5:99", SpinozaDisposition.Intervened, true, false, true) == SpinozaPracticeReward.Weak,
            "A genuine new combat permits a fresh intervention.");

        SpinozaCombatPracticeState draw = new();
        draw.BeginCombat(key);
        Check(draw.Observe("2:4:98", SpinozaDisposition.Reordered, true, false, true) == SpinozaPracticeReward.None,
            "A callback for a foreign combat cannot record an attack.");
        Check(draw.Observe(key, SpinozaDisposition.Reordered, false, true, false) == SpinozaPracticeReward.Draw,
            "The first manual Skill before any manual Attack claims Draw.");
        string claimedDraw = draw.Encode();
        Check(SpinozaCombatPracticeState.TryRestore(claimedDraw, out var reloadedDraw)
            && reloadedDraw.Observe(key, SpinozaDisposition.Reordered, false, true, false) == SpinozaPracticeReward.None,
            "A spent Draw receipt survives reload and cannot pay again.");
        Check(reloadedDraw.Observe(key, SpinozaDisposition.Reordered, true, false, true) == SpinozaPracticeReward.None,
            "The reordering branch does not grant Weak.");

        SpinozaCombatPracticeState attackFirst = new();
        attackFirst.BeginCombat(key);
        attackFirst.Observe(key, SpinozaDisposition.Reordered, true, false, false);
        Check(SpinozaCombatPracticeState.TryRestore(attackFirst.Encode(), out var partial)
            && partial.Observe(key, SpinozaDisposition.Reordered, false, true, false) == SpinozaPracticeReward.None,
            "A partial receipt preserves the prior manual Attack that prevents Draw.");
        Check(partial.Observe(key, SpinozaDisposition.Reordered, false, true, false) == SpinozaPracticeReward.None,
            "Later Skills cannot retry a disqualified first Skill.");

        string Mutate(Action<JsonObject> mutate)
        {
            JsonObject node = JsonNode.Parse(claimedWeak)!.AsObject();
            mutate(node);
            return node.ToJsonString();
        }
        foreach (string payload in new[]
        {
            "not-json", new string('x', 4097),
            claimedWeak.Replace("\"Version\":1", "\"Version\":1,\"Version\":1", StringComparison.Ordinal),
            Mutate(n => n["Version"] = 99),
            Mutate(n => n["CombatKey"] = ""),
            Mutate(n => n["CombatKey"] = "bad key"),
            Mutate(n => n["CombatKey"] = new string('9', 129)),
            Mutate(n => n["ManualAttackSeen"] = false),
            Mutate(n => { n["DrawClaimed"] = true; n["FirstSkillSeen"] = false; }),
            Mutate(n => n["Unknown"] = true),
            Mutate(n => n.Remove("FirstSkillSeen")),
        }) Check(!SpinozaCombatPracticeState.TryRestore(payload, out _), "Malformed or inconsistent practice receipts are rejected.");
        Check(!new SpinozaCombatPracticeState().BeginCombat("bad key"), "An illegal key cannot bind a combat.");
        Console.WriteLine("Spinoza practice checks passed: branch rewards, prior Attack, combat identity and strict receipts.");
    }
}
