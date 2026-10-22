namespace STS2Philosophers;

internal static class LaterActRelicEligibilityChecks
{
    public static void Run()
    {
        EveryCandidateDeclaresAnActAndAcceptedRelic();
        CurrentRelicsSelectOnlyRegisteredCandidates();
        ReplacedAwayRelicsStopUnlockingTheirSuccessors();
        EmptyUnknownAndWrongActInputsSkipSilently();
        RecalculationIsStableAcrossSaveLoadBoundaries();
        ProductionEntryPointsUseTheSharedCatalog();
        Console.WriteLine("Later-act relic eligibility checks passed.");
    }

    private static void EveryCandidateDeclaresAnActAndAcceptedRelic()
    {
        LaterActRelicEligibilityRegistration[] entries =
            LaterActRelicEligibilityCatalog.Registrations.ToArray();
        Assert(entries.Length == 17
               && entries.All(entry => !string.IsNullOrWhiteSpace(entry.CandidateId)
                   && entry.CurrentActIndex is LaterActRelicEligibilityCatalog.ActTwoIndex
                       or LaterActRelicEligibilityCatalog.ActThreeIndex
                   && entry.AcceptedRelicIds.Count > 0)
               && entries.Select(entry => (entry.CandidateId, entry.CurrentActIndex)).Distinct().Count()
                   == entries.Length,
            "Every later-act character must have one explicit act and at least one accepted current relic.");
    }

    private static void CurrentRelicsSelectOnlyRegisteredCandidates()
    {
        string[] actTwo = LaterActRelicEligibilityCatalog.GetEligibleCandidateIds(
            LaterActRelicEligibilityCatalog.ActTwoIndex,
            [RouteRelicIds.KongziQingYuPei, RouteRelicIds.SocratesQuestionCup, "UNRELATED_RELIC"])
            .ToArray();
        Assert(actTwo.SequenceEqual([LaterActCandidateIds.Mengzi, LaterActCandidateIds.Descartes])
               && LaterActRelicEligibilityCatalog.IsEligible(
                   LaterActCandidateIds.Epicurus,
                   LaterActRelicEligibilityCatalog.ActThreeIndex,
                   [RouteRelicIds.SocratesBalanceWeight])
               && !LaterActRelicEligibilityCatalog.IsEligible(
                   LaterActCandidateIds.Epicurus,
                   LaterActRelicEligibilityCatalog.ActTwoIndex,
                   [RouteRelicIds.SocratesBalanceWeight])
               && LaterActRelicEligibilityCatalog.IsEligible(
                   LaterActCandidateIds.Plotinus,
                   LaterActRelicEligibilityCatalog.ActThreeIndex,
                   [RouteRelicIds.SocratesBalanceWeight]),
            "Only candidates registered for the current act and held relics may enter the pool.");
    }

    private static void ReplacedAwayRelicsStopUnlockingTheirSuccessors()
    {
        Assert(LaterActRelicEligibilityCatalog.IsEligible(
                   LaterActCandidateIds.Mengzi,
                   LaterActRelicEligibilityCatalog.ActTwoIndex,
                   [RouteRelicIds.KongziQingYuPei])
               && !LaterActRelicEligibilityCatalog.IsEligible(
                   LaterActCandidateIds.Mengzi,
                   LaterActRelicEligibilityCatalog.ActTwoIndex,
                   ["MENGZI_XIONG_ZHANG"]),
            "After replacement, the removed root relic must no longer unlock its old successor.");
    }

    private static void EmptyUnknownAndWrongActInputsSkipSilently()
    {
        Assert(LaterActRelicEligibilityCatalog.GetEligibleCandidateIds(
                   LaterActRelicEligibilityCatalog.ActTwoIndex,
                   []).Count == 0
               && LaterActRelicEligibilityCatalog.GetEligibleCandidateIds(
                   LaterActRelicEligibilityCatalog.ActThreeIndex,
                   ["UNKNOWN_RELIC"]).Count == 0
               && !LaterActRelicEligibilityCatalog.IsEligible(
                   "UNREGISTERED_CHARACTER",
                   LaterActRelicEligibilityCatalog.ActTwoIndex,
                   [RouteRelicIds.KongziMuduo]),
            "No current relation must produce an empty candidate set without a placeholder.");
    }

    private static void RecalculationIsStableAcrossSaveLoadBoundaries()
    {
        string[] restoredCurrentRelics =
            [RouteRelicIds.MoziMoSeZhuJian, RouteRelicIds.SocratesBalanceWeight];
        string[] before = LaterActRelicEligibilityCatalog.GetEligibleCandidateIds(
            LaterActRelicEligibilityCatalog.ActTwoIndex,
            restoredCurrentRelics).ToArray();
        string[] after = LaterActRelicEligibilityCatalog.GetEligibleCandidateIds(
            LaterActRelicEligibilityCatalog.ActTwoIndex,
            restoredCurrentRelics.ToArray()).ToArray();
        Assert(before.SequenceEqual([LaterActCandidateIds.Huishi, LaterActCandidateIds.Plato])
               && after.SequenceEqual(before),
            "Eligibility must be recomputed deterministically from the restored current relic inventory.");
    }

    private static void ProductionEntryPointsUseTheSharedCatalog()
    {
        string root = Directory.GetCurrentDirectory();
        string easternSource = File.ReadAllText(Path.Combine(
            root, "src", "Events", "PhilosophersGazeContinuationCandidateSource.cs"));
        Assert(easternSource.Contains("LaterActRelicEligibilityCatalog.GetEligibleCandidateIds", StringComparison.Ordinal),
            "The eastern act-two continuation must use the shared relic registry.");

        foreach (string patch in new[]
                 {
                     "PlatoWholeArrangementPatch.cs",
                     "DescartesMethodicReconstructionPatch.cs",
                     "EpicurusSharedMarginPatch.cs",
                     "PlotinusCommonGroundPatch.cs",
                     "AquinasBridgeNoticePatch.cs",
                     "BaconSampleLedgerPatch.cs",
                     "HanFeiPublicStandardPatch.cs",
                 })
        {
            string source = File.ReadAllText(Path.Combine(root, "src", "Patches", patch));
            Assert(source.Contains("LaterActRelicEligibilityRuntime.IsEligible", StringComparison.Ordinal),
                $"{patch} must use the shared current-relic eligibility gate.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
