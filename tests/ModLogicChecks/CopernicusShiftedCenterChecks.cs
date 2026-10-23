using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using STS2Philosophers;

internal static class CopernicusShiftedCenterChecks
{
    private enum EmpedoclesOutcome { Record, Keep, Stop, Decline }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState state) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));

    private static CopernicusForkSnapshot Fork() => new("COPERNICUS_CHECK_SEED", 1, 4, 2, 73, 5, 5,
        [new(5, 1, "Monster"), new(5, 4, "RestSite")]);

    private static PhilosophyRunState Fixture(WesternRouteGraph graph,
        EmpedoclesOutcome outcome = EmpedoclesOutcome.Record)
    {
        WesternJourneyState journey = graph.Start("HERACLITUS__BEING_AND_CHANGE__CORE");
        Check(graph.TryApply(journey, DemocritusArrangementState.PrerequisiteEdgeId),
            "The actual first-act challenge reaches Parmenides and consumes Q1.");
        DemocritusArrangementState democritus = DemocritusArrangementState.Create("DEMOCRITUS:COPERNICUS:2");
        foreach (DemocritusArrangementAction action in new[]
        {
            DemocritusArrangementAction.AcceptInvitation, DemocritusArrangementAction.StudyMotion,
            DemocritusArrangementAction.LeaveGap, DemocritusArrangementAction.AdoptAtoms,
            DemocritusArrangementAction.Leave,
        })
            Check(DemocritusArrangementFlow.TryPrepare(democritus, action)
                && DemocritusArrangementFlow.TryCommitPending(democritus, journey, graph),
                "The real Democritus inquiry adopts atoms and closes instead of forging Closed.");

        Check(graph.TryReserveQuestion(journey, 2), "The real second-act question is consumed before Empedocles.");
        EmpedoclesRootsInquiryState empedocles = EmpedoclesRootsInquiryState.Create("EMPEDOCLES:COPERNICUS:2");
        EmpedoclesRootsInquiryAction[] path = outcome switch
        {
            EmpedoclesOutcome.Decline =>
                [EmpedoclesRootsInquiryAction.DeclineInvitation, EmpedoclesRootsInquiryAction.Leave],
            EmpedoclesOutcome.Stop =>
                [EmpedoclesRootsInquiryAction.AcceptInvitation, EmpedoclesRootsInquiryAction.StopInquiry,
                    EmpedoclesRootsInquiryAction.Leave],
            _ =>
                [EmpedoclesRootsInquiryAction.AcceptInvitation, EmpedoclesRootsInquiryAction.StudyComposition,
                    EmpedoclesRootsInquiryAction.CombineDifferent,
                    outcome == EmpedoclesOutcome.Record ? EmpedoclesRootsInquiryAction.RecordComparison
                        : EmpedoclesRootsInquiryAction.KeepAtoms,
                    EmpedoclesRootsInquiryAction.Leave],
        };
        foreach (EmpedoclesRootsInquiryAction action in path)
            Check(EmpedoclesRootsInquiryFlow.TryPrepare(empedocles, action)
                && EmpedoclesRootsInquiryFlow.TryCommitPending(empedocles, journey, graph),
                "The actual Empedocles question supplies the saved predecessor history.");

        return Reload(new()
        {
            WesternJourney = journey,
            DemocritusArrangement = democritus,
            EmpedoclesRootsInquiry = empedocles,
            CopernicusShiftedCenter = CopernicusShiftedCenterState.Create(
                "COPERNICUS_SHIFTED_CENTER:COPERNICUS_CHECK_SEED:2", democritus.InstanceId,
                empedocles.InstanceId, Fork()),
        });
    }

    private static bool Consistent(PhilosophyRunState shared, WesternRouteGraph graph) =>
        CopernicusShiftedCenterFlow.IsRouteConsistent(shared.CopernicusShiftedCenter!, shared.WesternJourney!,
            shared.DemocritusArrangement!, shared.EmpedoclesRootsInquiry!, graph);

    private static bool CommitPending(PhilosophyRunState shared, WesternRouteGraph graph) =>
        CopernicusShiftedCenterFlow.TryCommitPending(shared.CopernicusShiftedCenter!, shared.WesternJourney!,
            shared.DemocritusArrangement!, shared.EmpedoclesRootsInquiry!, graph);

    private static string OtherSharedState(PhilosophyRunState shared)
    {
        JsonObject node = JsonSerializer.SerializeToNode(shared)!.AsObject();
        node.Remove(nameof(PhilosophyRunState.CopernicusShiftedCenter));
        return node.ToJsonString();
    }

    private static PhilosophyRunState Commit(PhilosophyRunState shared, WesternRouteGraph graph,
        CopernicusShiftedCenterAction action)
    {
        string untouched = OtherSharedState(shared);
        CopernicusShiftedCenterState state = shared.CopernicusShiftedCenter!;
        Check(Consistent(shared, graph) && CopernicusShiftedCenterFlow.TryPrepare(state, action)
            && !CopernicusShiftedCenterFlow.TryPrepare(state, action),
            $"Only one real Copernicus choice can be prepared: {action}.");
        shared = Reload(shared);
        state = shared.CopernicusShiftedCenter!;
        Check(state.PendingAction == action && state.IsUsable() && Consistent(shared, graph),
            "A prepared choice survives the complete shared codec without becoming confirmed.");
        if (action == CopernicusShiftedCenterAction.FollowTable)
            Check(!state.Closed && !state.TravelPending && state.SelectedExitIndex == -1
                && state.ResidualRecorded,
                "Preparing navigation is not arrival, closure, or a selected native destination yet.");
        Check(CommitPending(shared, graph) && !CommitPending(shared, graph),
            "A saved pending operation commits once and a duplicate commit cannot replay it.");
        shared = Reload(shared);
        Check(Consistent(shared, graph) && shared.CopernicusShiftedCenter!.PendingAction == CopernicusShiftedCenterAction.None
            && OtherSharedState(shared) == untouched,
            "Confirmed practice preserves every other shared field, including Fixed/Question, doctrine and reward history.");
        return shared;
    }

    private static List<CopernicusShiftedCenterAction[]> CompletePaths()
    {
        CopernicusShiftedCenterAction accept = CopernicusShiftedCenterAction.AcceptInvitation;
        CopernicusShiftedCenterAction recenter = CopernicusShiftedCenterAction.RecenterOnSun;
        CopernicusShiftedCenterAction observer = CopernicusShiftedCenterAction.PlaceObserver;
        CopernicusShiftedCenterAction planet = CopernicusShiftedCenterAction.PlacePlanet;
        CopernicusShiftedCenterAction apparent = CopernicusShiftedCenterAction.PlaceApparent;
        List<CopernicusShiftedCenterAction[]> prefixes =
        [
            [accept, CopernicusShiftedCenterAction.KeepOldTable],
            [accept, recenter, observer, planet, apparent],
            [accept, recenter, observer, apparent, planet],
            [accept, recenter, planet, observer, apparent],
            [accept, recenter, planet, apparent, observer],
        ];
        List<CopernicusShiftedCenterAction[]> paths =
            [[CopernicusShiftedCenterAction.DeclineInvitation, CopernicusShiftedCenterAction.Leave]];
        foreach (CopernicusShiftedCenterAction[] prefix in prefixes)
        {
            paths.Add([.. prefix, CopernicusShiftedCenterAction.Withdraw, CopernicusShiftedCenterAction.Leave]);
            paths.Add([.. prefix, CopernicusShiftedCenterAction.RecordResidual,
                CopernicusShiftedCenterAction.Withdraw, CopernicusShiftedCenterAction.Leave]);
            paths.Add([.. prefix, CopernicusShiftedCenterAction.RecordResidual,
                CopernicusShiftedCenterAction.FollowTable]);
        }
        return paths;
    }

    public static void Run()
    {
        WesternRouteGraph graph = WesternRouteGraph.LoadEmbedded();
        List<CopernicusShiftedCenterAction[]> paths = CompletePaths();
        Check(paths.Count == 16, "One refusal and five complete table choices each have their three real endings.");
        HashSet<string> solarOrders = new(StringComparer.Ordinal);
        HashSet<CopernicusShiftedCenterStage> withdrawStages = [];
        foreach (EmpedoclesOutcome predecessor in new[]
            { EmpedoclesOutcome.Record, EmpedoclesOutcome.Keep, EmpedoclesOutcome.Stop })
        foreach (CopernicusShiftedCenterAction[] path in paths)
        {
            PhilosophyRunState shared = Fixture(graph, predecessor);
            string untouched = OtherSharedState(shared);
            foreach (CopernicusShiftedCenterAction action in path)
            {
                CopernicusShiftedCenterState state = shared.CopernicusShiftedCenter!;
                Check(CopernicusShiftedCenterFlow.Options(state.Stage).Count <= 2,
                    "Every actual practice step exposes no more than two buttons.");
                string beforeRejected = JsonSerializer.Serialize(state);
                if (!CopernicusShiftedCenterFlow.Options(state.Stage).Contains(CopernicusShiftedCenterAction.FollowTable))
                    Check(!CopernicusShiftedCenterFlow.TryPrepare(state, CopernicusShiftedCenterAction.FollowTable)
                        && JsonSerializer.Serialize(state) == beforeRejected,
                        "Navigation before a completed table and explicit residual recording is rejected without mutation.");
                if (!CopernicusShiftedCenterFlow.Options(state.Stage).Contains(CopernicusShiftedCenterAction.RecordResidual))
                    Check(!CopernicusShiftedCenterFlow.TryPrepare(state, CopernicusShiftedCenterAction.RecordResidual),
                        "A residual cannot be recorded before its actual observation page.");
                if (action == CopernicusShiftedCenterAction.Withdraw) withdrawStages.Add(state.Stage);
                shared = Commit(shared, graph, action);
            }
            CopernicusShiftedCenterState final = shared.CopernicusShiftedCenter!;
            bool follows = path[^1] == CopernicusShiftedCenterAction.FollowTable;
            bool solar = path.Contains(CopernicusShiftedCenterAction.RecenterOnSun);
            Check(final.Closed && final.IsUsable() && final.ConfirmedActions.SequenceEqual(path)
                && final.TravelPending == follows
                && final.SelectedExitIndex == (follows ? solar ? 1 : 0 : -1)
                && final.Stage == (follows ? CopernicusShiftedCenterStage.TravelReady
                    : path[0] == CopernicusShiftedCenterAction.DeclineInvitation
                        ? CopernicusShiftedCenterStage.ResultDeclined : CopernicusShiftedCenterStage.ResultWithdrawn)
                && OtherSharedState(shared) == untouched,
                "Each closed outcome retains its exact history and only following the table prepares a native destination.");
            Check(!CopernicusShiftedCenterFlow.TryPrepare(final, CopernicusShiftedCenterAction.Leave)
                && !CommitPending(shared, graph), "A closed practice cannot reopen or navigate again via another choice.");
            if (solar)
            {
                Check(final.Order.Count == 3 && final.Order.Distinct().Count() == 3,
                    "All three actual observations are recorded exactly once, including on withdrawal.");
                solarOrders.Add(string.Join(',', final.Order));
            }
            else Check(final.Order.SequenceEqual([CopernicusObservation.Apparent,
                    CopernicusObservation.Planet, CopernicusObservation.Observer]),
                "Keeping or declining the old table never secretly rearranges it.");
            if (follows)
                Check(final.ResidualRecorded && final.Fork.VisitedCount == 5 && final.Fork.HistoryCount == 5
                    && !JsonSerializer.SerializeToNode(final)!.AsObject().ContainsKey(nameof(final.TravelPending)),
                    "A persisted travel intention does not fabricate native visits/history or a serialized arrival receipt.");
        }
        Check(solarOrders.Count == 4 && withdrawStages.SetEquals([
            CopernicusShiftedCenterStage.ResidualOriginal, CopernicusShiftedCenterStage.ResidualSolar,
            CopernicusShiftedCenterStage.RouteOriginal, CopernicusShiftedCenterStage.RouteSolar]),
            "All four three-step new arrangements and every permitted withdrawal page are covered.");
        Check(Reload(new()).CopernicusShiftedCenter is null, "Pre-feature saves cannot acquire a practice or navigation intent.");
        InvalidHistory(graph);
        InvalidForks();
        Trigger();
        Localization();
        Console.WriteLine("Copernicus checks passed: 48 saved paths with real Democritus/accepted Empedocles predecessors, four three-step orders and old table, all withdrawals, pending/confirmed recovery, preserved Fixed/Question and other shared fields, strict history/fork identity, bilingual keys/placeholders; actual native travel is checked separately.");
    }

    private static void InvalidHistory(WesternRouteGraph graph)
    {
        PhilosophyRunState shared = Fixture(graph);
        foreach (CopernicusShiftedCenterAction action in new[]
        {
            CopernicusShiftedCenterAction.AcceptInvitation, CopernicusShiftedCenterAction.RecenterOnSun,
            CopernicusShiftedCenterAction.PlaceObserver, CopernicusShiftedCenterAction.PlacePlanet,
            CopernicusShiftedCenterAction.PlaceApparent, CopernicusShiftedCenterAction.RecordResidual,
            CopernicusShiftedCenterAction.FollowTable,
        }) shared = Commit(shared, graph, action);
        CopernicusShiftedCenterState complete = shared.CopernicusShiftedCenter!;
        foreach (string missing in new[]
        {
            "Version", "InstanceId", "DemocritusInstanceId", "EmpedoclesInstanceId", "Fork", "Stage",
            "PendingAction", "ConfirmedActions", "Reference", "Order", "ResidualRecorded", "SelectedExitIndex", "Closed",
        })
        {
            JsonObject node = JsonSerializer.SerializeToNode(complete)!.AsObject(); node.Remove(missing);
            Check(RejectsState(node), $"Missing {missing} cannot supply a completed observation or travel intent.");
        }
        foreach (string unknown in new[] { "Version", "Stage", "PendingAction", "Reference", "SelectedExitIndex" })
        {
            JsonObject node = JsonSerializer.SerializeToNode(complete)!.AsObject(); node[unknown] = 99;
            Check(RejectsState(node), $"Unknown {unknown} cannot resume a valid practice.");
        }
        foreach (string wrong in new[] { "missing_residual", "duplicate_observer", "forged_closed", "foreign_democritus", "foreign_empedocles" })
        {
            PhilosophyRunState bad = Reload(shared);
            switch (wrong)
            {
                case "missing_residual": bad.CopernicusShiftedCenter!.ResidualRecorded = false; break;
                case "duplicate_observer": bad.CopernicusShiftedCenter!.Order[1] = CopernicusObservation.Observer; break;
                case "forged_closed": bad.CopernicusShiftedCenter!.ConfirmedActions.RemoveAt(0); break;
                case "foreign_democritus": bad.DemocritusArrangement!.InstanceId = "OTHER:DEMOCRITUS"; break;
                case "foreign_empedocles": bad.EmpedoclesRootsInquiry!.InstanceId = "OTHER:EMPEDOCLES"; break;
            }
            Check(!Consistent(bad, graph), $"Mismatched observation or predecessor identity is rejected: {wrong}.");
        }
        PhilosophyRunState declined = Fixture(graph, EmpedoclesOutcome.Decline);
        Check(!Consistent(declined, graph), "A genuinely declined Empedocles invitation is not accepted practice history.");
        PhilosophyRunState wrongJourney = Fixture(graph);
        wrongJourney.WesternJourney = graph.Start("SOCRATES__KNOWLEDGE_AND_DOUBT__CORE");
        Check(!Consistent(wrongJourney, graph), "The practice cannot seize the existing knowledge route's invitations.");
        PhilosophyRunState missingQuestion = Fixture(graph); missingQuestion.WesternJourney!.ResolvedQuestionActs.Remove(2);
        Check(!Consistent(missingQuestion, graph), "Missing real Q2 cannot be inferred from a closed predecessor alone.");
    }

    private static bool RejectsState(JsonObject node)
    {
        try { return node.Deserialize<CopernicusShiftedCenterState>()?.IsUsable() != true; }
        catch (JsonException) { return true; }
    }

    private static void InvalidForks()
    {
        CopernicusForkSnapshot original = Fork();
        Check(original.IsUsable(), "The explicit two-exit fork fixture is structurally valid.");
        foreach (CopernicusForkSnapshot bad in new[]
        {
            original with { RunSeed = "" }, original with { ActIndex = 0 }, original with { ActIndex = 2 },
            original with { SourceRow = -1 }, original with { SourceColumn = -1 }, original with { SourceRoomId = -1 },
            original with { VisitedCount = 0 }, original with { HistoryCount = 0 },
            original with { Exits = null! }, original with { Exits = [] },
            original with { Exits = [original.Exits[0]] },
            original with { Exits = [original.Exits[0], original.Exits[1], new(5, 6, "Unknown")] },
            original with { Exits = [original.Exits[1], original.Exits[0]] },
            original with { Exits = [original.Exits[0], original.Exits[0]] },
            original with { Exits = [original.Exits[0] with { Row = original.SourceRow }, original.Exits[1]] },
            original with { Exits = [original.Exits[0] with { Column = -1 }, original.Exits[1]] },
            original with { Exits = [original.Exits[0] with { PointType = "Boss" }, original.Exits[1]] },
            original with { Exits = [original.Exits[0] with { PointType = "Unassigned" }, original.Exits[1]] },
            original with { Exits = [original.Exits[0] with { PointType = "" }, original.Exits[1]] },
            original with { Exits = [null!, original.Exits[1]] },
        }) Check(!bad.IsUsable(), "Malformed, reversed, duplicate or forbidden exits cannot become a usable two-route snapshot.");
        foreach (string missing in new[]
            { "RunSeed", "ActIndex", "SourceRow", "SourceColumn", "SourceRoomId", "VisitedCount", "HistoryCount", "Exits" })
        {
            JsonObject node = JsonSerializer.SerializeToNode(original)!.AsObject(); node.Remove(missing);
            Check(RejectsFork(node), $"Missing fork identity {missing} cannot be silently replaced by a legal zero/default.");
        }
        foreach (string missing in new[] { "Row", "Column", "PointType" })
        {
            JsonObject node = JsonSerializer.SerializeToNode(original)!.AsObject();
            node["Exits"]![0]!.AsObject().Remove(missing);
            Check(RejectsFork(node), $"Missing exit identity {missing} cannot authorize a different native target.");
        }
    }

    private static bool RejectsFork(JsonObject node)
    {
        try { return node.Deserialize<CopernicusForkSnapshot>()?.IsUsable() != true; }
        catch (JsonException) { return true; }
    }

    private static void Trigger()
    {
        CopernicusShiftedCenterTriggerContext valid = new(true, true, true, 1, true, true, true, true,
            false, false, false, false, true);
        Check(CopernicusShiftedCenterTriggerPolicy.ShouldInsert(valid), "The actual unused binary fork practice is reachable.");
        foreach (var blocked in new[] { valid with { RunInProgress = false }, valid with { AtSafeMapBoundary = false },
            valid with { IsSingleplayer = false }, valid with { CurrentActIndex = 0 }, valid with { CurrentActIndex = 2 },
            valid with { SourceIsCompletedNonBossCombat = false }, valid with { HasRequiredRelic = false },
            valid with { PredecessorReady = false }, valid with { NativeForkReady = false },
            valid with { HasSavedInstance = true }, valid with { ModelAvailable = false } })
            Check(!CopernicusShiftedCenterTriggerPolicy.ShouldInsert(blocked), "Missing native source, fork or completed predecessor blocks practice insertion.");
        var saved = valid with { HasSavedInstance = true, SavedInstanceUsable = true, SavedRouteConsistent = true,
            SourceIsCompletedNonBossCombat = false, PredecessorReady = false };
        Check(CopernicusShiftedCenterTriggerPolicy.ShouldInsert(saved)
            && !CopernicusShiftedCenterTriggerPolicy.ShouldInsert(saved with { SavedInstanceClosed = true })
            && !CopernicusShiftedCenterTriggerPolicy.ShouldInsert(saved with { NativeForkReady = false }),
            "A saved open practice resumes the same fork; closed navigation intentions never reinsert at later map points.");
    }

    private static void Localization()
    {
        const string prefix = "COPERNICUS_SHIFTED_CENTER.";
        Dictionary<string, string> Read(string language)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            return document.RootElement.EnumerateObject().Where(p => p.Name.StartsWith(prefix, StringComparison.Ordinal))
                .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
        }
        Dictionary<string, string> english = Read("eng"), chinese = Read("zhs");
        Check(english.Count > 0 && english.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(chinese.Keys),
            "Every Copernicus player-facing key exists in both languages.");
        foreach (CopernicusShiftedCenterStage stage in Enum.GetValues<CopernicusShiftedCenterStage>())
        {
            string page = prefix + "pages." + CopernicusShiftedCenterFlow.StageKey(stage);
            Check(english.ContainsKey(page + ".description"), $"The real {stage} event page is bilingual.");
            foreach (CopernicusShiftedCenterAction action in CopernicusShiftedCenterFlow.Options(stage))
            foreach (string suffix in new[] { "title", "description" })
                Check(english.ContainsKey(page + ".options." + CopernicusShiftedCenterFlow.ActionKey(action) + "." + suffix),
                    $"The actual {stage}/{action} button has bilingual {suffix}.");
        }
        HashSet<string> allPlaceholders = new(StringComparer.Ordinal);
        foreach ((string key, string value) in english)
        {
            Check(!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(chinese[key]),
                "Neither language contains an empty player-facing text value.");
            HashSet<string> Placeholders(string text) => Regex.Matches(text, @"\{([A-Za-z][A-Za-z0-9_]*)\}")
                .Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
            HashSet<string> placeholders = Placeholders(value);
            Check(placeholders.SetEquals(Placeholders(chinese[key]))
                && placeholders.All(p => p is "ExitA" or "ExitB" or "Order"),
                $"Bilingual dynamic placeholders match the supported native fork/observation fields: {key}.");
            allPlaceholders.UnionWith(placeholders);
        }
        Check(allPlaceholders.SetEquals(["ExitA", "ExitB", "Order"]),
            "The actual exit choices and the recorded order are represented in the localized pages.");
    }
}
