using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class MarcusAureliusCampPracticeChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        PhilosophyRunState before = EpictetusGuidePracticeChecks.Fixture();
        Check(before.MarcusAureliusCampPractice is null, "Earlier saves do not invent Marcus.");
        var eligible = new MarcusAureliusCampPracticeTriggerContext(
            true, true, true, 2, true, true, true, true, false, false, false, false, true);
        Check(MarcusAureliusCampPracticeTriggerPolicy.ShouldInsert(eligible),
            "A later completed combat permits a third-act camp practice.");
        foreach (var blocked in new[]
        {
            eligible with { CurrentActIndex = 1 }, eligible with { AtSafeMapBoundary = false },
            eligible with { IsSingleplayer = false }, eligible with { SourceIsCompletedNonBossCombat = false },
            eligible with { HasClosedZenoRoute = false }, eligible with { HasClosedSenecaReview = false },
            eligible with { HasClosedEpictetusPractice = false }, eligible with { HasPendingZenoOperation = true },
            eligible with { ModelAvailable = false }, eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!MarcusAureliusCampPracticeTriggerPolicy.ShouldInsert(blocked),
            "An event-source map, wrong act, unsafe boundary or consumed instance cannot insert.");
        Check(MarcusAureliusCampPracticeTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true }),
            "An open saved practice may recover after combat.");

        foreach (MarcusAureliusCampPracticeAction first in new[]
        {
            MarcusAureliusCampPracticeAction.AskNeed, MarcusAureliusCampPracticeAction.StatePlan,
        })
        foreach (MarcusAureliusCampPracticeAction second in new[]
        {
            MarcusAureliusCampPracticeAction.WorkTogether, MarcusAureliusCampPracticeAction.OrganizeAndMark,
        })
        {
            PhilosophyRunState shared = Fixture();
            foreach (MarcusAureliusCampPracticeAction action in new[] { first, second, MarcusAureliusCampPracticeAction.Leave })
            {
                MarcusAureliusCampPracticeState state = shared.MarcusAureliusCampPractice!;
                Check(MarcusAureliusCampPracticeFlow.TryPrepare(state, action)
                    && !MarcusAureliusCampPracticeFlow.TryPrepare(state, action),
                    "Each action has one write-ahead slot.");
                shared = Reload(shared);
                Check(Commit(shared) && !Commit(shared), "A saved choice commits once.");
                shared = Reload(shared);
            }
            MarcusAureliusCampPracticeState finished = shared.MarcusAureliusCampPractice!;
            Check(finished.Closed && finished.ConfirmedActions.SequenceEqual(
                new[] { first, second, MarcusAureliusCampPracticeAction.Leave })
                && shared.EpictetusGuidePractice!.Closed
                && shared.ZenoRoutePayload.State!.Route!.Outcome == ZenoAssentOutcome.NoAssent,
                "Four practical paths close without altering predecessor outcomes.");
        }
        PhilosophyRunState declined = Fixture();
        Check(MarcusAureliusCampPracticeFlow.TryPrepare(declined.MarcusAureliusCampPractice!,
            MarcusAureliusCampPracticeAction.Decline), "Decline is legal.");
        declined = Reload(declined);
        Check(Commit(declined), "Decline commits.");
        Check(MarcusAureliusCampPracticeFlow.TryPrepare(declined.MarcusAureliusCampPractice!,
            MarcusAureliusCampPracticeAction.Leave), "Declined encounter can close.");
        declined = Reload(declined);
        Check(Commit(declined) && Reload(declined).MarcusAureliusCampPractice!.Closed,
            "Declining does not fabricate later camp work.");

        PhilosophyRunState corrupted = Fixture();
        corrupted.MarcusAureliusCampPractice!.ZenoOutcome = ZenoAssentOutcome.Keep;
        ExpectInvalid(corrupted);
        corrupted = Fixture();
        corrupted.EpictetusGuidePractice!.Closed = false;
        ExpectInvalid(corrupted);
        corrupted = Fixture();
        corrupted.MarcusAureliusCampPractice!.EpictetusInstanceId = "another";
        ExpectInvalid(corrupted);
        foreach (string field in new[]
        { "Version", "InstanceId", "RunId", "ZenoOutcome", "EpictetusInstanceId", "Stage", "PendingAction", "ConfirmedActions", "Closed" })
        {
            JsonNode node = JsonSerializer.SerializeToNode(Fixture().MarcusAureliusCampPractice)!;
            node.AsObject().Remove(field);
            Check(!JsonSerializer.Deserialize<MarcusAureliusCampPracticeState>(node)!.IsUsable(),
                "Missing save fields cannot become an invitation.");
        }
        ValidateLocalization();
        Console.WriteLine("Marcus Aurelius camp practice checks passed: five complete paths, saved decisions, strict predecessors, delayed combat gate and bilingual pages.");
    }

    private static PhilosophyRunState Fixture()
    {
        PhilosophyRunState shared = EpictetusGuidePracticeChecks.Fixture();
        foreach (EpictetusGuidePracticeAction action in new[]
        {
            EpictetusGuidePracticeAction.GuideWithReservation,
            EpictetusGuidePracticeAction.EscortCompanion,
            EpictetusGuidePracticeAction.Leave,
        })
        {
            Check(EpictetusGuidePracticeFlow.TryPrepare(shared.EpictetusGuidePractice!, action),
                "Fixture Epictetus action prepares.");
            shared = Reload(shared);
            Check(EpictetusGuidePracticeFlow.TryCommitPending(shared.EpictetusGuidePractice!,
                shared.ZenoRoutePayload.State!.Route!, shared.SenecaDailyReview!),
                "Fixture Epictetus action commits.");
            shared = Reload(shared);
        }
        shared.MarcusAureliusCampPractice = MarcusAureliusCampPracticeState.Create(
            "MARCUS_AURELIUS_CAMP_PRACTICE:42:1", shared.ZenoRoutePayload.State!.Route!,
            shared.EpictetusGuidePractice!);
        return Reload(shared);
    }

    private static PhilosophyRunState Reload(PhilosophyRunState shared) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));

    private static bool Commit(PhilosophyRunState shared) =>
        MarcusAureliusCampPracticeFlow.TryCommitPending(shared.MarcusAureliusCampPractice!,
            shared.ZenoRoutePayload.State!.Route!, shared.EpictetusGuidePractice!);

    private static void ExpectInvalid(PhilosophyRunState shared)
    {
        try { _ = PhilosophyRunStateCodec.Encode(shared); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Conflicting Marcus save must be rejected.");
    }

    private static void ValidateLocalization()
    {
        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument localization = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            Check(localization.RootElement.TryGetProperty("MARCUS_AURELIUS_CAMP_PRACTICE.title", out _),
                "The native title is bilingual.");
            foreach (MarcusAureliusCampPracticeStage stage in Enum.GetValues<MarcusAureliusCampPracticeStage>())
            {
                string page = $"MARCUS_AURELIUS_CAMP_PRACTICE.pages.{MarcusAureliusCampPracticeFlow.StageKey(stage)}";
                Check(localization.RootElement.TryGetProperty(page + ".description", out _),
                    "Every page is bilingual.");
                foreach (MarcusAureliusCampPracticeAction action in MarcusAureliusCampPracticeFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(localization.RootElement.TryGetProperty(
                        $"{page}.options.{MarcusAureliusCampPracticeFlow.ActionKey(action)}.{suffix}", out _),
                        "Every legal option is bilingual.");
            }
        }
    }
}
