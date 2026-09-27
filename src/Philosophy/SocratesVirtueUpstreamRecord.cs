using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal enum SocratesVirtueAction
{
    None,
    Continued,
    Retreated,
}

internal enum SocratesVirtueActionResult
{
    None,
    BattleContinued,
    NonVictoryRetreatCompleted,
}

internal enum SocratesVirtueRecordWriteResult
{
    Rejected,
    Recorded,
    Unchanged,
}

internal sealed record SocratesVirtueMaterial(
    int SourceVersion,
    string SourceId,
    string ThinkerId,
    string ProblemId,
    string RouteNodeId,
    string OpportunityId,
    string CombatId,
    int ActIndex,
    bool OnSocratesVirtueBranch,
    bool HadRealRetreatOpportunity,
    SocratesVirtueAction Action,
    SocratesVirtueActionResult Result,
    int RetainedHitPointLoss,
    int ConsumedPotionCount,
    bool RetreatOpportunityConsumed,
    bool VictoryRewardsForfeited);

internal sealed class SocratesVirtueUpstreamRecord
{
    public const int CurrentSourceVersion = 1;
    public const string CurrentSourceId = "SOCRATES_VIRTUE_RETREAT";
    public const string SocratesThinkerId = "SOCRATES";
    public const string VirtueProblemId = "VIRTUE_AND_HAPPINESS";
    public const string VirtueRouteNodeId = "SOCRATES__VIRTUE_AND_HAPPINESS__CORE";

    [JsonRequired]
    public int SourceVersion { get; set; } = CurrentSourceVersion;

    [JsonRequired]
    public string SourceId { get; set; } = CurrentSourceId;

    [JsonRequired]
    public string ThinkerId { get; set; } = SocratesThinkerId;

    [JsonRequired]
    public string ProblemId { get; set; } = VirtueProblemId;

    [JsonRequired]
    public string RouteNodeId { get; set; } = VirtueRouteNodeId;

    [JsonRequired]
    public string OpportunityId { get; set; } = string.Empty;

    [JsonRequired]
    public string CombatId { get; set; } = string.Empty;

    [JsonRequired]
    public int ActIndex { get; set; }

    [JsonRequired]
    public bool OnSocratesVirtueBranch { get; set; }

    [JsonRequired]
    public bool HadRealRetreatOpportunity { get; set; }

    [JsonRequired]
    public SocratesVirtueAction Action { get; set; }

    [JsonRequired]
    public SocratesVirtueActionResult Result { get; set; }

    [JsonRequired]
    public int RetainedHitPointLoss { get; set; }

    [JsonRequired]
    public int ConsumedPotionCount { get; set; }

    [JsonRequired]
    public bool RetreatOpportunityConsumed { get; set; }

    [JsonRequired]
    public bool VictoryRewardsForfeited { get; set; }

    internal static SocratesVirtueUpstreamRecord Open(
        string opportunityId,
        string combatId,
        int actIndex)
    {
        return new SocratesVirtueUpstreamRecord
        {
            OpportunityId = opportunityId,
            CombatId = combatId,
            ActIndex = actIndex,
            OnSocratesVirtueBranch = true,
            HadRealRetreatOpportunity = true,
        };
    }

    internal bool MatchesOpportunity(string opportunityId, string combatId, int actIndex)
    {
        return string.Equals(OpportunityId, opportunityId, StringComparison.Ordinal)
            && string.Equals(CombatId, combatId, StringComparison.Ordinal)
            && ActIndex == actIndex;
    }

    internal SocratesVirtueRecordWriteResult Commit(
        string opportunityId,
        SocratesVirtueAction action,
        int retainedHitPointLoss,
        int consumedPotionCount)
    {
        if (!string.Equals(OpportunityId, opportunityId, StringComparison.Ordinal)
            || action is not (SocratesVirtueAction.Continued or SocratesVirtueAction.Retreated)
            || retainedHitPointLoss < 0
            || consumedPotionCount < 0)
        {
            return SocratesVirtueRecordWriteResult.Rejected;
        }

        SocratesVirtueActionResult result = action == SocratesVirtueAction.Continued
            ? SocratesVirtueActionResult.BattleContinued
            : SocratesVirtueActionResult.NonVictoryRetreatCompleted;
        bool consumedOpportunity = action == SocratesVirtueAction.Retreated;
        bool forfeitedRewards = action == SocratesVirtueAction.Retreated;

        if (Action != SocratesVirtueAction.None)
        {
            return Action == action
                && Result == result
                && RetainedHitPointLoss == retainedHitPointLoss
                && ConsumedPotionCount == consumedPotionCount
                && RetreatOpportunityConsumed == consumedOpportunity
                && VictoryRewardsForfeited == forfeitedRewards
                    ? SocratesVirtueRecordWriteResult.Unchanged
                    : SocratesVirtueRecordWriteResult.Rejected;
        }

        Action = action;
        Result = result;
        RetainedHitPointLoss = retainedHitPointLoss;
        ConsumedPotionCount = consumedPotionCount;
        RetreatOpportunityConsumed = consumedOpportunity;
        VictoryRewardsForfeited = forfeitedRewards;
        return SocratesVirtueRecordWriteResult.Recorded;
    }

    internal bool IsValid()
    {
        if (SourceVersion != CurrentSourceVersion
            || !string.Equals(SourceId, CurrentSourceId, StringComparison.Ordinal)
            || !string.Equals(ThinkerId, SocratesThinkerId, StringComparison.Ordinal)
            || !string.Equals(ProblemId, VirtueProblemId, StringComparison.Ordinal)
            || !string.Equals(RouteNodeId, VirtueRouteNodeId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(OpportunityId)
            || OpportunityId.Length > 256
            || string.IsNullOrWhiteSpace(CombatId)
            || CombatId.Length > 256
            || ActIndex < 0
            || !OnSocratesVirtueBranch
            || !HadRealRetreatOpportunity
            || !Enum.IsDefined(Action)
            || !Enum.IsDefined(Result)
            || RetainedHitPointLoss < 0
            || ConsumedPotionCount < 0)
        {
            return false;
        }

        return Action switch
        {
            SocratesVirtueAction.None =>
                Result == SocratesVirtueActionResult.None
                && RetainedHitPointLoss == 0
                && ConsumedPotionCount == 0
                && !RetreatOpportunityConsumed
                && !VictoryRewardsForfeited,
            SocratesVirtueAction.Continued =>
                Result == SocratesVirtueActionResult.BattleContinued
                && !RetreatOpportunityConsumed
                && !VictoryRewardsForfeited,
            SocratesVirtueAction.Retreated =>
                Result == SocratesVirtueActionResult.NonVictoryRetreatCompleted
                && RetreatOpportunityConsumed
                && VictoryRewardsForfeited,
            _ => false,
        };
    }

    internal bool TryGetMaterial(out SocratesVirtueMaterial? material)
    {
        material = null;
        if (!IsValid() || Action == SocratesVirtueAction.None)
        {
            return false;
        }

        material = new SocratesVirtueMaterial(
            SourceVersion,
            SourceId,
            ThinkerId,
            ProblemId,
            RouteNodeId,
            OpportunityId,
            CombatId,
            ActIndex,
            OnSocratesVirtueBranch,
            HadRealRetreatOpportunity,
            Action,
            Result,
            RetainedHitPointLoss,
            ConsumedPotionCount,
            RetreatOpportunityConsumed,
            VictoryRewardsForfeited);
        return true;
    }
}
