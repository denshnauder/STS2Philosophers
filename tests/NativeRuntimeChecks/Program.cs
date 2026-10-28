int portraitsMode = Array.IndexOf(args, "--portraits-only");
if (portraitsMode >= 0)
{
    if (portraitsMode != 0 || args.Length != 2)
        throw new ArgumentException("Use --portraits-only <content.pck> to verify the actual exported event portraits.");
    EventPortraitNativeChecks.Run(args[1]);
    return;
}
if (args.Contains("--galileo-only"))
{
    await GalileoMeasuredPracticeNativeChecks.Run();
    return;
}
if (args.Contains("--newton-only"))
{
    await NewtonTwoSitesNativeChecks.Run();
    return;
}
if (args.Contains("--spinoza-only"))
{
    await DescartesThinkingLensRuntimeChecks.Run();
    await SpinozaCauseNetworkNativeChecks.Run();
    return;
}
if (args.Contains("--enc197-only"))
{
    await PhilosophersGazeSaveRecoveryNativeChecks.Run();
    return;
}
if (args.Contains("--enc198-only"))
{
    await SlyCostReceiptNativeChecks.Run();
    return;
}
if (args.Contains("--enc199-special-only"))
{
    await StoicQuestionEntryNativeChecks.Run();
    return;
}
if (args.Contains("--enc199-legacy-only"))
{
    await LegacyStoicQuestionExitNativeChecks.Run();
    return;
}
if (args.Contains("--enc199-only"))
{
    await NativeQuestionNodeChecks.Run();
    await AllRouteQuestionNodeChecks.Run();
    await CopernicusQuestionEntryNativeChecks.Run();
    await EasternQuestionEntryNativeChecks.Run();
    await StoicQuestionEntryNativeChecks.Run();
    await LegacyStoicQuestionExitNativeChecks.Run();
    return;
}
if (args.Contains("--copernicus-only"))
{
    await CopernicusShiftedCenterNativeChecks.Run();
    return;
}
if (args.Contains("--enc196-only"))
{
    await NativeQuestionNodeChecks.Run();
    await ZenoCivicSealUpgradeNativeChecks.Run();
    return;
}
if (args.Contains("--zeno-civic-only"))
{
    await ZenoCivicSealUpgradeNativeChecks.Run();
    return;
}
if (!args.Contains("--convention-only")) await DescartesThinkingLensRuntimeChecks.Run();
await RousseauUncarvedStoneRuntimeChecks.Run();
await DemocritusArrangementRuntimeChecks.Run();
await RouteEventMapBoundaryNativeChecks.Run();
await PhilosophyRunStateRitsuRuntimeChecks.Run();
await PlotinusCombatPracticeNativeChecks.Run();
await AugustinePromiseNativeChecks.Run();
await AquinasBridgeNoticeNativeChecks.Run();
await AnselmUnfinishedProofNativeChecks.Run();
await OckhamNamesInquiryNativeChecks.Run();
await MachiavelliFloodPreparationNativeChecks.Run();
await LutherSignedCareNativeChecks.Run();
await BaconSampleLedgerNativeChecks.Run();
await CopernicusShiftedCenterNativeChecks.Run();
await GalileoMeasuredPracticeNativeChecks.Run();
await NewtonTwoSitesNativeChecks.Run();
await SpinozaCauseNetworkNativeChecks.Run();
await NativeQuestionNodeChecks.Run();
await ZenoCivicSealUpgradeNativeChecks.Run();
await PhilosophersGazeSaveRecoveryNativeChecks.Run();
await SlyCostReceiptNativeChecks.Run();
await AllRouteQuestionNodeChecks.Run();
await CopernicusQuestionEntryNativeChecks.Run();
await EasternQuestionEntryNativeChecks.Run();

await StoicQuestionEntryNativeChecks.Run();
await LegacyStoicQuestionExitNativeChecks.Run();
