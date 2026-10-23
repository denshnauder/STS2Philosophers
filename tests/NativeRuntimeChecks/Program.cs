int portraitsMode = Array.IndexOf(args, "--portraits-only");
if (portraitsMode >= 0)
{
    if (portraitsMode != 0 || args.Length != 2)
        throw new ArgumentException("Use --portraits-only <content.pck> to verify the actual exported event portraits.");
    EventPortraitNativeChecks.Run(args[1]);
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
await NativeQuestionNodeChecks.Run();
await ZenoCivicSealUpgradeNativeChecks.Run();
