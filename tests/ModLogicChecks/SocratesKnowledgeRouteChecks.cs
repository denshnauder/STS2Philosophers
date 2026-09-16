using STS2Philosophers;
using System.Text.Json.Nodes;

internal static class SocratesKnowledgeRouteChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static SocratesKnowledgeRouteRecord Resume(SocratesKnowledgeRouteRecord source)
    {
        string payload = SocratesKnowledgeRouteRecordCodec.Serialize(source);
        Check(SocratesKnowledgeRouteRecordCodec.Restore(payload, out var restored) == SocratesKnowledgeRestoreResult.Valid,
            "An explicit history must restore.");
        Check(SocratesKnowledgeRouteRecordCodec.Serialize(restored!) == payload, "A checkpoint must preserve every protocol fact.");
        return restored!;
    }

    private static void Reject(string payload)
    {
        Check(SocratesKnowledgeRouteRecordCodec.Restore(payload, out var record) == SocratesKnowledgeRestoreResult.Invalid
            && record is null, "Invalid new history must disable the experiment, never become a legacy run: " + payload);
    }

    public static void Run()
    {
        Check(SocratesKnowledgeRouteRecordCodec.Restore(null, out var absent) == SocratesKnowledgeRestoreResult.Absent
            && absent is null, "Only missing new data denotes an old run.");
        var initial = Resume(new());
        Check(!initial.HasExplicitStatement && initial.VisibleFacts.Count == 0 && initial.History.Count == 0,
            "An unused record contains no implicit belief or history.");
        Check(!initial.TryResolve(SocratesKnowledgeScene.FirstDay, SocratesKnowledgeChoice.Keep),
            "A statement cannot be submitted before its common facts are seen.");

        var firstCases = new[]
        {
            (SocratesKnowledgeChoice.Keep, SocratesKnowledgeStatement.SingleCorrectPerformance,
                new[] { SocratesKnowledgeChoice.Keep, SocratesKnowledgeChoice.RequireCurrentFacts, SocratesKnowledgeChoice.Withdraw, SocratesKnowledgeChoice.Leave }),
            (SocratesKnowledgeChoice.AddExplanation, SocratesKnowledgeStatement.DistinguishExplanation,
                new[] { SocratesKnowledgeChoice.LimitScope, SocratesKnowledgeChoice.RequireCurrentFacts, SocratesKnowledgeChoice.Withdraw, SocratesKnowledgeChoice.Leave }),
            (SocratesKnowledgeChoice.Withdraw, SocratesKnowledgeStatement.NoPositiveDefinition,
                new[] { SocratesKnowledgeChoice.ProposeCandidate, SocratesKnowledgeChoice.StayOpen, SocratesKnowledgeChoice.JudgeActionOnly, SocratesKnowledgeChoice.Leave }),
            (SocratesKnowledgeChoice.Leave, SocratesKnowledgeStatement.Unstated,
                new[] { SocratesKnowledgeChoice.ProposeCandidate, SocratesKnowledgeChoice.StayOpen, SocratesKnowledgeChoice.JudgeActionOnly, SocratesKnowledgeChoice.Leave }),
        };

        foreach (var (firstChoice, firstStatement, secondChoices) in firstCases)
        {
            var first = Resume(new());
            Check(first.TryShow(SocratesKnowledgeScene.FirstDay), "Show first common facts.");
            first = Resume(first);
            Check(first.VisibleFacts.Count == 3 && first.TryResolve(SocratesKnowledgeScene.FirstDay, firstChoice),
                "First choice follows the shown materials.");
            first = Resume(first);
            Check(first.Statement == firstStatement && first.History[0].Before == SocratesKnowledgeStatement.Unstated,
                "A first choice records exactly the authored statement, without back-inference.");
            Check(!first.TryResolve(SocratesKnowledgeScene.FirstDay, SocratesKnowledgeChoice.Withdraw)
                && !first.TryShow(SocratesKnowledgeScene.FirstDay), "A save cannot reopen mutually exclusive choices.");

            var second = Resume(first);
            Check(second.TryShow(SocratesKnowledgeScene.ChangedBridge) && second.VisibleFacts.Count == 6,
                "Changed-bridge materials include both days, not only the convenient facts.");
            second = Resume(second);
            Check(second.AvailableChoices(SocratesKnowledgeScene.ChangedBridge).ToHashSet().SetEquals(secondChoices),
                "Each prior statement gets its authored second-day options.");
            foreach (var choice in Enum.GetValues<SocratesKnowledgeChoice>())
            {
                var path = Resume(second);
                string before = SocratesKnowledgeRouteRecordCodec.Serialize(path);
                bool accepted = path.TryResolve(SocratesKnowledgeScene.ChangedBridge, choice);
                Check(accepted == secondChoices.Contains(choice), "Forbidden repair choices must not become valid by convenience.");
                if (!accepted)
                {
                    Check(SocratesKnowledgeRouteRecordCodec.Serialize(path) == before, "Rejected choices have no partial mutations.");
                    continue;
                }
                path = Resume(path);
                Check(path.History.Count == 2 && path.History[1].Before == firstStatement
                    && path.History[1].After == path.Statement && path.History[1].Facts.Count == 6,
                    "The history preserves origins and common facts.");
                Check(!path.TryResolve(SocratesKnowledgeScene.ChangedBridge, choice)
                    && !path.TryShow(SocratesKnowledgeScene.FirstDay), "No duplicate response or return to an earlier day.");
                if (choice == SocratesKnowledgeChoice.Leave)
                    Check(path.Statement == firstStatement && path.Question == first.Question,
                        "Leaving cannot become withdrawing or a new unresolved question.");
            }
        }

        var late = new SocratesKnowledgeRouteRecord();
        Check(late.TryShow(SocratesKnowledgeScene.ChangedBridge) && late.ActTwoLateEntry && !late.HasSeenFirstDay,
            "Missing the first opportunity is distinct from seeing and leaving it.");
        Check(late.TryResolve(SocratesKnowledgeScene.ChangedBridge, SocratesKnowledgeChoice.JudgeActionOnly),
            "A combined second-day page can explicitly choose action without knowledge.");
        late = Resume(late);
        Check(late.Statement == SocratesKnowledgeStatement.ActionWithoutKnowledge && late.History.Count == 1,
            "Late entry must not manufacture a first-day choice.");

        var seenOnly = new SocratesKnowledgeRouteRecord();
        seenOnly.TryShow(SocratesKnowledgeScene.FirstDay);
        seenOnly.TryShow(SocratesKnowledgeScene.ChangedBridge);
        seenOnly = Resume(seenOnly);
        Check(seenOnly.HasSeenFirstDay && seenOnly.ActTwoLateEntry && seenOnly.History.Count == 0
            && !seenOnly.HasExplicitStatement, "A closed page is seen-only, not a refusal or definition.");

        var readonlyHistory = new SocratesKnowledgeRouteRecord();
        readonlyHistory.TryShow(SocratesKnowledgeScene.FirstDay);
        readonlyHistory.TryResolve(SocratesKnowledgeScene.FirstDay, SocratesKnowledgeChoice.Keep);
        bool denied = false;
        try { ((IList<SocratesKnowledgeFact>)readonlyHistory.History[0].Facts)[0] = SocratesKnowledgeFact.BridgeChanged; }
        catch (NotSupportedException) { denied = true; }
        Check(denied, "Callers cannot rewrite the facts cited by a submitted decision.");

        string valid = SocratesKnowledgeRouteRecordCodec.Serialize(readonlyHistory);
        foreach (var bad in new[] { "", " ", "null", "[]", "{}", "broken",
            valid.Replace("\"Version\":1", "\"Version\":2"),
            valid.Replace("\"Version\":1", "\"Version\":1,\"Version\":1"),
            valid.Replace("SOCRATES", "DESCARTES"),
            valid.Replace("KNOWLEDGE_AND_DOUBT", "VIRTUE_AND_HAPPINESS"),
            valid.Replace("\"Choice\":0", "\"Choice\":99"),
            valid.Replace("\"Choice\":0", "\"Choice\":0,\"Choice\":0") }) Reject(bad);
        foreach (var key in new[] { "Version", "ThinkerId", "ProblemId", "SeenScenes", "Choices", "Statement", "Question", "ActTwoLateEntry" })
        {
            var missing = JsonNode.Parse(valid)!.AsObject(); missing.Remove(key); Reject(missing.ToJsonString());
        }
        foreach (var key in new[] { "Scene", "Choice" })
        {
            var missing = JsonNode.Parse(valid)!.AsObject(); missing["Choices"]![0]!.AsObject().Remove(key); Reject(missing.ToJsonString());
        }
        var forged = JsonNode.Parse(valid)!.AsObject();
        forged["Statement"] = (int)SocratesKnowledgeStatement.NoPositiveDefinition; Reject(forged.ToJsonString());
        forged = JsonNode.Parse(valid)!.AsObject(); forged["SeenScenes"] = new JsonArray(); Reject(forged.ToJsonString());
        forged = JsonNode.Parse(valid)!.AsObject(); forged["Choices"]!.AsArray().Add(forged["Choices"]![0]!.DeepClone()); Reject(forged.ToJsonString());
        forged = JsonNode.Parse(valid)!.AsObject(); forged["Choices"]![0]!["Scene"] = 1; Reject(forged.ToJsonString());
        forged = JsonNode.Parse(valid)!.AsObject(); forged["SeenScenes"]!.AsArray().Add(0); Reject(forged.ToJsonString());
        forged = JsonNode.Parse(valid)!.AsObject(); forged["ActTwoLateEntry"] = true; Reject(forged.ToJsonString());
        Console.WriteLine("Socrates knowledge route checks passed: explicit histories, late entry, and strict save replay.");
    }
}
