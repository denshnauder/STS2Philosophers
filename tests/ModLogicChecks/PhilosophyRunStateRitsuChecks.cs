using System.Text.Json;

namespace STS2Philosophers;

internal static class PhilosophyRunStateRitsuChecks
{
    public static void Run()
    {
        BoundPayloadTracksLiveState();
        SocratesVirtueFactsRoundTripThroughLivePayload();
        LoadedPayloadRestoresExistingCodec();
        SaveReaderFindsPersistedPayload();
        SaveReaderRejectsMalformedOrIncompleteData();

        Console.WriteLine("Philosophy run state RitsuLib migration checks passed.");
    }

    private static void SocratesVirtueFactsRoundTripThroughLivePayload()
    {
        PhilosophyRunState state = new();
        GeneratedCandidates candidates = new()
        {
            GenerationKey = WesternActOneCandidatePolicy.GenerationKey,
            CandidateIds = ["SOCRATES", "PLATO", "ARISTOTLE"],
        };
        Assert(WesternEntryPolicy.RecordObtained(
                state,
                candidates,
                SocratesVirtueUpstreamRecord.SocratesThinkerId,
                SocratesVirtueUpstreamRecord.VirtueProblemId,
                WesternEntryPolicy.RelicIdFor(SocratesVirtueUpstreamRecord.VirtueProblemId)),
            "The RitsuLib fixture must establish the Socrates virtue branch.");
        Assert(state.OpenSocratesVirtueOpportunity("RITSU_OPPORTUNITY", "RITSU_COMBAT", 0)
               == SocratesVirtueRecordWriteResult.Recorded
               && state.CommitSocratesVirtueAction(
                   "RITSU_OPPORTUNITY",
                   SocratesVirtueAction.Retreated,
                   retainedHitPointLoss: 9,
                   consumedPotionCount: 1) == SocratesVirtueRecordWriteResult.Recorded,
            "The live state must contain one committed upstream action before saving.");

        PhilosophyRunStateRitsuPayload payload = PhilosophyRunStateRitsuPayload.Bind(state);
        PhilosophyRunStateRitsuPayload loaded = new()
        {
            SchemaVersion = payload.SchemaVersion,
            EncodedState = payload.EncodedState,
        };
        Assert(loaded.TryDecode(out PhilosophyRunState? restored)
               && restored is not null
               && restored.TryGetSocratesVirtueMaterial(out SocratesVirtueMaterial? material)
               && material?.Action == SocratesVirtueAction.Retreated
               && material.RetainedHitPointLoss == 9
               && material.ConsumedPotionCount == 1,
            "The authoritative RitsuLib run slot must preserve the committed Socrates virtue facts.");
    }

    private static void BoundPayloadTracksLiveState()
    {
        PhilosophyRunState state = new();
        PhilosophyRunStateRitsuPayload payload = PhilosophyRunStateRitsuPayload.Bind(state);
        Assert(payload.EncodedState.Length == 0,
            "An empty live run state should remain equal to the RitsuLib slot default.");

        state.RecordCurrentDoctrine("ZENO_OF_ELEA", "DICHOTOMY", ["WESTERN"]);
        string encoded = payload.EncodedState;
        PhilosophyRunState restored = PhilosophyRunStateCodec.Decode(encoded);
        Assert(restored.CurrentDoctrine?.ThinkerId == "ZENO_OF_ELEA",
            "The RitsuLib payload must encode the live shared state at save time.");
    }

    private static void LoadedPayloadRestoresExistingCodec()
    {
        PhilosophyRunState source = new();
        source.RecordCurrentDoctrine("ZENO_OF_ELEA", "ARROW", []);
        PhilosophyRunStateRitsuPayload payload = new()
        {
            SchemaVersion = PhilosophyRunStateRitsuPayload.CurrentSchemaVersion,
            EncodedState = PhilosophyRunStateCodec.Encode(source),
        };

        Assert(payload.TryDecode(out PhilosophyRunState? restored) &&
               restored?.CurrentDoctrine?.DoctrineId == "ARROW",
            "A loaded RitsuLib payload must restore through the existing shared-state codec.");

        payload.EncodedState = "NOT_HEX";
        Assert(!payload.TryDecode(out _),
            "Malformed RitsuLib payloads must be isolated instead of replacing live state.");
    }

    private static void SaveReaderFindsPersistedPayload()
    {
        PhilosophyRunState source = new();
        source.RecordCurrentDoctrine("ZENO_OF_ELEA", "STADIUM", []);
        string encoded = PhilosophyRunStateCodec.Encode(source);
        string json = JsonSerializer.Serialize(new
        {
            ordinary = true,
            _ritsulib = new
            {
                version = 1,
                run_saved_data = new Dictionary<string, object>
                {
                    ["sts2philosophers"] = new Dictionary<string, object>
                    {
                        ["philosophy_run_state"] = new
                        {
                            schema = 1,
                            kind = "run",
                            data = new
                            {
                                SchemaVersion = 1,
                                EncodedState = encoded,
                            },
                        },
                    },
                },
            },
        });

        PhilosophyRunStateRitsuSaveReadResult result = PhilosophyRunStateRitsuSaveReader.Read(json);
        Assert(result.Classification == PhilosophyRunStateRitsuSaveClassification.Current &&
               string.Equals(result.EncodedState, encoded, StringComparison.Ordinal),
            "The disk reader must locate the run slot without depending on mod-id casing.");
    }

    private static void SaveReaderRejectsMalformedOrIncompleteData()
    {
        Assert(PhilosophyRunStateRitsuSaveReader.Read("{}").Classification ==
               PhilosophyRunStateRitsuSaveClassification.Missing,
            "A save without the run slot should remain a legacy fallback candidate.");
        Assert(PhilosophyRunStateRitsuSaveReader.Read("not json").Classification ==
               PhilosophyRunStateRitsuSaveClassification.Invalid,
            "Malformed JSON should not be accepted as persisted state.");
        Assert(PhilosophyRunStateRitsuSaveReader.Read(
                   "{\"_ritsulib\":{\"run_saved_data\":{\"STS2Philosophers\":{\"PHILOSOPHY_RUN_STATE\":{\"schema\":1,\"kind\":\"run\",\"data\":{}}}}}}")
               .Classification == PhilosophyRunStateRitsuSaveClassification.Invalid,
            "A present slot without encoded state should block a false legacy success.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
