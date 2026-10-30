using STS2Philosophers;

internal static class WesternQuestionNodeChecks
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));
    private static readonly WesternRouteGraph Graph = WesternRouteGraph.LoadEmbedded();

    public static void Run()
    {
        int count = 0;
        foreach (var (kind, initial, act, carrier) in Cases())
        {
            PhilosophyRunState shared = Reload(initial);
            string before = PhilosophyRunStateCodec.Encode(shared);
            Check(WesternQuestionNodePolicy.Select(shared, act, carrier, true) == kind,
                $"The real {kind} route selects its question without a completed-combat prerequisite.");
            Check(PhilosophyRunStateCodec.Encode(shared) == before,
                "Looking at native eligibility does not reserve, mutate, or manufacture a prior event.");
            Check(!WesternQuestionNodePolicy.TryStart(shared, act, carrier, true, "42", WesternQuestionKind.None)
                && PhilosophyRunStateCodec.Encode(shared) == before, "An unmatched selection cannot partially initialize.");
            foreach (var (wrongAct, wrongCarrier) in new[] { (act, "OTHER"), (-1, carrier), (3, carrier) })
                Check(WesternQuestionNodePolicy.Select(shared, wrongAct, wrongCarrier, true) == WesternQuestionKind.None,
                    "Wrong act/carrier leaves the native event unchanged.");
            PhilosophyRunState wrongDoctrine = Reload(shared);
            wrongDoctrine.CurrentDoctrine!.DoctrineId = "OTHER";
            Check(WesternQuestionNodePolicy.Select(wrongDoctrine, act, carrier, true) == WesternQuestionKind.None,
                "A real relic cannot replace an inconsistent current doctrine record.");
            PhilosophyRunState badThinker = Reload(shared); badThinker.CurrentDoctrine!.ThinkerId = "UNSEEN";
            Check(WesternQuestionNodePolicy.Select(badThinker, act, carrier, true) == WesternQuestionKind.None,
                "A doctrine thinker absent from actual route history cannot authorize a question.");
            PhilosophyRunState currentMetadata = Reload(shared);
            currentMetadata.CurrentDoctrine!.ThinkerId = Graph.GetNode(currentMetadata.WesternJourney!.CurrentNodeId).ThinkerId;
            Check(WesternQuestionNodePolicy.Select(currentMetadata, act, carrier, true) == kind,
                "Metadata may follow the adopted thinker while the actual original carrier remains valid.");
            PhilosophyRunState badWindow = Reload(shared); badWindow.WesternJourney!.CandidateWindows["BROKEN"] = null!;
            string broken = PhilosophyRunStateCodec.Encode(badWindow);
            Check(WesternQuestionNodePolicy.Select(badWindow, act, carrier, true) == WesternQuestionKind.None
                && !WesternQuestionNodePolicy.TryStart(badWindow, act, carrier, true, "42", kind)
                && PhilosophyRunStateCodec.Encode(badWindow) == broken,
                "A malformed candidate window is preserved instead of throwing or half-initializing a question.");
            PhilosophyRunState spent = Reload(shared); spent.WesternJourney!.ResolvedQuestionActs.Add(act + 1);
            Check(WesternQuestionNodePolicy.Select(spent, act, carrier, true) == WesternQuestionKind.None,
                "An already consumed question is never reclaimed.");
            Check(WesternQuestionNodePolicy.TryStart(shared, act, carrier, true, "42", kind),
                $"The selected native {kind} event creates its actual person state.");
            shared = Reload(shared);
            bool reservesAtEntry = kind is not (WesternQuestionKind.Protagoras or WesternQuestionKind.Ockham
                or WesternQuestionKind.Luther or WesternQuestionKind.Anselm);
            Check(shared.WesternJourney!.ResolvedQuestionActs.Contains(act + 1) == reservesAtEntry,
                "Native selection preserves the original entry-versus-acceptance question reservation timing.");
            Check(shared.WesternJourney.CurrentNodeId == initial.WesternJourney!.CurrentNodeId
                && shared.WesternJourney.CompletedEdgeIds.SetEquals(initial.WesternJourney.CompletedEdgeIds)
                && shared.WesternJourney.SeenThinkerIds.SetEquals(initial.WesternJourney.SeenThinkerIds)
                && shared.WesternJourney.LastFixedAct == initial.WesternJourney.LastFixedAct
                    + (kind == WesternQuestionKind.Anaximander ? 1 : 0),
                "Starting the event creates no adopted edge, thinker, or unexpected Fixed consumption.");
            Check(kind != WesternQuestionKind.Anaximander || shared.WesternJourney.DeclinedFixedActs.Contains(2),
                "The no-candidate Anaximander branch resolves its existing second Fixed vacancy once.");
            string started = PhilosophyRunStateCodec.Encode(shared);
            Check(WesternQuestionNodePolicy.Select(shared, act, carrier, true) == WesternQuestionKind.None
                && !WesternQuestionNodePolicy.TryStart(shared, act, carrier, true, "42", kind)
                && PhilosophyRunStateCodec.Encode(shared) == started,
                "A saved open event blocks another offer without spending a second budget or replacing its identity.");
            count++;
        }
        Check(count == 12, "Every implemented Question consumer belongs to the migration.");
        var knowledge = Fresh(SextusOpposedAppearancesState.RequiredNodeId, "SOCRATES_QUESTION_CUP");
        Check(WesternQuestionNodePolicy.Select(knowledge, 0, "SOCRATES_QUESTION_CUP", false) == WesternQuestionKind.None,
            "Sextus still requires an actual native fork.");
        var missingPrior = Language(); missingPrior.GorgiasSpeechInquiry = null;
        Check(WesternQuestionNodePolicy.Select(missingPrior, 1, "ARISTOTLE_CATEGORY_TABLET", true) == WesternQuestionKind.None,
            "Ockham cannot invent a closed Gorgias predecessor.");
        var badPrior = Institution(); badPrior.AugustinePromise!.ConfirmedActions.Clear();
        Check(WesternQuestionNodePolicy.Select(badPrior, 1, "PLATO_CIVIC_SEAL", true) == WesternQuestionKind.None,
            "Luther cannot use a forged closed predecessor.");
        var existing = Fresh(ZenoCivicChallengeState.SourceNodeId, "PLATO_CIVIC_SEAL");
        existing.ZenoCivicChallenge = ZenoCivicChallengeState.Create("LEGACY");
        string legacy = PhilosophyRunStateCodec.Encode(existing);
        Check(WesternQuestionNodePolicy.Select(existing, 0, "PLATO_CIVIC_SEAL", true) == WesternQuestionKind.None
            && PhilosophyRunStateCodec.Encode(existing) == legacy,
            "An old open question is preserved for its existing native room, never replaced with a new invitation.");
        foreach (string entry in new[] { "DESCARTES__SELF_AND_OTHER__CORE", "ROUSSEAU__HISTORY_AND_POWER__CORE" })
        {
            var shared = Fresh(entry, entry.StartsWith("DESCARTES") ? "DESCARTES_THINKING_LENS" : "ROUSSEAU_UNCARVED_STONE");
            Check(WesternQuestionNodePolicy.Select(shared, 0, shared.CurrentDoctrine!.DoctrineId, true) == WesternQuestionKind.None,
                "Unimplemented questions and unrelated practices are not moved into the pool.");
        }
        Console.WriteLine("Western question node checks passed: all 12 actual consumers with real predecessor histories, deterministic non-mutating selection, precise entry/acceptance budgets, saved identity and duplicate guards, no invented Fixed/edge/relic, fork and current-doctrine gates, and legacy open-record preservation.");
    }

    private static IEnumerable<(WesternQuestionKind Kind, PhilosophyRunState Shared, int Act, string Carrier)> Cases()
    {
        yield return (WesternQuestionKind.Protagoras, Fresh(ProtagorasSharedDeliberationState.SourceNodeId, "SOCRATES_BALANCE_WEIGHT"), 0, "SOCRATES_BALANCE_WEIGHT");
        yield return (WesternQuestionKind.Parmenides, Fresh(ParmenidesInquiryState.RequiredNodeId, "HERACLITUS_LYRE"), 0, "HERACLITUS_LYRE");
        yield return (WesternQuestionKind.Sextus, Fresh(SextusOpposedAppearancesState.RequiredNodeId, "SOCRATES_QUESTION_CUP"), 0, "SOCRATES_QUESTION_CUP");
        yield return (WesternQuestionKind.Gorgias, Fresh(GorgiasSpeechInquiryState.RequiredNodeId, "ARISTOTLE_CATEGORY_TABLET"), 0, "ARISTOTLE_CATEGORY_TABLET");
        yield return (WesternQuestionKind.Zeno, Fresh(ZenoCivicChallengeState.SourceNodeId, "PLATO_CIVIC_SEAL"), 0, "PLATO_CIVIC_SEAL");
        yield return (WesternQuestionKind.Anaximander, Being(adopt: false), 1, "HERACLITUS_LYRE");
        yield return (WesternQuestionKind.Empedocles, Atomist(adopt: true), 1, "HERACLITUS_LYRE");
        yield return (WesternQuestionKind.Anaxagoras, Atomist(adopt: false), 1, "HERACLITUS_LYRE");
        yield return (WesternQuestionKind.Pyrrho, Knowledge(closePyrrho: false), 1, "SOCRATES_QUESTION_CUP");
        yield return (WesternQuestionKind.Ockham, Language(), 1, "ARISTOTLE_CATEGORY_TABLET");
        yield return (WesternQuestionKind.Luther, Institution(), 1, "PLATO_CIVIC_SEAL");
        yield return (WesternQuestionKind.Anselm, Knowledge(closePyrrho: true), 2, "SOCRATES_QUESTION_CUP");
    }

    private static PhilosophyRunState Fresh(string node, string carrier) => new()
    {
        WesternJourney = Graph.Start(node),
        CurrentDoctrine = new() { ThinkerId = Graph.GetNode(node).ThinkerId, DoctrineId = carrier },
    };

    private static PhilosophyRunState Being(bool adopt)
    {
        var shared = Fresh(ParmenidesInquiryState.RequiredNodeId, "HERACLITUS_LYRE");
        var journey = shared.WesternJourney!;
        Check(Graph.TryReserveQuestion(journey, 1), "Real first-act being question.");
        var prior = shared.ParmenidesInquiry = ParmenidesInquiryState.Create("PARMENIDES:42:1");
        ParmenidesInquiryAction[] actions = adopt
            ? [ParmenidesInquiryAction.AcceptInvitation, ParmenidesInquiryAction.SpeakOfBeing,
                ParmenidesInquiryAction.AdoptBoundary, ParmenidesInquiryAction.Leave]
            : [ParmenidesInquiryAction.DeclineInvitation, ParmenidesInquiryAction.Leave];
        foreach (var action in actions)
            Check(ParmenidesInquiryFlow.TryPrepare(prior, action)
                && ParmenidesInquiryFlow.TryCommitPending(prior, journey, Graph), "Actual Parmenides history.");
        return shared;
    }

    private static PhilosophyRunState Atomist(bool adopt)
    {
        var shared = Being(adopt: true);
        // A refusal happens at the act's own position; adopting the event moves
        // the position itself.
        if (!adopt) shared.WesternJourney!.LastFixedAct = 2;
        var prior = shared.DemocritusArrangement = DemocritusArrangementState.Create("DEMOCRITUS:42:2");
        DemocritusArrangementAction[] actions = adopt
            ? [DemocritusArrangementAction.AcceptInvitation, DemocritusArrangementAction.StudyMotion,
                DemocritusArrangementAction.LeaveGap, DemocritusArrangementAction.AdoptAtoms, DemocritusArrangementAction.Leave]
            : [DemocritusArrangementAction.DeclineInvitation, DemocritusArrangementAction.Leave];
        foreach (var action in actions)
            Check(DemocritusArrangementFlow.TryPrepare(prior, action)
                && DemocritusArrangementFlow.TryCommitPending(prior, shared.WesternJourney!, Graph), "Actual Fixed2 Democritus history.");
        return shared;
    }

    private static PhilosophyRunState Language()
    {
        var shared = Fresh(GorgiasSpeechInquiryState.RequiredNodeId, "ARISTOTLE_CATEGORY_TABLET");
        Check(Graph.TryReserveQuestion(shared.WesternJourney!, 1), "Actual language Question1.");
        var prior = shared.GorgiasSpeechInquiry = GorgiasSpeechInquiryState.Create("GORGIAS:42:1");
        foreach (var action in new[] { GorgiasSpeechInquiryAction.AcceptInvitation, GorgiasSpeechInquiryAction.AssertContents,
            GorgiasSpeechInquiryAction.SeparateBeliefFromProof, GorgiasSpeechInquiryAction.RecordBoundary, GorgiasSpeechInquiryAction.Leave })
            Check(GorgiasSpeechInquiryFlow.TryPrepare(prior, action)
                && GorgiasSpeechInquiryFlow.TryCommitPending(prior, shared.WesternJourney!), "Actual Gorgias predecessor.");
        return shared;
    }

    private static PhilosophyRunState Institution()
    {
        var shared = Fresh(ZenoCivicChallengeState.SourceNodeId, "PLATO_CIVIC_SEAL");
        Check(Graph.TryReserveQuestion(shared.WesternJourney!, 1), "Actual institution Question1.");
        var zeno = shared.ZenoCivicChallenge = ZenoCivicChallengeState.Create("ZENO:42:1");
        foreach (var action in new[] { ZenoCivicAction.AcceptInvitation, ZenoCivicAction.DemandPublicRule,
            ZenoCivicAction.PublishPublicObjection, ZenoCivicAction.Leave })
            Check(ZenoCivicChallengeFlow.TryPrepare(zeno, action)
                && ZenoCivicChallengeFlow.TryCommitPending(zeno, shared.WesternJourney!, Graph), "Actual 019 predecessor.");
        var prior = shared.AugustinePromise = AugustinePromiseState.Create("AUGUSTINE:42:2");
        foreach (var action in new[] { AugustinePromiseAction.AcceptInvitation, AugustinePromiseAction.PromisePublic,
            AugustinePromiseAction.RepairPublic, AugustinePromiseAction.Leave })
            Check(AugustinePromiseFlow.TryPrepare(prior, action)
                && AugustinePromiseFlow.TryCommitPending(prior, shared.WesternJourney!, Graph), "Actual 020 adopted predecessor.");
        return shared;
    }

    private static PhilosophyRunState Knowledge(bool closePyrrho)
    {
        var shared = Fresh(SextusOpposedAppearancesState.RequiredNodeId, "SOCRATES_QUESTION_CUP");
        Check(Graph.TryReserveQuestion(shared.WesternJourney!, 1), "Actual knowledge Question1.");
        var sextus = shared.SextusOpposedAppearances = SextusOpposedAppearancesState.Create("SEXTUS:42:1");
        foreach (var action in new[] { SextusAppearancesAction.AcceptInvitation, SextusAppearancesAction.SuspendAssent, SextusAppearancesAction.Leave })
            Check(SextusOpposedAppearancesFlow.TryPrepare(sextus, action)
                && SextusOpposedAppearancesFlow.TryCommitPending(sextus), "Actual Sextus predecessor.");
        Check(Graph.TrySkipEmptyFixedSlot(shared.WesternJourney!), "Actual declined Fixed2 opportunity.");
        var descartes = shared.DescartesMethodicReconstruction = DescartesMethodicReconstructionState.Create("DESCARTES:42:2");
        foreach (var action in new[] { DescartesReconstructionAction.DeclineInvitation, DescartesReconstructionAction.Leave })
            Check(DescartesMethodicReconstructionFlow.TryPrepare(descartes, action)
                && DescartesMethodicReconstructionFlow.TryCommitPending(descartes), "Actual declined Descartes history.");
        if (!closePyrrho) return shared;
        Check(Graph.TryReserveQuestion(shared.WesternJourney!, 2), "Actual knowledge Question2.");
        var pyrrho = shared.PyrrhoReportedCrossing = PyrrhoReportedCrossingState.Create("PYRRHO:42:2", sextus, descartes);
        foreach (var action in new[] { PyrrhoReportedCrossingAction.CiteConflict,
            PyrrhoReportedCrossingAction.InspectAndCross, PyrrhoReportedCrossingAction.Leave })
            Check(PyrrhoReportedCrossingFlow.TryPrepare(pyrrho, action)
                && PyrrhoReportedCrossingFlow.TryCommitPending(pyrrho, shared.WesternJourney!, sextus, descartes), "Actual closed Pyrrho history.");
        return shared;
    }
}
