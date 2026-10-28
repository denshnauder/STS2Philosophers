namespace STS2Philosophers;

internal enum WesternQuestionKind
{
    None, Protagoras, Parmenides, Sextus, Gorgias, Zeno,
    Anaximander, Empedocles, Anaxagoras, Pyrrho, Ockham, Luther, Anselm,
}

// Selection is deterministic. Native Unknown odds and the native event queue remain
// responsible for deciding whether this map point contains an event at all.
internal static class WesternQuestionNodePolicy
{
    public static WesternQuestionKind Select(PhilosophyRunState shared, int actIndex,
        string carrierId, bool hasNaturalFork)
    {
        if (shared.WesternJourney is not { RetainedEnding: false } journey
            || shared.CurrentDoctrine is not { } doctrine || doctrine.DoctrineId != carrierId
            || !journey.SeenThinkerIds.Contains(doctrine.ThinkerId)
            || journey.CandidateWindows.Values.Any(window => window is null) || HasOpenQuestion(shared))
            return WesternQuestionKind.None;
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        bool Eligible(string edge) => graph.GetEligibleEdges(journey, "Question")
            .Any(candidate => candidate.EdgeId == edge);
        if (actIndex == 0 && journey.LastFixedAct == 1 && !journey.ResolvedQuestionActs.Contains(1))
        {
            return (journey.CurrentNodeId, carrierId) switch
            {
                (ProtagorasSharedDeliberationState.SourceNodeId, "SOCRATES_BALANCE_WEIGHT")
                    when shared.ProtagorasSharedDeliberation is null
                        && Eligible(ProtagorasSharedDeliberationState.CompletionEdgeId) => WesternQuestionKind.Protagoras,
                (ParmenidesInquiryState.RequiredNodeId, "HERACLITUS_LYRE")
                    when shared.ParmenidesInquiry is null && Eligible(ParmenidesInquiryState.EdgeId) => WesternQuestionKind.Parmenides,
                (SextusOpposedAppearancesState.RequiredNodeId, "SOCRATES_QUESTION_CUP")
                    when shared.SextusOpposedAppearances is null && hasNaturalFork => WesternQuestionKind.Sextus,
                (GorgiasSpeechInquiryState.RequiredNodeId, "ARISTOTLE_CATEGORY_TABLET")
                    when shared.GorgiasSpeechInquiry is null => WesternQuestionKind.Gorgias,
                (ZenoCivicChallengeState.SourceNodeId, "PLATO_CIVIC_SEAL")
                    when shared.ZenoCivicChallenge is null && Eligible(ZenoCivicChallengeState.EdgeId) => WesternQuestionKind.Zeno,
                _ => WesternQuestionKind.None,
            };
        }
        if (actIndex == 1 && !journey.ResolvedQuestionActs.Contains(2))
        {
            if (carrierId == "HERACLITUS_LYRE")
            {
                if (shared.AnaximanderOriginInquiry is null && journey.LastFixedAct == 1
                    && journey.CurrentNodeId == AnaximanderOriginInquiryState.RequiredNodeId
                    && journey.ResolvedQuestionActs.Contains(1) && !journey.DeclinedFixedActs.Contains(2)
                    && !journey.CompletedEdgeIds.Contains(ParmenidesInquiryState.EdgeId)
                    && shared.ParmenidesInquiry is { Closed: true } prior && prior.IsUsable()
                    && !prior.ConfirmedActions.Contains(ParmenidesInquiryAction.AdoptBoundary)
                    && graph.GetEligibleEdges(journey, "Fixed").Count == 0)
                    return WesternQuestionKind.Anaximander;
                if (journey.LastFixedAct == 2 && shared.DemocritusArrangement is { Closed: true } democritus
                    && DemocritusArrangementFlow.IsJourneyConsistent(democritus, journey, graph))
                {
                    if (shared.EmpedoclesRootsInquiry is null
                        && journey.CurrentNodeId == EmpedoclesRootsInquiryState.RequiredNodeId
                        && democritus.ConfirmedActions.Contains(DemocritusArrangementAction.AdoptAtoms)
                        && Eligible(EmpedoclesRootsInquiryState.EdgeId)) return WesternQuestionKind.Empedocles;
                    if (shared.AnaxagorasMixtureInquiry is null
                        && journey.CurrentNodeId == AnaxagorasMixtureInquiryState.RequiredNodeId
                        && journey.CompletedEdgeIds.Contains(AnaxagorasMixtureInquiryState.PrerequisiteEdgeId)
                        && journey.DeclinedFixedActs.Contains(2)
                        && !democritus.ConfirmedActions.Contains(DemocritusArrangementAction.AdoptAtoms))
                        return WesternQuestionKind.Anaxagoras;
                }
            }
            if (carrierId == "SOCRATES_QUESTION_CUP" && shared.PyrrhoReportedCrossing is null
                && journey.CurrentNodeId == SextusOpposedAppearancesState.RequiredNodeId
                && journey.LastFixedAct == 2 && journey.ResolvedQuestionActs.Contains(1)
                && journey.DeclinedFixedActs.Contains(2) && HasKnowledgePredecessors(shared))
                return WesternQuestionKind.Pyrrho;
            if (carrierId == "ARISTOTLE_CATEGORY_TABLET" && shared.OckhamNamesInquiry is null
                && shared.GorgiasSpeechInquiry is { Closed: true } gorgias
                && GorgiasSpeechInquiryFlow.IsJourneyConsistent(gorgias, journey)
                && !journey.DeclinedFixedActs.Contains(2)
                && !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_029")
                && !journey.CompletedEdgeIds.Contains("WESTERN_EDGE_030")) return WesternQuestionKind.Ockham;
            if (carrierId == "PLATO_CIVIC_SEAL" && shared.LutherSignedCare is null
                && journey.CurrentNodeId == LutherSignedCareState.SourceNodeId && journey.LastFixedAct == 2
                && shared.AugustinePromise is { Closed: true, EdgeCommitted: true } augustine
                && AugustinePromiseFlow.IsRouteConsistent(augustine, journey)
                && Eligible(LutherSignedCareState.EdgeId)) return WesternQuestionKind.Luther;
        }
        if (actIndex == 2 && carrierId == "SOCRATES_QUESTION_CUP"
            && shared.AnselmUnfinishedProof is null && journey.LastFixedAct == 2
            && journey.CurrentNodeId == AnselmUnfinishedProofState.RequiredNodeId
            && !journey.DeclinedFixedActs.Contains(3) && !journey.ResolvedQuestionActs.Contains(3)
            && shared.PyrrhoReportedCrossing is { Closed: true } pyrrho && HasKnowledgePredecessors(shared)
            && PyrrhoReportedCrossingFlow.IsRouteConsistent(pyrrho, journey,
                shared.SextusOpposedAppearances!, shared.DescartesMethodicReconstruction!))
            return WesternQuestionKind.Anselm;
        return WesternQuestionKind.None;
    }

