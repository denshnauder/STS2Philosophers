using System.Text.Json;
using STS2Philosophers;

internal static class AnselmUnfinishedProofChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static (PhilosophyRunState, WesternRouteGraph) Fixture()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start(AnselmUnfinishedProofState.RequiredNodeId);
        Check(graph.TryReserveQuestion(journey, 1)
            && graph.TryDecline(journey, "Fixed", 2)
            && graph.TryReserveQuestion(journey, 2)
            && !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_012"),
            "The current production knowledge branch uses real first- and second-act events, not a fabricated 012.");
        SextusOpposedAppearancesState sextus = SextusOpposedAppearancesState.Create("SEXTUS:TEST:1");
        foreach (SextusAppearancesAction action in new[]
            { SextusAppearancesAction.AcceptInvitation, SextusAppearancesAction.SuspendAssent,
                SextusAppearancesAction.Leave })
            Check(SextusOpposedAppearancesFlow.TryPrepare(sextus, action)
                && SextusOpposedAppearancesFlow.TryCommitPending(sextus),
                "A real Sextus event choice commits.");
        DescartesMethodicReconstructionState descartes =
            DescartesMethodicReconstructionState.Create("DESCARTES:TEST:2");
        foreach (DescartesReconstructionAction action in new[]
            { DescartesReconstructionAction.DeclineInvitation, DescartesReconstructionAction.Leave })
            Check(DescartesMethodicReconstructionFlow.TryPrepare(descartes, action)
                && DescartesMethodicReconstructionFlow.TryCommitPending(descartes),
                "The second Fixed turn is explicitly declined.");
        PyrrhoReportedCrossingState pyrrho = PyrrhoReportedCrossingState.Create(
            "PYRRHO:TEST:2", sextus, descartes);
        foreach (PyrrhoReportedCrossingAction action in new[]
            { PyrrhoReportedCrossingAction.CiteConflict,
                PyrrhoReportedCrossingAction.InspectAndCross, PyrrhoReportedCrossingAction.Leave })
            Check(PyrrhoReportedCrossingFlow.TryPrepare(pyrrho, action)
                && PyrrhoReportedCrossingFlow.TryCommitPending(pyrrho, journey, sextus, descartes),
                "The second Question is actually completed by Pyrrho.");
        PhilosophyRunState shared = new()
        {
            WesternJourney = journey,
            SextusOpposedAppearances = sextus,
            DescartesMethodicReconstruction = descartes,
            PyrrhoReportedCrossing = pyrrho,
            AnselmUnfinishedProof = AnselmUnfinishedProofState.Create("ANSELM:TEST:3", pyrrho.InstanceId),
        };
        return (Reload(shared), graph);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static PhilosophyRunState Commit(PhilosophyRunState shared,
        WesternRouteGraph graph, AnselmProofAction action)
    {
        AnselmUnfinishedProofState state = shared.AnselmUnfinishedProof!;
        Check(AnselmUnfinishedProofFlow.TryPrepare(state, action), $"Prepare {action}.");
        shared = Reload(shared);
        state = shared.AnselmUnfinishedProof!;
        Check(AnselmUnfinishedProofFlow.TryCommitPending(state, shared.WesternJourney!,
            shared.PyrrhoReportedCrossing!, graph), $"Commit {action} after a prepared save.");
        Check(!AnselmUnfinishedProofFlow.TryCommitPending(state, shared.WesternJourney!,
            shared.PyrrhoReportedCrossing!, graph), "The confirmed action cannot replay.");
        shared = Reload(shared);
        Check(AnselmUnfinishedProofFlow.IsRouteConsistent(shared.AnselmUnfinishedProof!,
            shared.WesternJourney!, shared.PyrrhoReportedCrossing!),
            "The event and route survive save/reload together.");
        return shared;
    }

    public static void Run()
    {
        (PhilosophyRunState decline, WesternRouteGraph declineGraph) = Fixture();
        decline = Commit(decline, declineGraph, AnselmProofAction.Decline);
        decline = Commit(decline, declineGraph, AnselmProofAction.Close);
        Check(decline.AnselmUnfinishedProof!.Closed
            && decline.WesternJourney!.LastFixedAct == 2
            && !decline.WesternJourney.ResolvedQuestionActs.Contains(3),
            "Declining the invitation preserves both third-act opportunities.");

        AnselmProofAction[] scopes = [AnselmProofAction.UniqueBeing,
            AnselmProofAction.UniversalMaximum, AnselmProofAction.QuestionOnly];
        AnselmProofAction[] objections = [AnselmProofAction.GenericRuleAttacked,
            AnselmProofAction.DistinctScope, AnselmProofAction.Undecided];
        AnselmProofAction[] dispositions = [AnselmProofAction.Publish,
            AnselmProofAction.Revise, AnselmProofAction.Withdraw,
            AnselmProofAction.LeaveUnanswered];
        foreach (AnselmProofAction scope in scopes)
        foreach (AnselmProofAction objection in objections)
        foreach (AnselmProofAction disposition in dispositions)
        {
            (PhilosophyRunState shared, WesternRouteGraph graph) = Fixture();
            shared = Commit(shared, graph, AnselmProofAction.Accept);
            Check(shared.WesternJourney!.LastFixedAct == 3
                && shared.WesternJourney.DeclinedFixedActs.Contains(3)
                && shared.WesternJourney.ResolvedQuestionActs.Contains(3)
                && !shared.WesternJourney.RetainedEnding,
                "Accepting alone takes exactly the third Fixed and Question turns.");
            shared = Commit(shared, graph, scope);
            shared = Commit(shared, graph, objection);
            shared = Commit(shared, graph, disposition);
            shared = Commit(shared, graph, AnselmProofAction.Close);
            AnselmUnfinishedProofState state = shared.AnselmUnfinishedProof!;
            Check(state.Closed && state.OriginalScope != AnselmProofScope.None
                && state.Objection != AnselmProofObjection.None
                && state.Disposition != AnselmProofDisposition.None
                && shared.WesternJourney!.RetainedEnding
                && state.FinalScope == (disposition == AnselmProofAction.Publish
                    ? state.OriginalScope : disposition == AnselmProofAction.Revise
                        ? AnselmUnfinishedProofFlow.Narrow(state.OriginalScope)
                        : AnselmProofScope.None),
                "The original assertion, challenge, final scope and one reply remain distinct.");
        }

        (PhilosophyRunState invalid, _) = Fixture();
        invalid.PyrrhoReportedCrossing!.Closed = false;
        Check(!AnselmUnfinishedProofFlow.IsRouteConsistent(invalid.AnselmUnfinishedProof!,
            invalid.WesternJourney!, invalid.PyrrhoReportedCrossing),
            "An unfinished predecessor cannot authorize the third-act text.");
        (invalid, _) = Fixture();
        invalid.WesternJourney!.ResolvedQuestionActs.Add(3);
        Check(!AnselmUnfinishedProofFlow.IsRouteConsistent(invalid.AnselmUnfinishedProof!,
            invalid.WesternJourney, invalid.PyrrhoReportedCrossing!),
            "A pre-spent third Question cannot be reused.");

        AnselmUnfinishedProofTriggerContext eligible = new(true, true, true, 2, true, 1,
            AnselmUnfinishedProofState.RequiredNodeId, 2, true, true, true,
            false, false, true, true, true, false, false, false, false, true);
        Check(AnselmUnfinishedProofTriggerPolicy.ShouldInsert(eligible),
            "Only the actual completed third-act combat after Pyrrho admits the independent question.");
        foreach (AnselmUnfinishedProofTriggerContext blocked in new[]
        {
            eligible with { CurrentActIndex = 1 },
            eligible with { SourceIsCompletedNonBossCombat = false },
            eligible with { LastFixedAct = 1 },
            eligible with { FirstQuestionResolved = false },
            eligible with { SecondFixedDeclined = false },
            eligible with { SecondQuestionResolved = false },
            eligible with { ThirdQuestionResolved = true },
            eligible with { HasRequiredRelic = false },
            eligible with { PyrrhoClosed = false },
            eligible with { PyrrhoRouteConsistent = false },
        }) Check(!AnselmUnfinishedProofTriggerPolicy.ShouldInsert(blocked),
            "A missing or conflicting native/route receipt must block Anselm.");

        AnselmCombatPracticeState practice = new();
        Check(practice.TryClaim("2:4:12") && !practice.TryClaim("2:4:12")
            && AnselmCombatPracticeState.TryRestore(practice.Encode(), out var restored)
            && !restored.TryClaim("2:4:12") && restored.TryClaim("2:5:13"),
            "The real combat identity pays once and survives save/load.");
        Check(!AnselmCombatPracticeState.TryRestore("{\"Version\":99}", out _),
            "Unknown practice payloads remain inert.");
        ValidateLocalization();
        Console.WriteLine("Anselm proof checks passed: 36 saved outcomes, third-act opportunity gates, predecessor facts, bilingual pages and combat receipts.");
    }

    private static void ValidateLocalization()
    {
        foreach (string language in new[] { "eng", "zhs" })
        {
            using JsonDocument events = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            JsonElement text = events.RootElement;
            Check(text.TryGetProperty("ANSELM_UNFINISHED_PROOF.title", out _),
                "The event title is bilingual.");
            foreach (AnselmProofStage stage in Enum.GetValues<AnselmProofStage>())
            {
                foreach (AnselmProofScope scope in stage is AnselmProofStage.Objection
                    or AnselmProofStage.Decision or >= AnselmProofStage.ResultPublished
                    ? new[] { AnselmProofScope.UniqueBeing, AnselmProofScope.UniversalMaximum,
                        AnselmProofScope.QuestionOnly }
                    : new[] { AnselmProofScope.None })
                foreach (AnselmProofObjection objection in stage is AnselmProofStage.Decision
                    or >= AnselmProofStage.ResultPublished
                    ? new[] { AnselmProofObjection.GenericRuleAttacked,
                        AnselmProofObjection.DistinctScope, AnselmProofObjection.Undecided }
                    : new[] { AnselmProofObjection.None })
                {
                    AnselmUnfinishedProofState state = AnselmUnfinishedProofState.Create("translation", "pyrrho");
                    state.Stage = stage;
                    state.OriginalScope = scope;
                    state.Objection = objection;
                    string key = $"ANSELM_UNFINISHED_PROOF.pages.{AnselmUnfinishedProofFlow.DescriptionKey(state)}.description";
                    Check(text.TryGetProperty(key, out _), $"Missing {language} {key}.");
                }
                string page = $"ANSELM_UNFINISHED_PROOF.pages.{AnselmUnfinishedProofFlow.StageKey(stage)}";
                foreach (AnselmProofAction action in AnselmUnfinishedProofFlow.Options(stage))
                {
                    string option = $"{page}.options.{AnselmUnfinishedProofFlow.ActionKey(action)}";
                    Check(text.TryGetProperty($"{option}.title", out _)
                        && text.TryGetProperty($"{option}.description", out _),
                        $"Missing {language} {option}.");
                }
            }
            using JsonDocument relics = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/relics.json"));
            foreach (string outcome in new[] { "published", "revised", "withdrawn" })
                Check(relics.RootElement.TryGetProperty($"SOCRATES_QUESTION_CUP.anselm.{outcome}.title", out _)
                    && relics.RootElement.TryGetProperty($"SOCRATES_QUESTION_CUP.anselm.{outcome}.description", out _),
                    "Every combat practice has a bilingual relic tip.");
        }
    }
}
