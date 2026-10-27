using STS2Philosophers;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class DiogenesStaySwitchEntryPolicyChecks
{
    private static readonly DiogenesStaySwitchEntryContext Context = new(2, 1, true, false, null);

    internal static void Run()
    {
        RealActionsRemainDistinct();
        MissingAndUnfinishedRecordsStayNoMaterial();
        WrongRoutesAndUnsafeContextsCannotEnter();
        FrozenMaterialSurvivesRepeatedRestore();
        InvalidAndIncompletePayloadsCannotRestore();
        NativeQuestionRecordsKeepTheirOwnVersion();
        PracticeObservationsRemainBoundedAndFrozen();
        Console.WriteLine("Diogenes entry policy checks passed: real branch eligibility, frozen facts and strict restore.");
    }

    private static void PracticeObservationsRemainBoundedAndFrozen()
    {
        DiogenesPracticeLedger ledger = new();
        Assert(ledger.TryRecord(0, 11, 3, 0) && ledger.TryRecord(0, 11, 3, 0),
            "Repeated callback for one observed choice must be idempotent.");
        Assert(!ledger.TryRecord(0, 11, 3, 1) && ledger.TryRecord(1, 12, 2, 0)
            && ledger.TryRecord(1, 13, 2, 1),
            "Conflicting replay must fail while distinct battles retain their own decisions.");
        PhilosophyRunState shared = Eligible();
        shared.DiogenesPractice = ledger;
        PhilosophyRunState restoredShared = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(shared));
        Assert(restoredShared.DiogenesPractice?.Observations.SequenceEqual(ledger.Observations) == true,
            "Real opening choices must survive the shared run-state save round trip.");
        Assert(DiogenesStaySwitchEntryPolicy.TryCreate(shared, 42, Context, out var entry)
            && entry?.Practice == new DiogenesPracticeSummary(3, 2, 1),
            "The third-act entry must freeze two declined aids and one accepted aid.");
        ledger.TryRecord(1, 14, 1, 1);
        Assert(entry!.Practice == new DiogenesPracticeSummary(3, 2, 1)
            && JsonSerializer.Deserialize<DiogenesStaySwitchEntryRecord>(JsonSerializer.Serialize(entry))?.Practice
                == entry.Practice,
            "Later mutations and save round trips must preserve the frozen observation summary.");
        ledger.Observations.Add(new DiogenesPracticeObservation(-1, 15, 1, 0));
        Assert(!DiogenesStaySwitchEntryPolicy.TryCreate(shared, 43, Context, out _),
            "Malformed observations must not become new entry evidence.");
    }

    private static void RealActionsRemainDistinct()
    {
        foreach (SocratesVirtueAction action in new[] { SocratesVirtueAction.Continued, SocratesVirtueAction.Retreated })
        {
            PhilosophyRunState shared = Eligible();
            shared.SocratesVirtueUpstream = Committed(action);
            Assert(DiogenesStaySwitchEntryPolicy.TryCreate(shared, 42, Context, out var entry) &&
                   entry is not null, "A real completed action on the live virtue branch should enter.");
            Assert(entry!.Facts?.Action == action && entry.Facts.RetainedHitPointLoss == 7 &&
                   entry.Facts.ConsumedPotionCount == 1 && !entry.Material.IsNoMaterial,
                "The source facts must retain the actual action and costs.");
            Assert(entry.Material.ActionFactId == (action == SocratesVirtueAction.Continued
                       ? DiogenesStaySwitchEntryPolicy.ContinuedFactId
                       : DiogenesStaySwitchEntryPolicy.RetreatedFactId), "Actions must use distinct fixed IDs.");
            Assert(entry.Material.PublicHistoryIds.Count == 0 && entry.Material.CurrentStatementId is null &&
                   entry.Material.NarrowStatementId is null && entry.Material.LaterOutcomeId is null,
                "No unproduced public history or statements may be invented.");
            ZenoRouteFeatureState initial = DiogenesStaySwitchEntryPolicy.CreateInitialState(entry);
            Assert(initial.Route?.Stage == ZenoRouteStage.Unresolved && initial.Route.RunId == entry.RunId &&
                   initial.Route.Material is null && initial.PendingOperation is null,
                "Entry must start unresolved without preselecting Switch.");
            Assert(ZenoRouteStateService.PrepareSwitch(initial, 0, entry.Material,
                       ZenoRouteValidationCatalogs.SaveRestore).IsAccepted,
                "Both production fact snapshots must be supported by the save restore catalog.");
            Assert(shared.WesternJourney!.CompletedEdgeIds.Count == 0 &&
                   shared.WesternJourney.CurrentNodeId == SocratesVirtueUpstreamRecord.VirtueRouteNodeId,
                "Entry must not fabricate a route edge or a second-act encounter.");
        }
    }

    private static void MissingAndUnfinishedRecordsStayNoMaterial()
    {
        foreach (SocratesVirtueUpstreamRecord? upstream in new SocratesVirtueUpstreamRecord?[]
                 { null, SocratesVirtueUpstreamRecord.Open("opportunity", "combat", 1),
                     Committed(SocratesVirtueAction.Continued) })
        {
            PhilosophyRunState shared = Eligible();
            shared.SocratesVirtueUpstream = upstream;
            if (upstream?.Action == SocratesVirtueAction.Continued)
            {
                upstream.SourceVersion++;
            }
            Assert(DiogenesStaySwitchEntryPolicy.TryCreate(shared, 42, Context, out var entry) &&
                   entry!.Facts is null && entry.Material.IsNoMaterial && entry.Material.ActionFactId is null,
                "Missing, unfinished or unsupported source records must remain explicitly NoMaterial.");
        }
    }

    private static void WrongRoutesAndUnsafeContextsCannotEnter()
    {
        foreach (var context in new[] { Context with { ActIndex = 1 }, Context with { ActIndex = 3 },
                     Context with { PlayerCount = 2 }, Context with { IsMapRoom = false },
                     Context with { IsRestoringRoomStackBase = true }, Context with { SourceRoomId = -1 } })
        {
            Assert(!DiogenesStaySwitchEntryPolicy.TryCreate(Eligible(), 42, context, out _),
                "Only the live single-player third-act map entry is eligible.");
        }
        PhilosophyRunState wrongThinker = Eligible();
        wrongThinker.CurrentDoctrine!.ThinkerId = "PLATO";
        PhilosophyRunState wrongDoctrine = Eligible();
        wrongDoctrine.CurrentDoctrine!.DoctrineId = "SOCRATES_QUESTION_CUP";
        PhilosophyRunState wrongNode = Eligible();
        wrongNode.WesternJourney!.CurrentNodeId = "PLATO__VIRTUE_AND_HAPPINESS__CORE";
        PhilosophyRunState unknown = Eligible();
        unknown.RestoreZenoRoutePayload(new(ZenoRoutePayloadClassification.UnknownNewer, null, "opaque"), "opaque");
        PhilosophyRunState isolated = Eligible();
        isolated.PreserveSaveMarkerEntries(["preserved"]);
        foreach (var shared in new[] { wrongThinker, wrongDoctrine, wrongNode, unknown, isolated })
        {
            Assert(!DiogenesStaySwitchEntryPolicy.TryCreate(shared, 42, Context, out _),
                "Wrong branch or isolated route data must never be overwritten by an entry.");
        }
        PhilosophyRunState existing = Eligible();
        DiogenesStaySwitchEntryPolicy.TryCreate(existing, 42, Context, out var entry);
        existing.SetCurrentZenoRouteState(DiogenesStaySwitchEntryPolicy.CreateInitialState(entry!));
        Assert(!DiogenesStaySwitchEntryPolicy.TryCreate(existing, 42, Context, out _),
            "An existing unresolved route cannot be initialized a second time.");
    }

    private static void FrozenMaterialSurvivesRepeatedRestore()
    {
        PhilosophyRunState shared = Eligible();
        shared.SocratesVirtueUpstream = Committed(SocratesVirtueAction.Retreated);
        DiogenesStaySwitchEntryPolicy.TryCreate(shared, ulong.MaxValue, Context with { SourceRoomId = 17 }, out var entry);
        string json = JsonSerializer.Serialize(entry);
        shared.SocratesVirtueUpstream.RetainedHitPointLoss = 100;
        shared.SocratesVirtueUpstream.ConsumedPotionCount = 20;
        for (int i = 0; i < 3; i++)
        {
            var restored = JsonSerializer.Deserialize<DiogenesStaySwitchEntryRecord>(json);
            Assert(DiogenesStaySwitchEntryPolicy.IsValid(restored) && restored!.Facts!.RetainedHitPointLoss == 7 &&
                   restored.Facts.ConsumedPotionCount == 1 && restored.Material.Digest == entry!.Material.Digest &&
                   restored.RunId == entry.RunId && restored.SourceRoomId == 17,
                "Restore must preserve the frozen payload, independently of later source mutation.");
            json = JsonSerializer.Serialize(restored);
        }
        var closed = entry! with { Closed = true };
        Assert(JsonSerializer.Deserialize<DiogenesStaySwitchEntryRecord>(JsonSerializer.Serialize(closed))!.Closed,
            "The confirmed close marker must survive serialization.");
        Assert(DiogenesStaySwitchEntryPolicy.IsValid(closed), "A valid closed record remains recoverable.");
    }

    private static void InvalidAndIncompletePayloadsCannotRestore()
    {
        DiogenesStaySwitchEntryPolicy.TryCreate(Eligible(), 42, Context, out var entry);
        JsonObject original = JsonNode.Parse(JsonSerializer.Serialize(entry))!.AsObject();
        foreach (string field in original.Select(pair => pair.Key).Where(key => key is not ("Practice" or "NativeNode")).ToArray())
        {
            JsonObject incomplete = original.DeepClone().AsObject();
            incomplete.Remove(field);
            AssertRejects(incomplete.ToJsonString(), "Every persisted entry field must be present, including Closed.");
        }
        JsonObject legacy = original.DeepClone().AsObject();
        legacy.Remove("Practice");
        legacy.Remove("NativeNode");
        Assert(JsonSerializer.Deserialize<DiogenesStaySwitchEntryRecord>(legacy.ToJsonString())?.Practice is null,
            "A pre-observation entry must restore without invented practice evidence.");
        JsonObject unknown = original.DeepClone().AsObject();
        unknown["Version"] = 3;
        AssertRejects(unknown.ToJsonString(), "An unknown entry version must remain isolated.");
        JsonObject changed = original.DeepClone().AsObject();
        changed["Material"]!["Digest"] = "changed";
        AssertRejects(changed.ToJsonString(), "A changed frozen material digest must be rejected.");
        PhilosophyRunState withFacts = Eligible();
        withFacts.SocratesVirtueUpstream = Committed(SocratesVirtueAction.Continued);
        DiogenesStaySwitchEntryPolicy.TryCreate(withFacts, 42, Context, out var factsEntry);
        JsonObject incompleteFacts = JsonNode.Parse(JsonSerializer.Serialize(factsEntry))!.AsObject();
        incompleteFacts["Facts"]!.AsObject().Remove("RetainedHitPointLoss");
        AssertRejects(incompleteFacts.ToJsonString(), "Omitted costs must not silently restore as zero.");
        Assert(!DiogenesStaySwitchEntryPolicy.IsValid(entry! with { RunId = "unrelated" }),
            "An entry must use its own deterministic run identity.");
    }

    private static void NativeQuestionRecordsKeepTheirOwnVersion()
    {
        PhilosophyRunState shared = Eligible();
        shared.SocratesVirtueUpstream = Committed(SocratesVirtueAction.Retreated);
        Assert(DiogenesStaySwitchEntryPolicy.TryCreateNative(shared, 42, new(2, 3, 1), out var entry)
            && entry is { Version: 2, SourceRoomId: null, NativeNode: { ActIndex: 2, Row: 3, Column: 1 } },
            "The native question record keeps its actual node and distinct version, without a legacy map ID.");
        string json = JsonSerializer.Serialize(entry);
        for (int i = 0; i < 3; i++)
        {
            var restored = JsonSerializer.Deserialize<DiogenesStaySwitchEntryRecord>(json)!;
            Assert(DiogenesStaySwitchEntryPolicy.IsValid(restored) && restored.NativeNode == entry!.NativeNode
                && restored.Facts!.RetainedHitPointLoss == 7 && restored.Material.Digest == entry.Material.Digest,
                "Native record restore preserves its original node, actual costs and frozen material.");
            json = JsonSerializer.Serialize(restored);
        }
        JsonObject original = JsonNode.Parse(json)!.AsObject();
        foreach (string field in new[] { "ActIndex", "Row", "Column" })
        {
            JsonObject incomplete = original.DeepClone().AsObject();
            incomplete["NativeNode"]!.AsObject().Remove(field);
            AssertRejects(incomplete.ToJsonString(), "All three native node fields must be explicit, not default coordinates.");
        }
        JsonObject noNode = original.DeepClone().AsObject(); noNode.Remove("NativeNode");
        AssertRejects(noNode.ToJsonString(), "A version-two record cannot fall back to a legacy entry when its node is missing.");
        JsonObject mixed = original.DeepClone().AsObject(); mixed["SourceRoomId"] = 17;
        AssertRejects(mixed.ToJsonString(), "Native node and legacy map identity cannot be mixed.");
        Assert(!DiogenesStaySwitchEntryPolicy.TryCreateNative(Eligible(), 42, new(1, 3, 1), out _)
            && !DiogenesStaySwitchEntryPolicy.TryCreateNative(Eligible(), 42, new(2, -1, 1), out _),
            "A non-third-act or invalid coordinate cannot create a native invitation.");
    }

    private static PhilosophyRunState Eligible() => new()
    {
        CurrentDoctrine = new() { ThinkerId = SocratesVirtueUpstreamRecord.SocratesThinkerId,
            DoctrineId = WesternEntryPolicy.RelicIdFor(SocratesVirtueUpstreamRecord.VirtueProblemId) },
        WesternJourney = new() { CurrentNodeId = SocratesVirtueUpstreamRecord.VirtueRouteNodeId, LastFixedAct = 1 },
    };

    private static SocratesVirtueUpstreamRecord Committed(SocratesVirtueAction action)
    {
        var record = SocratesVirtueUpstreamRecord.Open("opportunity", "combat", 1);
        record.Commit("opportunity", action, 7, 1);
        return record;
    }

    private static void AssertRejects(string json, string message)
    {
        try { JsonSerializer.Deserialize<DiogenesStaySwitchEntryRecord>(json); }
        catch (JsonException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