    public static bool TryStart(PhilosophyRunState shared, int actIndex, string carrierId,
        bool hasNaturalFork, string seed, WesternQuestionKind expected)
    {
        if (expected == WesternQuestionKind.None || string.IsNullOrWhiteSpace(seed)
            || Select(shared, actIndex, carrierId, hasNaturalFork) != expected) return false;
        // Prepare all route changes off to the side. There is no await or save between
        // publishing the matching person record and this journey on the main thread.
        WesternJourneyState journey = CopyJourney(shared.WesternJourney!);
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        string Id(string name) => $"{name}:{seed}:{actIndex + 1}";
        bool Reserve() => graph.TryReserveQuestion(journey, actIndex + 1);
        switch (expected)
        {
            case WesternQuestionKind.Protagoras:
                shared.ProtagorasSharedDeliberation = ProtagorasSharedDeliberationState.Create(Id("PROTAGORAS_SHARED_DELIBERATION")); break;
            case WesternQuestionKind.Parmenides:
                if (!Reserve()) return false;
                shared.ParmenidesInquiry = ParmenidesInquiryState.Create(Id("PARMENIDES_INQUIRY")); break;
            case WesternQuestionKind.Sextus:
                if (!Reserve()) return false;
                shared.SextusOpposedAppearances = SextusOpposedAppearancesState.Create(Id("SEXTUS_OPPOSED_APPEARANCES")); break;
            case WesternQuestionKind.Gorgias:
                if (!Reserve()) return false;
                shared.GorgiasSpeechInquiry = GorgiasSpeechInquiryState.Create(Id("GORGIAS_SPEECH_INQUIRY")); break;
            case WesternQuestionKind.Zeno:
                if (!Reserve()) return false;
                shared.ZenoCivicChallenge = ZenoCivicChallengeState.Create(Id("ZENO_CIVIC_CHALLENGE")); break;
            case WesternQuestionKind.Anaximander:
                if (!graph.TryDecline(journey, "Fixed", 2) || !Reserve()) return false;
                shared.AnaximanderOriginInquiry = AnaximanderOriginInquiryState.Create(Id("ANAXIMANDER_ORIGIN_INQUIRY")); break;
            case WesternQuestionKind.Empedocles:
                if (!Reserve()) return false;
                shared.EmpedoclesRootsInquiry = EmpedoclesRootsInquiryState.Create(Id("EMPEDOCLES_ROOTS_INQUIRY")); break;
            case WesternQuestionKind.Anaxagoras:
                if (!Reserve()) return false;
                shared.AnaxagorasMixtureInquiry = AnaxagorasMixtureInquiryState.Create(Id("ANAXAGORAS_MIXTURE_INQUIRY")); break;
            case WesternQuestionKind.Pyrrho:
                if (!Reserve()) return false;
                shared.PyrrhoReportedCrossing = PyrrhoReportedCrossingState.Create(Id("PYRRHO_REPORTED_CROSSING"),
                    shared.SextusOpposedAppearances!, shared.DescartesMethodicReconstruction!); break;
            case WesternQuestionKind.Ockham:
                shared.OckhamNamesInquiry = OckhamNamesInquiryState.Create(Id("OCKHAM_NAMES_INQUIRY"),
                    shared.GorgiasSpeechInquiry!.InstanceId); break;
            case WesternQuestionKind.Luther:
                shared.LutherSignedCare = LutherSignedCareState.Create(Id("LUTHER_SIGNED_CARE"),
                    shared.AugustinePromise!.InstanceId); break;
            case WesternQuestionKind.Anselm:
                shared.AnselmUnfinishedProof = AnselmUnfinishedProofState.Create(Id("ANSELM_UNFINISHED_PROOF"),
                    shared.PyrrhoReportedCrossing!.InstanceId); break;
            default: return false;
        }
        shared.WesternJourney = journey;
        return true;
    }

