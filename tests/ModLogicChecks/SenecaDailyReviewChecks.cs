using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class SenecaDailyReviewChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        PhilosophyRunState old = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(new()));
        Check(old.SenecaDailyReview is null, "An old save cannot invent a Seneca review.");
        foreach (var observations in new[] { Array.Empty<int>(), new[] { 0 }, new[] { 1 }, new[] { 1, 1 } })
        {
            PhilosophyRunState shared = Fixture(observations);
            SenecaDailyReviewState review = shared.SenecaDailyReview!;
            Check(SenecaDailyReviewFlow.TryPrepare(review, SenecaDailyReviewAction.Begin)
                && !SenecaDailyReviewFlow.TryPrepare(review, SenecaDailyReviewAction.Begin),
                "Only one invitation action may be prepared.");
            shared = Reload(shared);
            review = shared.SenecaDailyReview!;
            Commit(shared);
            Check(SenecaDailyReviewFlow.TryPrepare(review, SenecaDailyReviewAction.ExamineAid),
                "The player may select an aid audit.");
            shared = Reload(shared);
            Commit(shared);
            Check(shared.SenecaDailyReview!.Stage == (observations.Length switch
            {
                >= 2 when observations.Count(value => value == 1) >= 2 => SenecaDailyReviewStage.ReviewAidRepeated,
                _ when observations.Contains(1) => SenecaDailyReviewStage.ReviewAidOnce,
                > 0 => SenecaDailyReviewStage.ReviewAidForegone,
                _ => SenecaDailyReviewStage.ReviewAidUnknown,
            }), "The saved observation determines the review page without guessing motive.");
        }
        CompleteAndRestore();
        ValidateGuards();
        ValidateLocalization();
        Console.WriteLine("Seneca review checks passed: frozen practice evidence, legal third-act trigger, pending save recovery, choice limits and bilingual pages.");
    }

    private static void CompleteAndRestore()
    {
        PhilosophyRunState shared = Fixture([1, 1]);
        foreach (SenecaDailyReviewAction action in new[]
        {
            SenecaDailyReviewAction.Begin, SenecaDailyReviewAction.ExamineAid,
            SenecaDailyReviewAction.BackToFocus, SenecaDailyReviewAction.ExamineJudgment,
            SenecaDailyReviewAction.ReviseCheck, SenecaDailyReviewAction.Leave,
        })
        {
            SenecaDailyReviewState state = shared.SenecaDailyReview!;
            Check(SenecaDailyReviewFlow.TryPrepare(state, action), "A legal choice prepares.");
            shared = Reload(shared);
            Check(Commit(shared), "A saved pending choice commits once.");
            Check(!Commit(shared), "Duplicate confirmation cannot commit again.");
            shared = Reload(shared);
        }
        SenecaDailyReviewState complete = shared.SenecaDailyReview!;
        Check(complete.Closed && complete.Stage == SenecaDailyReviewStage.ResultJudgmentRevised
            && complete.ConfirmedActions.Count == 6
            && shared.ZenoRoutePayload.State!.Route!.Outcome == ZenoAssentOutcome.NoAssent
            && shared.DiogenesStaySwitchEntry!.Practice == new DiogenesPracticeSummary(2, 0, 2),
            "The result records only a bounded intention; source facts and route remain unchanged.");
        Check(!SenecaDailyReviewFlow.TryPrepare(complete, SenecaDailyReviewAction.Begin),
            "The closed review cannot reopen.");
        complete.Stage = SenecaDailyReviewStage.Invitation;
        ExpectInvalid(shared, "A forged old page must be isolated.");
    }

    private static void ValidateGuards()
    {
        SenecaDailyReviewTriggerContext eligible = new(true, true, true, 2, true, true,
            false, false, false, false, true);
        Check(SenecaDailyReviewTriggerPolicy.ShouldInsert(eligible), "The closed third-act route may open once.");
        foreach (var blocked in new[]
        {
            eligible with { CurrentActIndex = 1 }, eligible with { IsSingleplayer = false },
            eligible with { AtSafeMapBoundary = false }, eligible with { HasClosedZenoRoute = false },
            eligible with { HasClosedDiogenesEntry = false }, eligible with { HasPendingZenoOperation = true },
            eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!SenecaDailyReviewTriggerPolicy.ShouldInsert(blocked), "Unsafe or consumed routes stay closed.");
        Check(SenecaDailyReviewTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true }), "An open saved review can resume.");

        PhilosophyRunState shared = Fixture([]);
        SenecaDailyReviewState review = shared.SenecaDailyReview!;
        Check(review.Practice == new DiogenesPracticeSummary(0, 0, 0),
            "Zero observations are an explicit empty record.");
        Check(!SenecaDailyReviewFlow.TryPrepare(review, SenecaDailyReviewAction.ReviseCheck),
            "A result cannot be chosen before an audit focus.");
        foreach (string field in new[] { "Version", "InstanceId", "RunId", "Outcome", "Stage", "PendingAction", "ConfirmedActions", "Closed" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(review)!;
            node.AsObject().Remove(field);
            Check(!JsonSerializer.Deserialize<SenecaDailyReviewState>(node)!.IsUsable(),
                "Missing save fields cannot become a new invitation.");
        }
        shared.SenecaDailyReview = review;
        shared.SenecaDailyReview.Outcome = ZenoAssentOutcome.Keep;
        ExpectInvalid(shared, "A changed frozen outcome cannot be rewritten.");
    }

    private static void ValidateLocalization()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument localization = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            Check(localization.RootElement.TryGetProperty("SENECA_DAILY_REVIEW.title", out _), "Title is localized.");
            foreach (SenecaDailyReviewStage stage in Enum.GetValues<SenecaDailyReviewStage>())
            {
                string page = $"SENECA_DAILY_REVIEW.pages.{SenecaDailyReviewFlow.StageKey(stage)}";
                Check(localization.RootElement.TryGetProperty(page + ".description", out _), "Every page is localized.");
                foreach (SenecaDailyReviewAction action in SenecaDailyReviewFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(localization.RootElement.TryGetProperty(
                        $"{page}.options.{SenecaDailyReviewFlow.ActionKey(action)}.{suffix}", out _),
                        "Every available option is localized.");
            }
        }
    }

    internal static PhilosophyRunState Fixture(int[] selected)
    {
        PhilosophyRunState shared = new()
        {
            CurrentDoctrine = new() { ThinkerId = "SOCRATES", DoctrineId = "SOCRATES_BALANCE_WEIGHT" },
            WesternJourney = new() { CurrentNodeId = SocratesVirtueUpstreamRecord.VirtueRouteNodeId, LastFixedAct = 1 },
            DiogenesPractice = new(),
        };
        for (int i = 0; i < selected.Length; i++)
            Check(shared.DiogenesPractice.TryRecord(0, i + 1, 2, selected[i]), "Fixture observation is valid.");
        Check(DiogenesStaySwitchEntryPolicy.TryCreate(shared, 42, new(2, 1, true, false, 17), out var created)
            && created is not null, "Fixture must use the real Diogenes entry.");
        DiogenesStaySwitchEntryRecord entry = created! with { Closed = true };
        shared.DiogenesStaySwitchEntry = entry;
        ZenoRouteState route = new(ZenoRouteState.CurrentVersion, ZenoRouteStage.ZenoClosed,
            6, 6, entry.RunId, 2, ZenoRouteIds.RouteEdge, ZenoRouteIds.TerminalNode,
            entry.Material, "ROOM_18", ZenoOpeningTrigger.SafeBoundary, ZenoResumeDestination.Map,
            "MAP_18", "ZENO_18", ZenoAssentOutcome.NoAssent, null);
        shared.SetCurrentZenoRouteState(new(ZenoRouteFeatureGeneration.Current, route));
        shared.SenecaDailyReview = SenecaDailyReviewState.Create("SENECA_DAILY_REVIEW:42:1", route, entry);
        return Reload(shared);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static bool Commit(PhilosophyRunState shared) =>
        SenecaDailyReviewFlow.TryCommitPending(shared.SenecaDailyReview!,
            shared.ZenoRoutePayload.State!.Route!, shared.DiogenesStaySwitchEntry!);

    private static void ExpectInvalid(PhilosophyRunState shared, string message)
    {
        try { _ = PhilosophyRunStateCodec.Encode(shared); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException(message);
    }

}
