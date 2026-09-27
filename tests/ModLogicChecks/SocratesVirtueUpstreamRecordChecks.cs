using System.Text;
using STS2Philosophers;

internal static class SocratesVirtueUpstreamRecordChecks
{
    private const string OpportunityId = "SOCRATES_VIRTUE_OPPORTUNITY_001";
    private const string CombatId = "COMBAT_001";

    public static void Run()
    {
        MissingOrUncommittedFactsExposeNoMaterial();
        ContinuedFactsRoundTripWithoutInventingAResult();
        RetreatedFactsRoundTripWithActualCosts();
        DuplicateAndConflictingCallbacksCannotRewriteFacts();
        InvalidOrIncompleteSavedFactsAreRejected();

        Console.WriteLine("Socrates virtue upstream checks passed: route gating, immutable action facts and shared-save round trips.");
    }

    private static void MissingOrUncommittedFactsExposeNoMaterial()
    {
        PhilosophyRunState oldSave = PhilosophyRunStateCodec.Decode(ToHex("{}"));
        Assert(!oldSave.TryGetSocratesVirtueMaterial(out _),
            "An old save must expose no Socrates virtue material.");

        PhilosophyRunState wrongRoute = new();
        wrongRoute.RecordCurrentDoctrine("SOCRATES", "SOCRATES_QUESTION_CUP", ["WESTERN", "KNOWLEDGE_AND_DOUBT"]);
        Assert(wrongRoute.OpenSocratesVirtueOpportunity(OpportunityId, CombatId, 0)
               == SocratesVirtueRecordWriteResult.Rejected,
            "A non-virtue route must not manufacture a retreat opportunity.");

        PhilosophyRunState state = CreateVirtueState();
        Assert(state.OpenSocratesVirtueOpportunity(OpportunityId, CombatId, 0)
               == SocratesVirtueRecordWriteResult.Recorded,
            "A verified Socrates virtue route should record a real opportunity.");
        Assert(!state.TryGetSocratesVirtueMaterial(out _),
            "An opportunity without a committed action must remain unavailable material.");
        Assert(state.CancelSocratesVirtueOpportunity(OpportunityId)
               && !state.TryGetSocratesVirtueMaterial(out _),
            "A canceled request must leave no action material and consume nothing.");
    }

