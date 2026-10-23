using System.Text.Json;
using System.Text.Json.Nodes;
using STS2Philosophers;

internal static class ZenoCivicSealUpgradeChecks
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static PhilosophyRunState Reload(PhilosophyRunState state) =>
        PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));
    private static PhilosophyRunState Start(WesternRouteGraph graph)
    {
        var journey = graph.Start(ZenoCivicChallengeState.SourceNodeId);
        Check(graph.TryReserveQuestion(journey, 1), "The actual civic encounter consumes Q1 once.");
        return Reload(new() { WesternJourney = journey, ZenoCivicChallenge = ZenoCivicChallengeState.Create("ZENO:GRANT:1") });
    }
    private static PhilosophyRunState Commit(PhilosophyRunState run, WesternRouteGraph graph, ZenoCivicAction action)
    {
        Check(ZenoCivicChallengeFlow.TryPrepare(run.ZenoCivicChallenge!, action), $"Prepare civic choice {action}.");
        run = Reload(run);
        Check(ZenoCivicChallengeFlow.TryCommitPending(run.ZenoCivicChallenge!, run.WesternJourney!, graph)
            && !ZenoCivicChallengeFlow.TryCommitPending(run.ZenoCivicChallenge!, run.WesternJourney!, graph),
            "A pending civic operation survives shared serialization and commits once.");
        return Reload(run);
    }
    private static ZenoCivicChallengeState Legacy(ZenoCivicChallengeState state)
    {
        JsonObject json = JsonSerializer.SerializeToNode(state)!.AsObject();
        json.Remove(nameof(state.SealUpgradeVersion)); json.Remove(nameof(state.SealUpgradeGranted));
        return json.Deserialize<ZenoCivicChallengeState>()!;
    }

    public static void Run()
    {
        var graph = WesternRouteGraph.LoadEmbedded();
        ZenoCivicAction[][] paths =
        [
            [ZenoCivicAction.DeclineInvitation],
            [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.DemandPublicRule, ZenoCivicAction.PublishPublicObjection],
            [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.ProposeConditionalException, ZenoCivicAction.PublishConditionalObjection],
            [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.DemandPublicRule, ZenoCivicAction.WithdrawObjection],
            [ZenoCivicAction.AcceptInvitation, ZenoCivicAction.ProposeConditionalException, ZenoCivicAction.WithdrawObjection],
        ];
        foreach (ZenoCivicAction[] path in paths)
        {
            var run = Start(graph);
            foreach (var action in path)
            {
                run = Commit(run, graph, action);
                Check(run.ZenoCivicChallenge!.SealUpgradeGranted == false && !run.ZenoCivicChallenge.Closed,
                    "Publishing a result alone does not claim that the real seal was delivered.");
            }
            bool grants = run.ZenoCivicChallenge!.EdgeCommitted;
            Check(!ZenoCivicChallengeFlow.DescriptionKey(run.ZenoCivicChallenge).EndsWith("_CONFIRMED", StringComparison.Ordinal),
                "The page before final Leave advertises a pending grant, never a completed delivery.");
            run = Commit(run, graph, ZenoCivicAction.Leave);
            var state = run.ZenoCivicChallenge!;
            Check(state.Closed && state.SealUpgradeGranted == grants && state.SealUpgradeVersion == 1
                && (state.SealUpgradeBranch != ZenoCivicUpgradeBranch.None) == grants,
                "Only final Leave on a published objection records the exact closing grant branch.");
            Check(graph.GetEligibleEdges(run.WesternJourney!, "Fixed").Any(edge => edge.EdgeId == "WESTERN_EDGE_020") == grants
                && run.WesternJourney!.LastFixedAct == 1 && run.WesternJourney.ResolvedQuestionActs.SetEquals([1]),
                "The immediate seal upgrade preserves real 019->020 eligibility and spends no additional opportunity.");

            ZenoCivicChallengeState oldClosed = Legacy(state);
            Check(oldClosed.IsUsable() && oldClosed.Closed && !oldClosed.TryEnableSealUpgrade()
                && oldClosed.SealUpgradeVersion is null && oldClosed.SealUpgradeGranted is null,
                "A completed pre-feature encounter cannot be upgraded by replaying history.");
            if (grants)
                Check(ZenoCivicChallengeFlow.DescriptionKey(oldClosed).EndsWith("_LEGACY", StringComparison.Ordinal)
                    && ZenoCivicChallengeFlow.DescriptionKey(state).EndsWith("_CONFIRMED", StringComparison.Ordinal),
                    "Old completed and newly delivered objections have different truthful player-facing pages.");
        }

        var unfinished = Start(graph);
        foreach (var action in paths[1]) unfinished = Commit(unfinished, graph, action);
        unfinished.ZenoCivicChallenge = Legacy(unfinished.ZenoCivicChallenge!);
        unfinished = Reload(unfinished);
        Check(unfinished.ZenoCivicChallenge!.IsUsable() && !unfinished.ZenoCivicChallenge.Closed
            && unfinished.ZenoCivicChallenge.TryEnableSealUpgrade(),
            "An old unfinished published result explicitly opts into the current closing grant protocol.");
        unfinished = Reload(unfinished);
        Check(unfinished.ZenoCivicChallenge!.SealUpgradeVersion == 1 && unfinished.ZenoCivicChallenge.SealUpgradeGranted == false,
            "The explicit unfinished migration is saved before any reward or closure.");
        unfinished = Commit(unfinished, graph, ZenoCivicAction.Leave);
        Check(unfinished.ZenoCivicChallenge!.SealUpgradeGranted == true,
            "An explicitly migrated unfinished result can receive its reward when it actually closes.");
        foreach (string missing in new[] { "SealUpgradeVersion", "SealUpgradeGranted" })
        {
            JsonObject json = JsonSerializer.SerializeToNode(unfinished.ZenoCivicChallenge)!.AsObject(); json.Remove(missing);
            Check(!json.Deserialize<ZenoCivicChallengeState>()!.IsUsable(), "Half of a new grant protocol cannot masquerade as a legacy record.");
        }
        unfinished.ZenoCivicChallenge.SealUpgradeVersion = 99;
        Check(!unfinished.ZenoCivicChallenge.IsUsable(), "An unknown civic grant version is not migrated or rewarded.");
        Receipts(); Localization();
        Console.WriteLine("Zeno civic seal checks passed: five saved closing paths, explicit unfinished compatibility, no closed-legacy backfill, unchanged 019/020 and strict instance/branch/combat/turn receipts; real native grant/save and card effects are checked separately.");
    }

    private static void Receipts()
    {
        foreach (var branch in new[] { ZenoCivicUpgradeBranch.Public, ZenoCivicUpgradeBranch.Conditional })
        {
            var state = ZenoCivicSealUpgradeState.Create("ZENO:RECEIPT:1", branch);
            Check(state.IsUsable() && state.Matches("ZENO:RECEIPT:1", branch)
                && !state.Matches("ZENO:OTHER:1", branch)
                && !state.Matches("ZENO:RECEIPT:1", branch == ZenoCivicUpgradeBranch.Public
                    ? ZenoCivicUpgradeBranch.Conditional : ZenoCivicUpgradeBranch.Public),
                "A grant is bound to exactly one event instance and mutually exclusive branch.");
            Check(state.TryClaim("0:4:12", 1) && !state.TryClaim("0:4:12", 1), "The same combat turn cannot pay twice.");
            Check(ZenoCivicSealUpgradeState.TryRestore(state.Encode(), out var restored)
                && restored is not null && !restored.TryClaim("0:4:12", 1)
                && restored.TryClaim("0:4:12", 2) && !restored.TryClaim("0:4:12", 1)
                && restored.TryClaim("0:5:13", 1), "Native payload reload preserves spent turns, a later turn and a distinct combat.");
            foreach (string key in new[] { "", "0:4", "0:4:12:1", "0:-4:12", "foreign" })
                Check(!state.TryClaim(key, 3), "An invalid combat identity cannot grant a reward.");
            Check(!state.TryClaim("0:4:12", 0), "The turn loop must have actually started.");
            foreach (string field in new[] { "Version", "EventInstanceId", "Branch", "ClaimedTurns" })
            {
                JsonObject json = JsonSerializer.SerializeToNode(state)!.AsObject(); json.Remove(field);
                Check(!ZenoCivicSealUpgradeState.TryRestore(json.ToJsonString(), out _), "Missing native grant fields remain invalid.");
            }
            foreach (string bad in new[] { "broken", "{}", "null", "{\"Version\":99}" })
                Check(!ZenoCivicSealUpgradeState.TryRestore(bad, out _), "Unknown or malformed native grant payloads cannot be reset into rewards.");
        }
        Check(ZenoCivicSealUpgradeState.TryRestore(string.Empty, out var absent) && absent is null,
            "A genuinely absent old seal payload stays unimproved.");
    }

    private static void Localization()
    {
        foreach (string language in new[] { "eng", "zhs" })
        {
            using var events = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/events.json"));
            foreach (string branch in new[] { "PUBLIC", "CONDITIONAL" })
            foreach (string suffix in new[] { "CONFIRMED", "LEGACY" })
                Check(events.RootElement.TryGetProperty($"ZENO_CIVIC_CHALLENGE.pages.RESULT_{branch}_OBJECTION_{suffix}.description", out var value)
                    && !string.IsNullOrWhiteSpace(value.GetString()), "New and old closing results have truthful bilingual pages.");
            using var relics = JsonDocument.Parse(File.ReadAllText($"content/STS2Philosophers/localization/{language}/relics.json"));
            foreach (string branch in new[] { "public", "conditional", "unavailable" })
            foreach (string suffix in new[] { "title", "description" })
                Check(relics.RootElement.TryGetProperty($"PLATO_CIVIC_SEAL.zeno.{branch}.{suffix}", out var value)
                    && !string.IsNullOrWhiteSpace(value.GetString()), "The actual granted or isolated seal upgrade has bilingual hover text.");
        }
    }
}
