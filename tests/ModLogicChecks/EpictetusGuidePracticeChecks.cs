using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class EpictetusGuidePracticeChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        PhilosophyRunState before = SenecaDailyReviewChecks.Fixture([]);
        Check(before.EpictetusGuidePractice is null, "Earlier saves do not invent Epictetus.");
        var eligible = new EpictetusGuidePracticeTriggerContext(true, true, true, 2, true, true,
            false, false, false, false, true);
        Check(EpictetusGuidePracticeTriggerPolicy.ShouldInsert(eligible),
            "A closed Zeno route and Seneca review authorize the third-act practice.");
        foreach (var blocked in new[]
        {
            eligible with { CurrentActIndex = 1 }, eligible with { AtSafeMapBoundary = false },
            eligible with { IsSingleplayer = false }, eligible with { HasClosedZenoRoute = false },
            eligible with { HasClosedSenecaReview = false }, eligible with { HasPendingZenoOperation = true },
            eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!EpictetusGuidePracticeTriggerPolicy.ShouldInsert(blocked),
            "A wrong act, unsafe boundary or consumed instance cannot reenter.");
        Check(EpictetusGuidePracticeTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true }),
            "An open saved practice may recover.");

        foreach (EpictetusGuidePracticeAction first in new[]
        {
            EpictetusGuidePracticeAction.GuideWithReservation,
            EpictetusGuidePracticeAction.PromisePassage,
        })
        foreach (EpictetusGuidePracticeAction second in new[]
        {
            EpictetusGuidePracticeAction.EscortCompanion,
            EpictetusGuidePracticeAction.MarkThenReturn,
        })
        {
            PhilosophyRunState shared = Fixture();
            foreach (EpictetusGuidePracticeAction action in new[] { first, second, EpictetusGuidePracticeAction.Leave })
            {
                EpictetusGuidePracticeState state = shared.EpictetusGuidePractice!;
                Check(EpictetusGuidePracticeFlow.TryPrepare(state, action)
                    && !EpictetusGuidePracticeFlow.TryPrepare(state, action),
                    "Each action has one write-ahead slot.");
                shared = Reload(shared);
                Check(Commit(shared) && !Commit(shared),
                    "A saved choice commits exactly once, including the post-response action.");
                shared = Reload(shared);
            }
            EpictetusGuidePracticeState finished = shared.EpictetusGuidePractice!;
            Check(finished.Closed && finished.ConfirmedActions.SequenceEqual(
                new[] { first, second, EpictetusGuidePracticeAction.Leave })
                && finished.Stage == EpictetusGuidePracticeFlow.Next(
                    EpictetusGuidePracticeFlow.Next(EpictetusGuidePracticeStage.Invitation, first), second)
                && shared.ZenoRoutePayload.State!.Route!.Outcome == ZenoAssentOutcome.NoAssent
                && shared.SenecaDailyReview!.Closed,
                "Two distinct actions and the external response persist without changing prior outcomes.");
        }
        PhilosophyRunState declined = Fixture();
        Check(EpictetusGuidePracticeFlow.TryPrepare(declined.EpictetusGuidePractice!,
            EpictetusGuidePracticeAction.Decline), "Decline is a legal branch.");
        declined = Reload(declined);
        Check(Commit(declined), "Decline commits.");
        Check(EpictetusGuidePracticeFlow.TryPrepare(declined.EpictetusGuidePractice!,
            EpictetusGuidePracticeAction.Leave), "Declined encounter can close.");
        declined = Reload(declined);
        Check(Commit(declined) && Reload(declined).EpictetusGuidePractice!.Closed,
            "Declining never fabricates later guiding actions.");

        PhilosophyRunState corrupted = Fixture();
        corrupted.EpictetusGuidePractice!.ZenoOutcome = ZenoAssentOutcome.Keep;
        ExpectInvalid(corrupted, "A forged earlier outcome cannot be rewritten.");
        corrupted = Fixture();
        corrupted.SenecaDailyReview!.Closed = false;
        ExpectInvalid(corrupted, "An unclosed predecessor cannot authorize this practice.");
        foreach (string field in new[]
        { "Version", "InstanceId", "RunId", "ZenoOutcome", "SenecaInstanceId", "Stage", "PendingAction", "ConfirmedActions", "Closed" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(Fixture().EpictetusGuidePractice)!;
            node.AsObject().Remove(field);
            Check(!JsonSerializer.Deserialize<EpictetusGuidePracticeState>(node)!.IsUsable(),
                "Missing save fields cannot turn into an invitation.");
        }
        ValidateLocalization();
        Console.WriteLine("Epictetus practice checks passed: five complete paths, two saved actions around an external response, strict predecessors and bilingual pages.");
    }

    internal static PhilosophyRunState Fixture()
    {
        PhilosophyRunState shared = SenecaDailyReviewChecks.Fixture([]);
        foreach (SenecaDailyReviewAction action in new[]
        {
            SenecaDailyReviewAction.Begin, SenecaDailyReviewAction.ExamineJudgment,
            SenecaDailyReviewAction.KeepCheck, SenecaDailyReviewAction.Leave,
        })
        {
            Check(SenecaDailyReviewFlow.TryPrepare(shared.SenecaDailyReview!, action),
                "Fixture Seneca action prepares.");
            shared = Reload(shared);
            Check(SenecaDailyReviewFlow.TryCommitPending(shared.SenecaDailyReview!,
                shared.ZenoRoutePayload.State!.Route!, shared.DiogenesStaySwitchEntry!),
                "Fixture Seneca action commits.");
            shared = Reload(shared);
        }
        ZenoRouteState route = shared.ZenoRoutePayload.State!.Route!;
        shared.EpictetusGuidePractice = EpictetusGuidePracticeState.Create(
            "EPICTETUS_GUIDE_PRACTICE:42:1", route, shared.SenecaDailyReview!);
        return Reload(shared);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static bool Commit(PhilosophyRunState shared) =>
        EpictetusGuidePracticeFlow.TryCommitPending(shared.EpictetusGuidePractice!,
            shared.ZenoRoutePayload.State!.Route!, shared.SenecaDailyReview!);

    private static void ExpectInvalid(PhilosophyRunState shared, string message)
    {
        try { _ = PhilosophyRunStateCodec.Encode(shared); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void ValidateLocalization()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument localization = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            Check(localization.RootElement.TryGetProperty("EPICTETUS_GUIDE_PRACTICE.title", out _),
                "The native title is bilingual.");
            foreach (EpictetusGuidePracticeStage stage in Enum.GetValues<EpictetusGuidePracticeStage>())
            {
                string page = $"EPICTETUS_GUIDE_PRACTICE.pages.{EpictetusGuidePracticeFlow.StageKey(stage)}";
                Check(localization.RootElement.TryGetProperty(page + ".description", out _),
                    "Every page is bilingual.");
                foreach (EpictetusGuidePracticeAction action in EpictetusGuidePracticeFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(localization.RootElement.TryGetProperty(
                        $"{page}.options.{EpictetusGuidePracticeFlow.ActionKey(action)}.{suffix}", out _),
                        "Every legal option is bilingual.");
            }
        }
    }
}