    private static void ContinuedFactsRoundTripWithoutInventingAResult()
    {
        PhilosophyRunState state = CreateVirtueState();
        Assert(state.OpenSocratesVirtueOpportunity(OpportunityId, CombatId, 0)
               == SocratesVirtueRecordWriteResult.Recorded,
            "The continued path needs a real opportunity first.");
        Assert(state.CommitSocratesVirtueAction(
                   OpportunityId,
                   SocratesVirtueAction.Continued,
                   retainedHitPointLoss: 7,
                   consumedPotionCount: 1)
               == SocratesVirtueRecordWriteResult.Recorded,
            "A verified continue action should commit once.");

        PhilosophyRunState restored = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));
        Assert(restored.TryGetSocratesVirtueMaterial(out SocratesVirtueMaterial? material)
               && material is not null
               && material.OnSocratesVirtueBranch
               && material.HadRealRetreatOpportunity
               && material.Action == SocratesVirtueAction.Continued
               && material.Result == SocratesVirtueActionResult.BattleContinued
               && material.RetainedHitPointLoss == 7
               && material.ConsumedPotionCount == 1
               && !material.RetreatOpportunityConsumed
               && !material.VictoryRewardsForfeited,
            "Continue must preserve only the facts known at the decision, not a later win or death.");
    }

    private static void RetreatedFactsRoundTripWithActualCosts()
    {
        PhilosophyRunState state = CreateVirtueState();
        state.OpenSocratesVirtueOpportunity(OpportunityId, CombatId, 1);
        Assert(state.CommitSocratesVirtueAction(
                   OpportunityId,
                   SocratesVirtueAction.Retreated,
                   retainedHitPointLoss: 12,
                   consumedPotionCount: 2)
               == SocratesVirtueRecordWriteResult.Recorded,
            "A completed non-victory retreat should commit once.");

        PhilosophyRunState restored = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));
        Assert(restored.TryGetSocratesVirtueMaterial(out SocratesVirtueMaterial? material)
               && material is not null
               && material.SourceVersion == SocratesVirtueUpstreamRecord.CurrentSourceVersion
               && material.SourceId == SocratesVirtueUpstreamRecord.CurrentSourceId
               && material.ThinkerId == SocratesVirtueUpstreamRecord.SocratesThinkerId
               && material.ProblemId == SocratesVirtueUpstreamRecord.VirtueProblemId
               && material.RouteNodeId == SocratesVirtueUpstreamRecord.VirtueRouteNodeId
               && material.OpportunityId == OpportunityId
               && material.CombatId == CombatId
               && material.ActIndex == 1
               && material.Action == SocratesVirtueAction.Retreated
               && material.Result == SocratesVirtueActionResult.NonVictoryRetreatCompleted
               && material.RetainedHitPointLoss == 12
               && material.ConsumedPotionCount == 2
               && material.RetreatOpportunityConsumed
               && material.VictoryRewardsForfeited,
            "Retreat must preserve its source identity, actual prior costs and forfeited rewards.");
    }

    private static void DuplicateAndConflictingCallbacksCannotRewriteFacts()
    {
        PhilosophyRunState state = CreateVirtueState();
        Assert(state.OpenSocratesVirtueOpportunity(OpportunityId, CombatId, 0)
               == SocratesVirtueRecordWriteResult.Recorded
               && state.OpenSocratesVirtueOpportunity(OpportunityId, CombatId, 0)
               == SocratesVirtueRecordWriteResult.Unchanged,
            "A duplicate opportunity callback must be idempotent.");
        Assert(state.CommitSocratesVirtueAction(OpportunityId, SocratesVirtueAction.Retreated, 5, 1)
               == SocratesVirtueRecordWriteResult.Recorded
               && state.CommitSocratesVirtueAction(OpportunityId, SocratesVirtueAction.Retreated, 5, 1)
               == SocratesVirtueRecordWriteResult.Unchanged,
            "A duplicate action callback must preserve the original facts.");

        PhilosophyRunState restored = PhilosophyRunStateCodec.Decode(PhilosophyRunStateCodec.Encode(state));
        Assert(restored.CommitSocratesVirtueAction(OpportunityId, SocratesVirtueAction.Retreated, 5, 1)
               == SocratesVirtueRecordWriteResult.Unchanged,
            "Reloading must not make the same action appendable again.");
        Assert(restored.CommitSocratesVirtueAction(OpportunityId, SocratesVirtueAction.Continued, 5, 1)
               == SocratesVirtueRecordWriteResult.Rejected
               && restored.CommitSocratesVirtueAction(OpportunityId, SocratesVirtueAction.Retreated, 6, 1)
               == SocratesVirtueRecordWriteResult.Rejected
               && restored.OpenSocratesVirtueOpportunity("OTHER_OPPORTUNITY", "OTHER_COMBAT", 0)
               == SocratesVirtueRecordWriteResult.Rejected,
            "A conflicting callback or save-load retry must not change a committed action.");
        Assert(!restored.CancelSocratesVirtueOpportunity(OpportunityId),
            "A committed action cannot be erased by a late cancellation.");
    }

    private static void InvalidOrIncompleteSavedFactsAreRejected()
    {
        PhilosophyRunState state = CreateVirtueState();
        state.OpenSocratesVirtueOpportunity(OpportunityId, CombatId, 0);
        state.CommitSocratesVirtueAction(OpportunityId, SocratesVirtueAction.Retreated, 4, 0);
        string json = Encoding.UTF8.GetString(Convert.FromHexString(PhilosophyRunStateCodec.Encode(state)));

        AssertDecodeFails(json.Replace(
                "\"SourceVersion\":1",
                "\"SourceVersion\":2",
                StringComparison.Ordinal),
            "An unknown source version must not be treated as current material.");
        AssertDecodeFails(json.Replace(
                "\"VictoryRewardsForfeited\":true",
                "\"VictoryRewardsForfeited\":false",
                StringComparison.Ordinal),
            "A retreat without its required forfeiture fact must be rejected.");
        AssertDecodeFails(json.Replace(
                "\"HadRealRetreatOpportunity\":true",
                "\"HadRealRetreatOpportunity\":false",
                StringComparison.Ordinal),
            "A committed action cannot exist without a real opportunity.");
    }

    private static PhilosophyRunState CreateVirtueState()
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
            "The test fixture must establish the production Socrates virtue branch.");
        return state;
    }

    private static string ToHex(string json) => Convert.ToHexString(Encoding.UTF8.GetBytes(json));

    private static void AssertDecodeFails(string json, string message)
    {
        try
        {
            _ = PhilosophyRunStateCodec.Decode(ToHex(json));
            throw new InvalidOperationException(message);
        }
        catch (InvalidDataException)
        {
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
