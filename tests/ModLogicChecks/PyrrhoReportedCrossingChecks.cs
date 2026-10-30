using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class PyrrhoReportedCrossingChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        var eligible = new PyrrhoReportedCrossingTriggerContext(
            true, true, true, 1, true, SextusOpposedAppearancesState.RequiredNodeId, 2,
            true, false, true, true, true, true, false, false, false, true);
        Check(PyrrhoReportedCrossingTriggerPolicy.ShouldInsert(eligible),
            "Only the declined Descartes branch may spend the second-act question.");
        foreach (var blocked in new[]
        {
            eligible with { CurrentActIndex = 0 }, eligible with { AtSafeMapBoundary = false },
            eligible with { IsSingleplayer = false }, eligible with { SourceIsCompletedNonBossCombat = false },
            eligible with { CurrentNodeId = "OTHER" }, eligible with { LastFixedAct = 3 },
            eligible with { FirstQuestionResolved = false }, eligible with { SecondQuestionResolved = true },
            eligible with { SecondFixedDeclined = false }, eligible with { HasRequiredRelic = false },
            eligible with { SextusClosed = false }, eligible with { DescartesDeclinedAndClosed = false },
            eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!PyrrhoReportedCrossingTriggerPolicy.ShouldInsert(blocked),
            "A wrong branch, act, source or consumed question must not insert.");
        Check(PyrrhoReportedCrossingTriggerPolicy.ShouldInsert(eligible with
            { SecondQuestionResolved = true, HasSavedInstance = true, SavedInstanceUsable = true }),
            "An open saved question resumes after its own reservation.");

        Check(Fixture(withPyrrho: false).PyrrhoReportedCrossing is null,
            "Old saves do not invent the new question.");
        foreach (PyrrhoReportedCrossingAction first in new[]
        { PyrrhoReportedCrossingAction.CiteConflict, PyrrhoReportedCrossingAction.ClaimDirectRule })
        foreach (PyrrhoReportedCrossingAction second in new[]
        { PyrrhoReportedCrossingAction.InspectAndCross, PyrrhoReportedCrossingAction.AskGuideAndCross })
        {
            PhilosophyRunState shared = Fixture();
            foreach (PyrrhoReportedCrossingAction action in new[]
            { first, second, PyrrhoReportedCrossingAction.Leave })
            {
                PyrrhoReportedCrossingState state = shared.PyrrhoReportedCrossing!;
                Check(PyrrhoReportedCrossingFlow.TryPrepare(state, action)
                    && !PyrrhoReportedCrossingFlow.TryPrepare(state, action),
                    "Each action has one write-ahead slot.");
                shared = Reload(shared);
                Check(Commit(shared) && !Commit(shared), "A saved action commits once.");
                shared = Reload(shared);
            }
            Check(shared.PyrrhoReportedCrossing!.Closed
                && shared.PyrrhoReportedCrossing.ConfirmedActions.SequenceEqual(
                    new[] { first, second, PyrrhoReportedCrossingAction.Leave })
                && shared.WesternJourney!.CurrentNodeId == SextusOpposedAppearancesState.RequiredNodeId
                && shared.WesternJourney.ResolvedQuestionActs.SetEquals([1, 2])
                && shared.DescartesMethodicReconstruction!.Stage == DescartesReconstructionStage.ResultDeclined,
                "Four testimony/action paths preserve the route and both consumed slots.");
        }
        PhilosophyRunState declined = Fixture();
        Check(PyrrhoReportedCrossingFlow.TryPrepare(declined.PyrrhoReportedCrossing!,
            PyrrhoReportedCrossingAction.Decline), "Declining is a legal path.");
        declined = Reload(declined);
        Check(Commit(declined), "Declining commits.");
        Check(PyrrhoReportedCrossingFlow.TryPrepare(declined.PyrrhoReportedCrossing!,
            PyrrhoReportedCrossingAction.Leave), "A declined question can close.");
        declined = Reload(declined);
        Check(Commit(declined) && Reload(declined).PyrrhoReportedCrossing!.Closed,
            "Declining never fabricates crossing actions.");

        PhilosophyRunState corrupted = Fixture();
        corrupted.PyrrhoReportedCrossing!.DescartesInstanceId = "OTHER";
        ExpectInvalid(corrupted);
        corrupted = Fixture();
        corrupted.DescartesMethodicReconstruction!.Stage = DescartesReconstructionStage.ResultStopped;
        ExpectInvalid(corrupted);
        corrupted = Fixture();
        corrupted.WesternJourney!.ResolvedQuestionActs.Remove(2);
        ExpectInvalid(corrupted);
        foreach (string field in new[]
        { "Version", "InstanceId", "SextusInstanceId", "DescartesInstanceId", "QuestionSlotReserved",
            "Stage", "PendingAction", "ConfirmedActions", "Closed" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(Fixture().PyrrhoReportedCrossing)!;
            node.AsObject().Remove(field);
            Check(!JsonSerializer.Deserialize<PyrrhoReportedCrossingState>(node)!.IsUsable(),
                "Missing save fields cannot become an invitation.");
        }
        ValidateLocalization();
        Console.WriteLine("Pyrrho reported crossing checks passed: legal unused question branch, five saved paths, strict testimony predecessors and bilingual pages.");
    }

    private static PhilosophyRunState Fixture(bool withPyrrho = true)
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start(SextusOpposedAppearancesState.RequiredNodeId);
        Check(graph.TryReserveQuestion(journey, 1) && graph.TryDecline(journey, "Fixed", 2),
            "The earlier Sextus question and declined fixed slot are real graph actions.");
        SextusOpposedAppearancesState sextus = SextusOpposedAppearancesState.Create("SEXTUS_OPPOSED_APPEARANCES:42:1");
        foreach (SextusAppearancesAction action in new[]
        { SextusAppearancesAction.AcceptInvitation, SextusAppearancesAction.SuspendAssent, SextusAppearancesAction.Leave })
        {
            Check(SextusOpposedAppearancesFlow.TryPrepare(sextus, action)
                && SextusOpposedAppearancesFlow.TryCommitPending(sextus), "Fixture Sextus action commits.");
        }
        DescartesMethodicReconstructionState descartes =
            DescartesMethodicReconstructionState.Create("DESCARTES_METHODIC_RECONSTRUCTION:42:2");
        foreach (DescartesReconstructionAction action in new[]
        { DescartesReconstructionAction.DeclineInvitation, DescartesReconstructionAction.Leave })
        {
            Check(DescartesMethodicReconstructionFlow.TryPrepare(descartes, action)
                && DescartesMethodicReconstructionFlow.TryCommitPending(descartes),
                "Fixture Descartes decline commits.");
        }
        PhilosophyRunState shared = new()
        {
            WesternJourney = journey,
            SextusOpposedAppearances = sextus,
            DescartesMethodicReconstruction = descartes,
        };
        if (withPyrrho)
        {
            Check(graph.TryReserveQuestion(journey, 2), "Pyrrho reserves the otherwise unused act-two slot.");
            shared.PyrrhoReportedCrossing = PyrrhoReportedCrossingState.Create(
                "PYRRHO_REPORTED_CROSSING:42:2", sextus, descartes);
        }
        return Reload(shared);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static bool Commit(PhilosophyRunState shared) =>
        PyrrhoReportedCrossingFlow.TryCommitPending(shared.PyrrhoReportedCrossing!,
            shared.WesternJourney!, shared.SextusOpposedAppearances!, shared.DescartesMethodicReconstruction!);

    private static void ExpectInvalid(PhilosophyRunState shared)
    {
        try { _ = PhilosophyRunStateCodec.Encode(shared); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Conflicting Pyrrho save must be rejected.");
    }

    private static void ValidateLocalization()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument localization = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            Check(localization.RootElement.TryGetProperty("PYRRHO_REPORTED_CROSSING.title", out _),
                "The native title is bilingual.");
            foreach (PyrrhoReportedCrossingStage stage in Enum.GetValues<PyrrhoReportedCrossingStage>())
            {
                string page = $"PYRRHO_REPORTED_CROSSING.pages.{PyrrhoReportedCrossingFlow.StageKey(stage)}";
                Check(localization.RootElement.TryGetProperty(page + ".description", out _),
                    "Every page is bilingual.");
                foreach (PyrrhoReportedCrossingAction action in PyrrhoReportedCrossingFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(localization.RootElement.TryGetProperty(
                        $"{page}.options.{PyrrhoReportedCrossingFlow.ActionKey(action)}.{suffix}", out _),
                        "Every legal option is bilingual.");
            }
        }
    }
}
