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
                if (!graph.TrySkipEmptyFixedSlot(journey) || !Reserve()) return false;
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

    internal static bool HasOpenQuestion(PhilosophyRunState shared)
    {
        static bool Open(bool closed, bool usable) => !closed && usable;
        return false
            || Open(shared.ProtagorasSharedDeliberation?.Closed ?? true, shared.ProtagorasSharedDeliberation?.IsUsable() ?? false)
            || Open(shared.PlatoWholeArrangement?.Closed ?? true, shared.PlatoWholeArrangement?.IsUsable() ?? false)
            || Open(shared.EpicurusSharedMargin?.Closed ?? true, shared.EpicurusSharedMargin?.IsUsable() ?? false)
            || Open(shared.PlotinusCommonGround?.Closed ?? true, shared.PlotinusCommonGround?.IsUsable() ?? false)
            || Open(shared.ZenoCivicChallenge?.Closed ?? true, shared.ZenoCivicChallenge?.IsUsable() ?? false)
            || Open(shared.AugustinePromise?.Closed ?? true, shared.AugustinePromise?.IsUsable() ?? false)
            || Open(shared.AnselmUnfinishedProof?.Closed ?? true, shared.AnselmUnfinishedProof?.IsUsable() ?? false)
            || Open(shared.NewtonTwoSites?.Closed ?? true, shared.NewtonTwoSites?.IsUsable() ?? false)
            || Open(shared.SpinozaCauseNetwork?.Closed ?? true, shared.SpinozaCauseNetwork?.IsUsable() ?? false)
            || Open(shared.SextusOpposedAppearances?.Closed ?? true, shared.SextusOpposedAppearances?.IsUsable() ?? false)
            || Open(shared.DescartesMethodicReconstruction?.Closed ?? true, shared.DescartesMethodicReconstruction?.IsUsable() ?? false)
            || Open(shared.HanFeiPublicStandard?.Closed ?? true, shared.HanFeiPublicStandard?.IsUsable() ?? false)
            || Open(shared.ParmenidesInquiry?.Closed ?? true, shared.ParmenidesInquiry?.IsUsable() ?? false)
            || Open(shared.AnaximanderOriginInquiry?.Closed ?? true, shared.AnaximanderOriginInquiry?.IsUsable() ?? false)
            || Open(shared.AnaximenesBreathInvitation?.Closed ?? true, shared.AnaximenesBreathInvitation?.IsUsable() ?? false)
            || Open(shared.AristotleCivicInquiry?.Closed ?? true, shared.AristotleCivicInquiry?.IsUsable() ?? false)
            || Open(shared.DemocritusArrangement?.Closed ?? true, shared.DemocritusArrangement?.IsUsable() ?? false)
            || Open(shared.EmpedoclesRootsInquiry?.Closed ?? true, shared.EmpedoclesRootsInquiry?.IsUsable() ?? false)
            || Open(shared.AquinasBridgeNotice?.Closed ?? true, shared.AquinasBridgeNotice?.IsUsable() ?? false)
            || Open(shared.OckhamNamesInquiry?.Closed ?? true, shared.OckhamNamesInquiry?.IsUsable() ?? false)
            || Open(shared.MachiavelliFloodPreparation?.Closed ?? true, shared.MachiavelliFloodPreparation?.IsUsable() ?? false)
            || Open(shared.LutherSignedCare?.Closed ?? true, shared.LutherSignedCare?.IsUsable() ?? false)
            || Open(shared.BaconSampleLedger?.Closed ?? true, shared.BaconSampleLedger?.IsUsable() ?? false)
            || Open(shared.CopernicusShiftedCenter?.Closed ?? true, shared.CopernicusShiftedCenter?.IsUsable() ?? false)
            || Open(shared.GalileoMeasuredPractice?.Closed ?? true, shared.GalileoMeasuredPractice?.IsUsable() ?? false)
            || Open(shared.AnaxagorasMixtureInquiry?.Closed ?? true, shared.AnaxagorasMixtureInquiry?.IsUsable() ?? false)
            || Open(shared.GorgiasSpeechInquiry?.Closed ?? true, shared.GorgiasSpeechInquiry?.IsUsable() ?? false)
            || Open(shared.SenecaDailyReview?.Closed ?? true, shared.SenecaDailyReview?.IsUsable() ?? false)
            || Open(shared.EpictetusGuidePractice?.Closed ?? true, shared.EpictetusGuidePractice?.IsUsable() ?? false)
            || Open(shared.MarcusAureliusCampPractice?.Closed ?? true, shared.MarcusAureliusCampPractice?.IsUsable() ?? false)
            || Open(shared.PyrrhoReportedCrossing?.Closed ?? true, shared.PyrrhoReportedCrossing?.IsUsable() ?? false)
            || Open(shared.LeibnizMonadComparison?.Closed ?? true, shared.LeibnizMonadComparison?.IsUsable() ?? false)
            || Open(shared.HegelSubstanceSubject?.Closed ?? true, shared.HegelSubstanceSubject?.IsUsable() ?? false)
            || Open(shared.HobbesAuthorizedSovereignty?.Closed ?? true, shared.HobbesAuthorizedSovereignty?.IsUsable() ?? false)
            || Open(shared.LockeCargoIdentity?.Closed ?? true, shared.LockeCargoIdentity?.IsUsable() ?? false)
            || Open(shared.HumeInduction?.Closed ?? true, shared.HumeInduction?.IsUsable() ?? false)
            || Open(shared.WollstonecraftAdmission?.Closed ?? true, shared.WollstonecraftAdmission?.IsUsable() ?? false)
            || Open(shared.KantApperception?.Closed ?? true, shared.KantApperception?.IsUsable() ?? false)
            || Open(shared.SchopenhauerWillDenial?.Closed ?? true, shared.SchopenhauerWillDenial?.IsUsable() ?? false)
            || Open(shared.JamesForcedBridge?.Closed ?? true, shared.JamesForcedBridge?.IsUsable() ?? false)
            || Open(shared.CamusRevoltLimit?.Closed ?? true, shared.CamusRevoltLimit?.IsUsable() ?? false)
            || Open(shared.CarnapFrameworkChoice?.Closed ?? true, shared.CarnapFrameworkChoice?.IsUsable() ?? false)
            || Open(shared.AustinSpeechAct?.Closed ?? true, shared.AustinSpeechAct?.IsUsable() ?? false)
            || Open(shared.PopperFalsification?.Closed ?? true, shared.PopperFalsification?.IsUsable() ?? false)
            || Open(shared.RousseauConsent?.Closed ?? true, shared.RousseauConsent?.IsUsable() ?? false)
            || Open(shared.KantUniversalization?.Closed ?? true, shared.KantUniversalization?.IsUsable() ?? false)
            || Open(shared.MillHarm?.Closed ?? true, shared.MillHarm?.IsUsable() ?? false)
            || Open(shared.MontesquieuBalance?.Closed ?? true, shared.MontesquieuBalance?.IsUsable() ?? false)
            || Open(shared.SmithSympathy?.Closed ?? true, shared.SmithSympathy?.IsUsable() ?? false)
            || Open(shared.SchellingNature?.Closed ?? true, shared.SchellingNature?.IsUsable() ?? false)
            || Open(shared.KierkegaardFaith?.Closed ?? true, shared.KierkegaardFaith?.IsUsable() ?? false)
            || Open(shared.MarxAlienation?.Closed ?? true, shared.MarxAlienation?.IsUsable() ?? false)
            || Open(shared.NietzscheRevaluation?.Closed ?? true, shared.NietzscheRevaluation?.IsUsable() ?? false)
            || Open(shared.BenthamUtility?.Closed ?? true, shared.BenthamUtility?.IsUsable() ?? false)
            || Open(shared.ComtePositivism?.Closed ?? true, shared.ComtePositivism?.IsUsable() ?? false)
            || Open(shared.PeirceInquiry?.Closed ?? true, shared.PeirceInquiry?.IsUsable() ?? false)
            || Open(shared.DeweyPublicExperiment?.Closed ?? true, shared.DeweyPublicExperiment?.IsUsable() ?? false)
            || Open(shared.RouteTurnQuestion?.Closed ?? true, shared.RouteTurnQuestion?.IsUsable() ?? false)
            || Open(shared.HusserlEpoche?.Closed ?? true, shared.HusserlEpoche?.IsUsable() ?? false)
            || Open(shared.HeideggerDasein?.Closed ?? true, shared.HeideggerDasein?.IsUsable() ?? false)
            || Open(shared.SartreExistence?.Closed ?? true, shared.SartreExistence?.IsUsable() ?? false)
            || Open(shared.MerleauPontyBody?.Closed ?? true, shared.MerleauPontyBody?.IsUsable() ?? false)
            || Open(shared.BeauvoirOther?.Closed ?? true, shared.BeauvoirOther?.IsUsable() ?? false)
            || Open(shared.FichteSelfPositing?.Closed ?? true, shared.FichteSelfPositing?.IsUsable() ?? false)
            || Open(shared.FoucaultPower?.Closed ?? true, shared.FoucaultPower?.IsUsable() ?? false)
            || Open(shared.DeleuzeDifference?.Closed ?? true, shared.DeleuzeDifference?.IsUsable() ?? false)
            || Open(shared.GadamerFusion?.Closed ?? true, shared.GadamerFusion?.IsUsable() ?? false)
            || Open(shared.RussellAnalysis?.Closed ?? true, shared.RussellAnalysis?.IsUsable() ?? false)
            || Open(shared.WittgensteinGame?.Closed ?? true, shared.WittgensteinGame?.IsUsable() ?? false)
            || Open(shared.SaussureSign?.Closed ?? true, shared.SaussureSign?.IsUsable() ?? false)
            || Open(shared.LeviStraussMyth?.Closed ?? true, shared.LeviStraussMyth?.IsUsable() ?? false)
            || Open(shared.DerridaDeconstruction?.Closed ?? true, shared.DerridaDeconstruction?.IsUsable() ?? false)
            || Open(shared.HorkheimerReason?.Closed ?? true, shared.HorkheimerReason?.IsUsable() ?? false)
            || Open(shared.AdornoNegative?.Closed ?? true, shared.AdornoNegative?.IsUsable() ?? false)
            || Open(shared.HabermasCommunication?.Closed ?? true, shared.HabermasCommunication?.IsUsable() ?? false)
            || Open(shared.FreudUnconscious?.Closed ?? true, shared.FreudUnconscious?.IsUsable() ?? false)
            || Open(shared.LacanSignifier?.Closed ?? true, shared.LacanSignifier?.IsUsable() ?? false)
            || Open(shared.FregeSense?.Closed ?? true, shared.FregeSense?.IsUsable() ?? false)
            || Open(shared.KuhnParadigm?.Closed ?? true, shared.KuhnParadigm?.IsUsable() ?? false);
    }

    private static WesternJourneyState CopyJourney(WesternJourneyState source) => new()
    {
        CurrentNodeId = source.CurrentNodeId, LastFixedAct = source.LastFixedAct, RetainedEnding = source.RetainedEnding,
        SeenThinkerIds = new(source.SeenThinkerIds, StringComparer.Ordinal),
        CompletedEdgeIds = new(source.CompletedEdgeIds, StringComparer.Ordinal),
        ResolvedQuestionActs = new(source.ResolvedQuestionActs), DeclinedFixedActs = new(source.DeclinedFixedActs),
        CandidateWindows = source.CandidateWindows.ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.Ordinal),
    };
}
