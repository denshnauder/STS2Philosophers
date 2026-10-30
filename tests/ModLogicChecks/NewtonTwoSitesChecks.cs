using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class NewtonTwoSitesChecks
{
    public static void Run()
    {
        var fresh = new NewtonTwoSitesTriggerContext(
            RunInProgress: true, AtNativeQuestionEntry: true, IsSingleplayer: true, CurrentActIndex: 2,
            HasRequiredRelic: true, HasValidKnowledgePredecessors: true, HasClosedRebuiltDescartes: true,
            HasPyrrhoInstance: false, HasAnselmInstance: false, HasOpenRouteQuestion: false,
            CurrentNodeId: NewtonTwoSitesTriggerPolicy.SourceNodeId, LastFixedAct: 2,
            FixedActThreeResolved: false, RetainedEnding: false, HasEligibleFixedEdge: true,
            HasCompletedCombatAfterDescartes: true, HasSavedInstance: false, SavedInstanceUsable: false,
            SavedInstanceClosed: false, SavedInstanceConsistent: false, ModelAvailable: true);
        Check(NewtonTwoSitesTriggerPolicy.ShouldInsert(fresh), "legitimate unused Fixed3");
        foreach (var denied in new[]
        {
            fresh with { AtNativeQuestionEntry = false },
            fresh with { CurrentActIndex = 1 },
            fresh with { IsSingleplayer = false },
            fresh with { HasRequiredRelic = false },
            fresh with { HasValidKnowledgePredecessors = false },
            fresh with { HasClosedRebuiltDescartes = false },
            fresh with { HasPyrrhoInstance = true },
            fresh with { HasAnselmInstance = true },
            fresh with { HasOpenRouteQuestion = true },
            fresh with { CurrentNodeId = "DESCARTES__KNOWLEDGE_AND_DOUBT__CORE" },
            fresh with { LastFixedAct = 1 },
            fresh with { FixedActThreeResolved = true },
            fresh with { RetainedEnding = true },
            fresh with { HasEligibleFixedEdge = false },
            fresh with { HasCompletedCombatAfterDescartes = false },
        }) Check(!NewtonTwoSitesTriggerPolicy.ShouldInsert(denied), "reject unsafe fresh entry");

        // A saved result has already changed the node and spent the Fixed slot.
        // Its exact, consistent instance must recover without creating a new one.
        var saved = fresh with
        {
            HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceConsistent = true,
            CurrentNodeId = "ISAAC_NEWTON__KNOWLEDGE_AND_DOUBT__CORE", LastFixedAct = 3,
            FixedActThreeResolved = true, HasEligibleFixedEdge = false,
            HasCompletedCombatAfterDescartes = false,
        };
        Check(NewtonTwoSitesTriggerPolicy.ShouldInsert(saved), "recover exact committed instance");
        Check(!NewtonTwoSitesTriggerPolicy.ShouldInsert(saved with { SavedInstanceClosed = true }), "closed instance");
        Check(!NewtonTwoSitesTriggerPolicy.ShouldInsert(saved with { SavedInstanceUsable = false }), "invalid instance");
        Check(!NewtonTwoSitesTriggerPolicy.ShouldInsert(saved with { SavedInstanceConsistent = false }), "foreign instance");
        Check(!NewtonTwoSitesTriggerPolicy.ShouldInsert(saved with { AtNativeQuestionEntry = false }), "recovery still needs real node");
        Check(!NewtonTwoSitesTriggerPolicy.ShouldInsert(saved with { HasPyrrhoInstance = true }), "recovery cannot hide conflicting path");
        Console.WriteLine("Newton native question eligibility checks passed.");
        RunEventChecks();
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static (PhilosophyRunState, WesternRouteGraph) Fixture()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        WesternJourneyState journey = graph.Start(NewtonTwoSitesState.SourceNodeId);
        Check(graph.TryReserveQuestion(journey, 1)
            && graph.TryDecline(journey, "Fixed", 2)
            && !journey.CompletedEdgeIds.Contains(NewtonTwoSitesState.CompletionEdgeId),
            "The Newton source node is reached through real first- and second-act events.");

        SextusOpposedAppearancesState sextus = SextusOpposedAppearancesState.Create("SEXTUS:NEWTON:1");
        foreach (SextusAppearancesAction action in new[]
            { SextusAppearancesAction.AcceptInvitation, SextusAppearancesAction.SuspendAssent,
                SextusAppearancesAction.Leave })
            Check(SextusOpposedAppearancesFlow.TryPrepare(sextus, action)
                && SextusOpposedAppearancesFlow.TryCommitPending(sextus),
                "A real Sextus event choice commits.");

        DescartesMethodicReconstructionState descartes =
            DescartesMethodicReconstructionState.Create("DESCARTES:NEWTON:2");
        foreach (DescartesReconstructionAction action in new[]
            { DescartesReconstructionAction.AcceptInvitation,
                DescartesReconstructionAction.WithdrawSharedBasis,
                DescartesReconstructionAction.InspectCurrentLandmark,
                DescartesReconstructionAction.RebuildCoveredJudgment,
                DescartesReconstructionAction.Leave })
            Check(DescartesMethodicReconstructionFlow.TryPrepare(descartes, action)
                && DescartesMethodicReconstructionFlow.TryCommitPending(descartes),
                "The rebuilt Descartes method commits.");

        PhilosophyRunState shared = new()
        {
            WesternJourney = journey,
            SextusOpposedAppearances = sextus,
            DescartesMethodicReconstruction = descartes,
            NewtonTwoSites = NewtonTwoSitesState.Create("NEWTON:TEST:3", sextus.InstanceId, descartes.InstanceId),
        };
        return (Reload(shared), graph);
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        NewtonTwoSitesAction action)
    {
        NewtonTwoSitesState state = shared.NewtonTwoSites!;
        Check(NewtonTwoSitesFlow.TryPrepare(state, action), $"Prepare {action}.");
        shared = Reload(shared);
        state = shared.NewtonTwoSites!;
        Check(NewtonTwoSitesFlow.TryCommitPending(state, shared.WesternJourney!,
            shared.SextusOpposedAppearances!, shared.DescartesMethodicReconstruction!, graph),
            $"Commit {action} after a prepared save.");
        Check(!NewtonTwoSitesFlow.TryCommitPending(state, shared.WesternJourney!,
            shared.SextusOpposedAppearances!, shared.DescartesMethodicReconstruction!, graph),
            "The confirmed action cannot replay.");
        shared = Reload(shared);
        Check(NewtonTwoSitesFlow.IsRouteConsistent(shared.NewtonTwoSites!, shared.WesternJourney!,
            shared.SextusOpposedAppearances!, shared.DescartesMethodicReconstruction!),
            "The event and route survive save/reload together.");
        return shared;
    }

    private static void RunEventChecks()
    {
        // Decline + Leave: no edge, no adoption, third Fixed declined, ending retained.
        (PhilosophyRunState declined, WesternRouteGraph declinedGraph) = Fixture();
        declined = Commit(declined, declinedGraph, NewtonTwoSitesAction.Decline);
        declined = Commit(declined, declinedGraph, NewtonTwoSitesAction.Leave);
        Check(declined.NewtonTwoSites!.Closed && declined.NewtonTwoSites.IsUsable(),
            "Declined event closes as a usable state.");
        Check(declined.WesternJourney!.LastFixedAct == 3
            && declined.WesternJourney.DeclinedFixedActs.Contains(3)
            && declined.WesternJourney.RetainedEnding
            && !declined.WesternJourney.CompletedEdgeIds.Contains(NewtonTwoSitesState.CompletionEdgeId)
            && !declined.WesternJourney.SeenThinkerIds.Contains("ISAAC_NEWTON")
            && declined.WesternJourney.CurrentNodeId == NewtonTwoSitesState.SourceNodeId,
            "Declining keeps the source node and spends the third Fixed slot.");
        Check(!declined.WesternJourney.ResolvedQuestionActs.Contains(2)
            && !declined.WesternJourney.ResolvedQuestionActs.Contains(3),
            "Declining consumes neither the second nor the third Question.");

        NewtonTwoSitesAction[] measures = [NewtonTwoSitesAction.MeasureDrop, NewtonTwoSitesAction.MeasureTide];
        NewtonTwoSitesAction[] decisions = [NewtonTwoSitesAction.ConditionRelation,
            NewtonTwoSitesAction.RecheckOtherSite, NewtonTwoSitesAction.KeepLocal];
        foreach (NewtonTwoSitesAction measure in measures)
        foreach (NewtonTwoSitesAction decision in decisions)
        {
            (PhilosophyRunState shared, WesternRouteGraph graph) = Fixture();
            shared = Commit(shared, graph, NewtonTwoSitesAction.Accept);
            shared = Commit(shared, graph, measure);
            NewtonSite first = measure == NewtonTwoSitesAction.MeasureDrop ? NewtonSite.Drop : NewtonSite.Tide;
            Check(shared.NewtonTwoSites!.FirstSite == first
                && shared.NewtonTwoSites.CalibrationSite == first,
                "The first reading fixes the calibration site.");
            shared = Commit(shared, graph, NewtonTwoSitesAction.RecordError);
            shared = Commit(shared, graph, NewtonTwoSitesAction.RevealOther);
            shared = Commit(shared, graph, decision);
            shared = Commit(shared, graph, NewtonTwoSitesAction.Leave);

            NewtonTwoSitesState state = shared.NewtonTwoSites!;
            WesternJourneyState journey = shared.WesternJourney!;
            Check(state.Closed && state.IsUsable(), "The completed event stays usable.");
            Check(journey.LastFixedAct == 3
                && !journey.ResolvedQuestionActs.Contains(2)
                && !journey.ResolvedQuestionActs.Contains(3),
                "The event takes the third Fixed slot without consuming a Question.");
            Check(shared.SextusOpposedAppearances!.Closed && shared.SextusOpposedAppearances.IsUsable()
                && shared.DescartesMethodicReconstruction!.Closed
                && shared.DescartesMethodicReconstruction.IsUsable(),
                "Precursor events remain unchanged and usable.");

            bool adopted = decision is NewtonTwoSitesAction.ConditionRelation
                or NewtonTwoSitesAction.RecheckOtherSite;
            if (adopted)
            {
                Check(journey.CurrentNodeId == NewtonTwoSitesState.AdoptedNodeId
                    && journey.CompletedEdgeIds.Contains(NewtonTwoSitesState.CompletionEdgeId)
                    && journey.SeenThinkerIds.Contains("ISAAC_NEWTON")
                    && !journey.RetainedEnding,
                    "Conditioning or rechecking adopts Newton through edge 049.");
                Check(state.CalibrationSite == (decision == NewtonTwoSitesAction.RecheckOtherSite
                        ? (first == NewtonSite.Drop ? NewtonSite.Tide : NewtonSite.Drop)
                        : first),
                    "Only rechecking transfers the calibration site.");
            }
            else
            {
                Check(journey.CurrentNodeId == NewtonTwoSitesState.SourceNodeId
                    && !journey.CompletedEdgeIds.Contains(NewtonTwoSitesState.CompletionEdgeId)
                    && !journey.SeenThinkerIds.Contains("ISAAC_NEWTON")
                    && journey.DeclinedFixedActs.Contains(3) && journey.RetainedEnding,
                    "Keeping the local reading declines the third Fixed and retains the ending.");
                Check(state.CalibrationSite == first, "Keeping local leaves the calibration site.");
            }
        }

        // Absence of Newton on an old shared save stays absent.
        (PhilosophyRunState absent, _) = Fixture();
        absent.NewtonTwoSites = null;
        PhilosophyRunState absentReload = Reload(absent);
        Check(absentReload.NewtonTwoSites is null, "An old save without Newton stays without Newton.");

        // A legal pending action is usable and rejects a double prepare.
        (PhilosophyRunState pending, _) = Fixture();
        Check(NewtonTwoSitesFlow.TryPrepare(pending.NewtonTwoSites!, NewtonTwoSitesAction.Accept),
            "A legal action prepares.");
        pending = Reload(pending);
        Check(pending.NewtonTwoSites!.IsUsable(), "A pending legal action is usable.");
        Check(!NewtonTwoSitesFlow.TryPrepare(pending.NewtonTwoSites, NewtonTwoSitesAction.Decline),
            "A pending action rejects a second prepare.");

        // Missing required fields are rejected.
        foreach (string field in new[] { "Version", "Closed", "ConfirmedActions", "Stage", "PendingAction" })
        {
            (PhilosophyRunState shared, _) = Fixture();
            JsonObject node = JsonNode.Parse(JsonSerializer.Serialize(shared))!.AsObject();
            node["NewtonTwoSites"]!.AsObject().Remove(field);
            Check(!PhilosophyRunStateCodec.Decode(Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()))).NewtonTwoSites!.IsUsable(),
                $"A state missing {field} is rejected.");
        }

        // Invalid version, phantom history and forged calibration are rejected.
        (PhilosophyRunState badVersion, _) = Fixture();
        badVersion.NewtonTwoSites!.Version = 99;
        Check(!Reload(badVersion).NewtonTwoSites!.IsUsable(), "An unknown version is rejected.");

        (PhilosophyRunState phantom, _) = Fixture();
        phantom.NewtonTwoSites!.ConfirmedActions.Add(NewtonTwoSitesAction.Leave);
        Check(!Reload(phantom).NewtonTwoSites!.IsUsable(), "A phantom history is rejected.");

        (PhilosophyRunState forged, _) = Fixture();
        forged.NewtonTwoSites!.CalibrationSite = NewtonSite.Tide;
        Check(!Reload(forged).NewtonTwoSites!.IsUsable(), "A forged calibration site is rejected.");

        // A state bound to a different precursor instance is inconsistent.
        (PhilosophyRunState mismatched, _) = Fixture();
        mismatched.NewtonTwoSites!.SextusInstanceId = "SEXTUS:OTHER:9";
        Check(!NewtonTwoSitesFlow.IsRouteConsistent(mismatched.NewtonTwoSites,
            mismatched.WesternJourney!, mismatched.SextusOpposedAppearances!,
            mismatched.DescartesMethodicReconstruction!),
            "A state bound to another precursor instance is inconsistent.");

        Console.WriteLine("Newton two-sites event checks passed: 7 paths, adoption, calibration and save integrity.");
    }

    private static void Check(bool condition, string boundary)
    {
        if (!condition) throw new InvalidOperationException("Newton entry: " + boundary);
    }
}
