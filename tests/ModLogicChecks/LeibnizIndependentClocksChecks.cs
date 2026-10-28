using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class LeibnizIndependentClocksChecks
{
    private const ulong Seed = 123456789UL;
    private static readonly WesternRouteGraph Graph = WesternRouteGraph.LoadEmbedded();

    public static void Run()
    {
        CheckTriggers();
        CheckEvent();
        CheckPractice();
        Console.WriteLine("Leibniz independent clocks pure checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Leibniz: " + message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static string SpinozaId => $"SPINOZA_CAUSE_NETWORK:{Seed}:2";
    private static string LeibnizId => $"LEIBNIZ_INDEPENDENT_CLOCKS:{Seed}:3";

    private static PhilosophyRunState Precursor()
    {
        WesternJourneyState journey = Graph.Start("DESCARTES__SELF_AND_OTHER__CORE");
        Check(Graph.TryReserveQuestion(journey, 1), "the first question is reserved.");
        SpinozaCauseNetworkState spinoza = SpinozaCauseNetworkState.Create(SpinozaId);
        foreach (SpinozaCauseNetworkAction action in new[]
                 {
                     SpinozaCauseNetworkAction.Accept, SpinozaCauseNetworkAction.TraceChains,
                     SpinozaCauseNetworkAction.Continue, SpinozaCauseNetworkAction.Reorder,
                     SpinozaCauseNetworkAction.Leave,
                 })
        {
            Check(SpinozaCauseNetworkFlow.TryPrepare(spinoza, action), $"prepare Spinoza {action}.");
            Check(SpinozaCauseNetworkFlow.TryCommitPending(spinoza, journey, Graph), $"commit Spinoza {action}.");
        }
        Check(spinoza.Closed && spinoza.IsUsable(), "the Spinoza precursor is closed and usable.");
        Check(journey.CompletedEdgeIds.Contains(SpinozaCauseNetworkState.CompletionEdgeId),
            "the Spinoza precursor committed edge023.");
        Check(journey.CurrentNodeId == SpinozaCauseNetworkState.AdoptedNodeId,
            "the Spinoza precursor adopted its node.");
        return new PhilosophyRunState
        {
            WesternJourney = journey,
            CurrentDoctrine = new CurrentDoctrine { ThinkerId = "DESCARTES", DoctrineId = "DESCARTES_THINKING_LENS" },
            SpinozaCauseNetwork = spinoza,
            LeibnizHarmony = LeibnizHarmonyState.Create(LeibnizId, spinoza.InstanceId),
        };
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, LeibnizHarmonyAction action)
    {
        LeibnizHarmonyState state = shared.LeibnizHarmony!;
        Check(LeibnizHarmonyFlow.TryPrepare(state, action), $"prepare {action}.");
        shared = Reload(shared);
        state = shared.LeibnizHarmony!;
        Check(LeibnizHarmonyFlow.TryCommitPending(state, shared.WesternJourney!,
            shared.SpinozaCauseNetwork!, Graph), $"commit {action} after a prepared save.");
        Check(!LeibnizHarmonyFlow.TryCommitPending(state, shared.WesternJourney!,
            shared.SpinozaCauseNetwork!, Graph), "the confirmed action cannot replay.");
        shared = Reload(shared);
        Check(LeibnizHarmonyFlow.IsRouteConsistent(shared.LeibnizHarmony!, shared.WesternJourney!,
            shared.SpinozaCauseNetwork!), "the event and route survive save/reload together.");
        return shared;
    }

    private static void CheckTriggers()
    {
        Check(LeibnizHarmonyState.BuildInstanceId(Seed) == LeibnizId,
            "the trigger's expected instance id is the uppercase Leibniz id.");
        var fresh = new LeibnizHarmonyTriggerContext(
            RunSeed: Seed, RunInProgress: true, AtNativeQuestionEntry: true, IsSingleplayer: true,
            CurrentActIndex: 2, OwnsSoleDescartesThinkingLens: true,
            CurrentDoctrineId: "DESCARTES", CurrentDoctrineLensId: "DESCARTES_THINKING_LENS",
            PredecessorUsable: true, PredecessorConsistent: true, PredecessorClosed: true,
            PredecessorModelAvailable: true, PredecessorInstanceId: SpinozaId,
            CurrentNodeId: LeibnizHarmonyState.SourceNodeId, LastFixedAct: 2, Fixed3Unused: true,
            CompletionEdgeEligible: true, InEligibleCombat: true, HasOpenRouteQuestion: false,
            HasEdge024: false, LeibnizSeen: false, HasResolvedLaterQuestions: false, RetainedEnding: false,
            HasSavedInstance: false,
            SavedStateUsable: false, SavedStateConsistent: false, SavedStateClosed: false,
            SavedModelAvailable: false, SavedInstanceId: "", SavedSpinozaInstanceId: "");
        Check(LeibnizHarmonyTriggerPolicy.ShouldInsert(fresh), "legitimate unused Fixed3");
        foreach (var denied in new[]
        {
            fresh with { RunInProgress = false },
            fresh with { AtNativeQuestionEntry = false },
            fresh with { IsSingleplayer = false },
            fresh with { CurrentActIndex = 1 },
            fresh with { OwnsSoleDescartesThinkingLens = false },
            fresh with { CurrentDoctrineId = "SPINOZA" },
            fresh with { CurrentDoctrineLensId = "OTHER" },
            fresh with { PredecessorUsable = false },
            fresh with { PredecessorConsistent = false },
            fresh with { PredecessorClosed = false },
            fresh with { PredecessorModelAvailable = false },
            fresh with { PredecessorInstanceId = "" },
            fresh with { CurrentNodeId = "DESCARTES__SELF_AND_OTHER__CORE" },
            fresh with { LastFixedAct = 1 },
            fresh with { Fixed3Unused = false },
            fresh with { CompletionEdgeEligible = false },
            fresh with { InEligibleCombat = false },
            fresh with { HasOpenRouteQuestion = true },
            fresh with { HasEdge024 = true },
            fresh with { LeibnizSeen = true },
            fresh with { HasResolvedLaterQuestions = true },
            fresh with { RetainedEnding = true },
        }) Check(!LeibnizHarmonyTriggerPolicy.ShouldInsert(denied), "reject unsafe fresh entry");

        var saved = fresh with
        {
            HasSavedInstance = true,
            SavedStateUsable = true, SavedStateConsistent = true, SavedStateClosed = false,
            SavedModelAvailable = true, SavedInstanceId = LeibnizId, SavedSpinozaInstanceId = SpinozaId,
        };
        Check(LeibnizHarmonyTriggerPolicy.ShouldInsert(saved), "a usable open saved instance recovers.");
        foreach (var denied in new[]
        {
            saved with { SavedStateUsable = false },
            saved with { SavedStateConsistent = false },
            saved with { SavedStateClosed = true },
            saved with { SavedModelAvailable = false },
            saved with { SavedInstanceId = "" },
            saved with { SavedInstanceId = "LEIBNIZ_INDEPENDENT_CLOCKS:1:3" },
            saved with { SavedSpinozaInstanceId = "" },
            saved with { SavedSpinozaInstanceId = "SPINOZA_CAUSE_NETWORK:1:2" },
        }) Check(!LeibnizHarmonyTriggerPolicy.ShouldInsert(denied), "reject unsafe saved recovery");

        var committed = saved with
        {
            CurrentNodeId = LeibnizHarmonyState.AdoptedNodeId, LastFixedAct = 3, Fixed3Unused = false,
            CompletionEdgeEligible = false, InEligibleCombat = false, LeibnizSeen = true,
        };
        Check(LeibnizHarmonyTriggerPolicy.ShouldInsert(committed),
            "a committed saved recovery is accepted when the saved instance is consistent and open.");
        Check(!LeibnizHarmonyTriggerPolicy.ShouldInsert(committed with { SavedStateConsistent = false }),
            "a committed saved recovery with an inconsistent instance is rejected.");
        Check(!LeibnizHarmonyTriggerPolicy.ShouldInsert(committed with { SavedStateClosed = true }),
            "a committed saved recovery with a closed instance is rejected.");
        Console.WriteLine("Leibniz trigger eligibility checks passed.");
    }

    private static void CheckEvent()
    {
        // Decline + Leave: no edge, no adoption, Fixed3 declined and ending retained.
        PhilosophyRunState declined = Commit(Precursor(), LeibnizHarmonyAction.Decline);
        declined = Commit(declined, LeibnizHarmonyAction.Leave);
        Check(declined.LeibnizHarmony!.Closed && declined.LeibnizHarmony.IsUsable(),
            "declined event closes as a usable state.");
        Check(declined.WesternJourney!.LastFixedAct == 3
            && declined.WesternJourney.DeclinedFixedActs.Contains(3)
            && declined.WesternJourney.RetainedEnding
            && !declined.WesternJourney.CompletedEdgeIds.Contains(LeibnizHarmonyState.CompletionEdgeId)
            && !declined.WesternJourney.SeenThinkerIds.Contains("GOTTFRIED_WILHELM_LEIBNIZ")
            && declined.WesternJourney.CurrentNodeId == LeibnizHarmonyState.SourceNodeId,
            "declining keeps the Spinoza node and spends Fixed3 with a retained ending.");
        Check(!declined.WesternJourney.ResolvedQuestionActs.Contains(2)
            && !declined.WesternJourney.ResolvedQuestionActs.Contains(3),
            "declining consumes no Question slot.");
        Check(Graph.IsComplete(declined.WesternJourney)
            && !Graph.TryApply(declined.WesternJourney, "WESTERN_EDGE_025"),
            "declining completes the route and cannot apply the old edge025.");
        Check(JsonSerializer.Serialize(declined.SpinozaCauseNetwork)
              == JsonSerializer.Serialize(Precursor().SpinozaCauseNetwork),
            "declining does not rewrite the old Spinoza record.");

        foreach (LeibnizHarmonyAction probe in new[]
                 { LeibnizHarmonyAction.DelayOne, LeibnizHarmonyAction.ObserveBoth })
        foreach (LeibnizHarmonyAction decision in new[]
                 {
                     LeibnizHarmonyAction.Independent, LeibnizHarmonyAction.Coordinated,
                     LeibnizHarmonyAction.KeepSpinoza,
                 })
        {
            PhilosophyRunState shared = Commit(Precursor(), LeibnizHarmonyAction.Accept);
            shared = Commit(shared, probe);
            Check(shared.LeibnizHarmony!.Probe == (probe == LeibnizHarmonyAction.DelayOne
                    ? LeibnizClockProbe.DelayOne : LeibnizClockProbe.ObserveBoth),
                "the probe choice is recorded.");
            shared = Commit(shared, LeibnizHarmonyAction.Continue);
            shared = Commit(shared, decision);
            shared = Commit(shared, LeibnizHarmonyAction.Leave);

            LeibnizHarmonyState state = shared.LeibnizHarmony!;
            WesternJourneyState journey = shared.WesternJourney!;
            Check(state.Closed && state.IsUsable(), "the completed event stays usable.");
            Check(journey.LastFixedAct == 3
                && !journey.ResolvedQuestionActs.Contains(2)
                && !journey.ResolvedQuestionActs.Contains(3),
                "the event takes the third Fixed slot without consuming a Question.");

            Check(Graph.IsComplete(journey) && !Graph.TryApply(journey, "WESTERN_EDGE_025"),
                "every ending completes the route and cannot apply the old edge025.");
            Check(JsonSerializer.Serialize(shared.SpinozaCauseNetwork)
                  == JsonSerializer.Serialize(Precursor().SpinozaCauseNetwork),
                "the event does not rewrite the old Spinoza record.");

            bool adopted = decision is LeibnizHarmonyAction.Independent or LeibnizHarmonyAction.Coordinated;
            if (adopted)
            {
                Check(journey.CurrentNodeId == LeibnizHarmonyState.AdoptedNodeId
                    && journey.CompletedEdgeIds.Contains(LeibnizHarmonyState.CompletionEdgeId)
                    && journey.SeenThinkerIds.Contains("GOTTFRIED_WILHELM_LEIBNIZ")
                    && !journey.RetainedEnding
                    && !journey.DeclinedFixedActs.Contains(3),
                    "adopting reaches the Leibniz terminal through edge050.");
                Check(state.Disposition == (decision == LeibnizHarmonyAction.Independent
                        ? LeibnizDisposition.Independent : LeibnizDisposition.Coordinated),
                    "the adopted disposition is recorded.");
            }
            else
            {
                Check(journey.CurrentNodeId == LeibnizHarmonyState.SourceNodeId
                    && !journey.CompletedEdgeIds.Contains(LeibnizHarmonyState.CompletionEdgeId)
                    && !journey.SeenThinkerIds.Contains("GOTTFRIED_WILHELM_LEIBNIZ")
                    && journey.DeclinedFixedActs.Contains(3)
                    && journey.RetainedEnding,
                    "keeping Spinoza declines Fixed3 and retains the ending.");
                Check(state.Disposition == LeibnizDisposition.Kept, "the kept disposition is recorded.");
            }
        }

        // The new terminal edge050 requires the old edge023 and cannot be applied without it.
        PhilosophyRunState oldTerminal = Precursor();
        Check(Graph.TryApply(oldTerminal.WesternJourney!, "WESTERN_EDGE_024"),
            "the old edge024 applies to the precursor.");
        Check(oldTerminal.WesternJourney!.CurrentNodeId == LeibnizHarmonyState.SourceNodeId
            && oldTerminal.WesternJourney.SeenThinkerIds.Contains("GOTTFRIED_WILHELM_LEIBNIZ"),
            "the old edge024 keeps the Spinoza node and sees Leibniz.");
        Check(!Graph.TryApply(oldTerminal.WesternJourney, LeibnizHarmonyState.CompletionEdgeId),
            "the new edge050 is rejected after the old edge024.");
        Check(!LeibnizHarmonyFlow.IsRouteConsistent(oldTerminal.LeibnizHarmony!,
            oldTerminal.WesternJourney, oldTerminal.SpinozaCauseNetwork!),
            "the event is inconsistent after the old edge024.");
        PhilosophyRunState noPrecursor = Precursor();
        noPrecursor.WesternJourney!.CompletedEdgeIds.Remove(SpinozaCauseNetworkState.CompletionEdgeId);
        Check(!Graph.TryApply(noPrecursor.WesternJourney, LeibnizHarmonyState.CompletionEdgeId),
            "the new edge050 requires the old edge023.");

        // Absence of Leibniz on an old shared save stays absent.
        PhilosophyRunState absent = Precursor();
        absent.LeibnizHarmony = null;
        Check(Reload(absent).LeibnizHarmony is null, "an old save without Leibniz stays without Leibniz.");

        // A legal pending action is usable and rejects a double prepare.
        PhilosophyRunState pending = Precursor();
        Check(LeibnizHarmonyFlow.TryPrepare(pending.LeibnizHarmony!, LeibnizHarmonyAction.Accept),
            "a legal action prepares.");
        pending = Reload(pending);
        Check(pending.LeibnizHarmony!.IsUsable(), "a pending legal action is usable.");
        Check(!LeibnizHarmonyFlow.TryPrepare(pending.LeibnizHarmony, LeibnizHarmonyAction.Decline),
            "a pending action rejects a second prepare.");

        // Missing required fields are rejected.
        foreach (string field in new[] { "Version", "Closed", "ConfirmedActions", "Stage", "PendingAction" })
        {
            PhilosophyRunState shared = Precursor();
            JsonObject node = JsonNode.Parse(JsonSerializer.Serialize(shared))!.AsObject();
            node["LeibnizHarmony"]!.AsObject().Remove(field);
            Check(!PhilosophyRunStateCodec.Decode(Convert.ToHexString(
                    System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()))).LeibnizHarmony!.IsUsable(),
                $"a state missing {field} is rejected.");
        }

        // Invalid version, phantom history and forged probe are rejected.
        PhilosophyRunState badVersion = Precursor();
        badVersion.LeibnizHarmony!.Version = 99;
        Check(!Reload(badVersion).LeibnizHarmony!.IsUsable(), "an unknown version is rejected.");

        PhilosophyRunState phantom = Precursor();
        phantom.LeibnizHarmony!.ConfirmedActions.Add(LeibnizHarmonyAction.Leave);
        Check(!Reload(phantom).LeibnizHarmony!.IsUsable(), "a phantom history is rejected.");

        PhilosophyRunState forged = Precursor();
        forged.LeibnizHarmony!.Probe = LeibnizClockProbe.ObserveBoth;
        Check(!Reload(forged).LeibnizHarmony!.IsUsable(), "a forged probe is rejected.");

        // A state bound to a different precursor is inconsistent.
        PhilosophyRunState mismatched = Precursor();
        mismatched.LeibnizHarmony!.SpinozaInstanceId = "SPINOZA_CAUSE_NETWORK:1:2";
        Check(!LeibnizHarmonyFlow.IsRouteConsistent(mismatched.LeibnizHarmony!,
            mismatched.WesternJourney!, mismatched.SpinozaCauseNetwork!),
            "a state bound to another precursor is inconsistent.");

        // A state bound to a different route node is inconsistent.
        PhilosophyRunState wrongRoute = Precursor();
        wrongRoute.WesternJourney!.CurrentNodeId = "DESCARTES__SELF_AND_OTHER__CORE";
        Check(!LeibnizHarmonyFlow.IsRouteConsistent(wrongRoute.LeibnizHarmony!,
            wrongRoute.WesternJourney!, wrongRoute.SpinozaCauseNetwork!),
            "a state bound to another route node is inconsistent.");

        Console.WriteLine("Leibniz event checks passed: 7 paths, adoption, keep and save integrity.");
    }

    private static void CheckPractice()
    {
        const string key = "2:4:98";

        LeibnizCombatPracticeState independent = LeibnizCombatPracticeState.CreateDefault();
        Check(independent.BeginCombat(key), "a first valid combat binds the receipt.");
        Check(independent.StartTurn(key, 1), "the first turn starts.");
        Check(independent.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Skill)
              == LeibnizPracticeReward.None, "the first manual Skill grants nothing.");
        Check(LeibnizCombatPracticeState.TryRestore(independent.Encode(), out var partialSkill),
            "a partial practice receipt restores.");
        Check(partialSkill.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Skill)
              == LeibnizPracticeReward.Draw1, "the second manual Skill draws once after a partial restore.");
        independent = partialSkill;
        Check(independent.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Skill)
              == LeibnizPracticeReward.None, "a third manual Skill grants nothing.");
        Check(independent.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Attack)
              == LeibnizPracticeReward.None, "the first manual Attack grants nothing.");
        Check(independent.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Attack)
              == LeibnizPracticeReward.Block4, "the second manual Attack grants Block4.");
        Check(independent.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Attack)
              == LeibnizPracticeReward.None, "a third manual Attack grants nothing.");

        LeibnizCombatPracticeState attackFirst = LeibnizCombatPracticeState.CreateDefault();
        attackFirst.BeginCombat(key);
        attackFirst.StartTurn(key, 1);
        Check(attackFirst.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Attack)
              == LeibnizPracticeReward.None, "the first manual Attack grants nothing.");
        Check(attackFirst.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Attack)
              == LeibnizPracticeReward.Block4, "the second manual Attack grants Block4 regardless of order.");
        Check(attackFirst.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Skill)
              == LeibnizPracticeReward.None, "the first manual Skill grants nothing.");
        Check(attackFirst.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Skill)
              == LeibnizPracticeReward.Draw1, "the second manual Skill draws once regardless of order.");

        string claimed = independent.Encode();
        Check(LeibnizCombatPracticeState.TryRestore(claimed, out var reloaded)
              && reloaded.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Attack)
              == LeibnizPracticeReward.None,
            "a spent Block receipt survives reload and cannot pay again.");
        Check(!reloaded.BeginCombat(key)
              && reloaded.ObserveManual(key, 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Skill)
              == LeibnizPracticeReward.None,
            "a repeated combat-start callback cannot reset the receipt.");
        Check(reloaded.BeginCombat("2:5:99")
              && reloaded.StartTurn("2:5:99", 1)
              && reloaded.ObserveManual("2:5:99", 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Attack)
              == LeibnizPracticeReward.None
              && reloaded.ObserveManual("2:5:99", 1, LeibnizDisposition.Independent, LeibnizPracticeCardKind.Attack)
              == LeibnizPracticeReward.Block4,
            "a genuine new combat permits a fresh Block claim.");

        LeibnizCombatPracticeState coordinated = LeibnizCombatPracticeState.CreateDefault();
        coordinated.BeginCombat(key);
        coordinated.StartTurn(key, 1);
        coordinated.ObserveManual(key, 1, LeibnizDisposition.Coordinated, LeibnizPracticeCardKind.Attack);
        coordinated.ObserveManual(key, 1, LeibnizDisposition.Coordinated, LeibnizPracticeCardKind.Skill);
        Check(coordinated.StartTurn(key, 2), "the second turn starts.");
        Check(coordinated.TryClaimEnergy(key, 2, LeibnizDisposition.Coordinated),
            "the coordinated first turn grants Energy once at turn two.");
        Check(!coordinated.TryClaimEnergy(key, 2, LeibnizDisposition.Coordinated),
            "the coordinated Energy cannot be claimed twice.");
        string claimedEnergy = coordinated.Encode();
        Check(LeibnizCombatPracticeState.TryRestore(claimedEnergy, out var reloadedEnergy)
              && !reloadedEnergy.TryClaimEnergy(key, 2, LeibnizDisposition.Coordinated),
            "a spent Energy receipt survives reload and cannot pay again.");

        LeibnizCombatPracticeState missingClass = LeibnizCombatPracticeState.CreateDefault();
        missingClass.BeginCombat(key);
        missingClass.StartTurn(key, 1);
        missingClass.ObserveManual(key, 1, LeibnizDisposition.Coordinated, LeibnizPracticeCardKind.Attack);
        missingClass.StartTurn(key, 2);
        Check(!missingClass.TryClaimEnergy(key, 2, LeibnizDisposition.Coordinated),
            "a first turn missing a Skill cannot claim Energy at turn two.");
        missingClass.ObserveManual(key, 2, LeibnizDisposition.Coordinated, LeibnizPracticeCardKind.Skill);
        Check(!missingClass.TryClaimEnergy(key, 2, LeibnizDisposition.Coordinated),
            "a later Skill cannot retroactively qualify the first turn.");

        string Mutate(Action<JsonObject> mutate)
        {
            JsonObject node = JsonNode.Parse(claimed)!.AsObject();
            mutate(node);
            return node.ToJsonString();
        }
        string MutateEnergy(Action<JsonObject> mutate)
        {
            JsonObject node = JsonNode.Parse(claimedEnergy)!.AsObject();
            mutate(node);
            return node.ToJsonString();
        }
        foreach (string payload in new[]
        {
            "not-json", new string('x', 4097),
            claimed.Replace("\"Version\":1", "\"Version\":1,\"Version\":1", StringComparison.Ordinal),
            Mutate(n => n["Version"] = 99),
            Mutate(n => n["CombatKey"] = ""),
            Mutate(n => n["CombatKey"] = new string('9', 129)),
            Mutate(n => n["AttackCount"] = 3),
            Mutate(n => n["SkillCount"] = 3),
            Mutate(n => n["BlockClaimed"] = false),
            Mutate(n => { n["DrawClaimed"] = true; n["SkillCount"] = 1; }),
            Mutate(n => { n["EnergyClaimed"] = true; n["FirstTurnAttack"] = false; }),
            MutateEnergy(n => n["TurnNumber"] = 1),
            MutateEnergy(n => n["TurnNumber"] = 0),
            MutateEnergy(n => { n["AttackCount"] = 2; n["BlockClaimed"] = true; }),
            Mutate(n => { n["FirstTurnAttack"] = true; n["FirstTurnSkill"] = true; }),
            Mutate(n => { n["FirstTurnAttack"] = true; n["FirstTurnSkill"] = true; n["EnergyClaimed"] = true; }),
            Mutate(n => n["Unknown"] = true),
            Mutate(n => n.Remove("FirstTurnSkill")),
        }) Check(!LeibnizCombatPracticeState.TryRestore(payload, out _),
            "Malformed or inconsistent practice receipt was accepted: " + payload);
        Check(!LeibnizCombatPracticeState.CreateDefault().BeginCombat(" "),
            "an illegal key cannot bind a combat.");
        Console.WriteLine("Leibniz practice checks passed: independent and coordinated rewards, combat identity and strict receipts.");
    }
}
