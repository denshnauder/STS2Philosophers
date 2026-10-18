using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class AnaximenesBreathInvitationChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        foreach (var (choice, hasRelic) in new[]
        {
            (AnaximenesBreathInvitationAction.Accept, true),
            (AnaximenesBreathInvitationAction.Decline, false),
        })
        {
            PhilosophyRunState run = new()
            {
                WesternJourney = WesternRouteGraph.LoadEmbedded().Start(AnaximenesBreathInvitationState.RequiredNodeId),
                AnaximenesBreathInvitation = AnaximenesBreathInvitationState.Create("BREATH_INSTANCE"),
            };
            AnaximenesBreathInvitationState state = run.AnaximenesBreathInvitation;
            Check(AnaximenesBreathInvitationFlow.TryPrepare(state, choice)
                && !AnaximenesBreathInvitationFlow.TryPrepare(state, choice),
                "One saved choice must be pending at a time.");
            run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
            state = run.AnaximenesBreathInvitation!;
            Check(!AnaximenesBreathInvitationFlow.TryCommitPending(state, !hasRelic)
                && state.PendingAction == choice,
                "A mismatch between the choice and the native relic cannot commit.");
            Check(AnaximenesBreathInvitationFlow.TryCommitPending(state, hasRelic)
                && !AnaximenesBreathInvitationFlow.TryCommitPending(state, hasRelic),
                "The saved choice commits exactly once.");
            Check(AnaximenesBreathInvitationFlow.TryPrepare(state, AnaximenesBreathInvitationAction.Leave),
                "The result has a native leave action.");
            run = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(run));
            state = run.AnaximenesBreathInvitation!;
            Check(AnaximenesBreathInvitationFlow.TryCommitPending(state, hasRelic)
                && state.Closed && state.IsUsable()
                && run.WesternJourney!.CurrentNodeId == AnaximenesBreathInvitationState.RequiredNodeId,
                "Both outcomes close once without replacing the route.");
            Check(!AnaximenesBreathInvitationFlow.TryPrepare(state, choice),
                "A closed invitation cannot reopen.");
        }

        Check(PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(new())).AnaximenesBreathInvitation is null,
            "Older saves must not invent a practice invitation.");
        foreach (string field in new[] { "Version", "Stage", "PendingAction" })
        {
            JsonNode json = JsonSerializer.SerializeToNode(AnaximenesBreathInvitationState.Create("INVALID"))!;
            json[field] = 99;
            Check(!JsonSerializer.Deserialize<AnaximenesBreathInvitationState>(json)!.IsUsable(),
                "Unknown versions and actions must not become an invitation.");
        }

        var eligible = new AnaximenesBreathInvitationTriggerContext(true, true, true, 2, 1,
            AnaximenesBreathInvitationState.RequiredNodeId, true, false, false, false, false, true);
        Check(AnaximenesBreathInvitationTriggerPolicy.ShouldInsert(eligible),
            "The third-act Lyre route may receive one invitation.");
        foreach (var denied in new[]
        {
            eligible with { CurrentActIndex = 1 }, eligible with { CompletedCombatCount = 0 },
            eligible with { CurrentNodeId = "OTHER" }, eligible with { HasLyre = false },
            eligible with { HasPracticeRelic = true }, eligible with { IsSingleplayer = false },
            eligible with { AtSafeMapBoundary = false }, eligible with { ModelAvailable = false },
            eligible with { HasSavedInstance = true },
            eligible with { HasSavedInstance = true, SavedInstanceUsable = true, SavedInstanceClosed = true },
        }) Check(!AnaximenesBreathInvitationTriggerPolicy.ShouldInsert(denied),
            "Missing eligibility and closed or damaged saves cannot reoffer the practice.");
        Check(AnaximenesBreathInvitationTriggerPolicy.ShouldInsert(eligible with
            { HasSavedInstance = true, SavedInstanceUsable = true, HasPracticeRelic = true }),
            "A saved open invitation can resume after an already granted relic without duplicating it.");

        AnaximenesBreathPracticeState practice = default;
        Check(!practice.TryRedeem(1) && !practice.TryBank(1, 0),
            "The first turn has no prior energy and zero remaining energy cannot bank.");
        Check(practice.TryBank(1, 1) && !practice.TryBank(1, 1)
            && practice.TryRedeem(2) && !practice.TryRedeem(2),
            "One unused energy becomes one next-turn reward exactly once.");
        Check(!practice.TryRedeem(3) && practice.TryBank(2, 2)
            && practice.TryRedeem(3),
            "An unbanked turn gives no reward while each new bank carries only one turn.");
        practice.Reset();
        Check(!practice.TryRedeem(4) && practice.BankedTurn == 0,
            "Combat completion loses any unredeemed reserve.");

        foreach (string language in new[] { "zhs", "eng" })
        {
            using JsonDocument events = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (AnaximenesBreathInvitationStage stage in Enum.GetValues<AnaximenesBreathInvitationStage>())
            {
                string page = $"ANAXIMENES_BREATH_INVITATION.pages.{AnaximenesBreathInvitationFlow.StageKey(stage)}";
                Check(events.RootElement.TryGetProperty(page + ".description", out _), "Each event page needs text.");
                foreach (AnaximenesBreathInvitationAction action in AnaximenesBreathInvitationFlow.Options(stage))
                foreach (string suffix in new[] { "title", "description" })
                    Check(events.RootElement.TryGetProperty(
                        $"{page}.options.{AnaximenesBreathInvitationFlow.ActionKey(action)}.{suffix}", out _),
                        "Each event action needs text.");
            }
            using JsonDocument relics = JsonDocument.Parse(File.ReadAllText(
                $"content/STS2Philosophers/localization/{language}/relics.json"));
            foreach (string suffix in new[] { "title", "description", "flavor" })
                Check(relics.RootElement.TryGetProperty($"ANAXIMENES_BREATH.{suffix}", out _),
                    "The practice relic needs text.");
        }
        Console.WriteLine("Anaximenes practice checks passed: two event paths, saved pending choices, old saves, safe invitation guards, turn bank and bilingual text.");
    }
}