    private static bool HasKnowledgePredecessors(PhilosophyRunState shared) =>
        shared.SextusOpposedAppearances is { Closed: true } sextus && sextus.IsUsable()
        && shared.DescartesMethodicReconstruction is { Closed: true, Stage: DescartesReconstructionStage.ResultDeclined } descartes
        && descartes.IsUsable();

    internal static bool HasOpenQuestion(PhilosophyRunState shared) =>
        shared.ProtagorasSharedDeliberation is { Closed: false } || shared.ParmenidesInquiry is { Closed: false }
        || shared.SextusOpposedAppearances is { Closed: false } || shared.GorgiasSpeechInquiry is { Closed: false }
        || shared.ZenoCivicChallenge is { Closed: false } || shared.AnaximanderOriginInquiry is { Closed: false }
        || shared.EmpedoclesRootsInquiry is { Closed: false } || shared.AnaxagorasMixtureInquiry is { Closed: false }
        || shared.PyrrhoReportedCrossing is { Closed: false } || shared.OckhamNamesInquiry is { Closed: false }
        || shared.LutherSignedCare is { Closed: false } || shared.AnselmUnfinishedProof is { Closed: false }
        || shared.MachiavelliFloodPreparation is { Closed: false }
        || shared.WollstonecraftAdmission is { Closed: false }
        || shared.MillHarm is { Closed: false }
        || shared.BeauvoirOther is { Closed: false }
        || shared.FichteSelfPositing is { Closed: false }
        || shared.LockeCargoIdentity is { Closed: false }
        || shared.HumeInduction is { Closed: false }
        || shared.KantApperception is { Closed: false }
        || shared.SchopenhauerWillDenial is { Closed: false }
        || shared.JamesForcedBridge is { Closed: false }
        || shared.CamusRevoltLimit is { Closed: false }
        || shared.CarnapFrameworkChoice is { Closed: false }
        || shared.AustinSpeechAct is { Closed: false }
        || shared.PopperFalsification is { Closed: false }
        || shared.RousseauConsent is { Closed: false }
        || shared.KantUniversalization is { Closed: false };

    private static WesternJourneyState CopyJourney(WesternJourneyState source) => new()
    {
        CurrentNodeId = source.CurrentNodeId, LastFixedAct = source.LastFixedAct, RetainedEnding = source.RetainedEnding,
        SeenThinkerIds = new(source.SeenThinkerIds, StringComparer.Ordinal),
        CompletedEdgeIds = new(source.CompletedEdgeIds, StringComparer.Ordinal),
        ResolvedQuestionActs = new(source.ResolvedQuestionActs), DeclinedFixedActs = new(source.DeclinedFixedActs),
        CandidateWindows = source.CandidateWindows.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.Ordinal),
    };
}
