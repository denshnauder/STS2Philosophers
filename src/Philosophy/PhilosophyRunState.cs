using System.Text.Json.Serialization;

namespace STS2Philosophers;

internal sealed class PhilosophyRunState
{
    private static readonly ZenoRouteValidationCatalog EmptyZenoCatalog = new([], [], [], []);

    public CurrentDoctrine? CurrentDoctrine { get; set; }
    public List<ThoughtImprint> ThoughtImprints { get; set; } = [];
    public Dictionary<int, ActBehaviorState> ActBehaviorStates { get; set; } = [];
    public Dictionary<string, GeneratedCandidates> GeneratedCandidates { get; set; } = [];
    public WesternJourneyState? WesternJourney { get; set; }

    [JsonIgnore]
    public ZenoRouteDecodeResult ZenoRoutePayload { get; private set; } = new(
        ZenoRoutePayloadClassification.PreFeatureLegacy,
        null,
        null);

    [JsonIgnore]
    public string? ZenoRouteOriginalEncodedState { get; private set; }

    [JsonIgnore]
    public IReadOnlyList<string> PreservedSaveMarkerEntries { get; private set; } = [];

    [JsonIgnore]
    internal ZenoRouteValidationCatalog ZenoRouteValidationCatalog { get; private set; } = EmptyZenoCatalog;

    public bool HasData => CurrentDoctrine is not null
        || ThoughtImprints.Count > 0
        || ActBehaviorStates.Count > 0
        || GeneratedCandidates.Count > 0
        || WesternJourney is not null
        || ZenoRoutePayload.Classification != ZenoRoutePayloadClassification.PreFeatureLegacy
        || PreservedSaveMarkerEntries.Count > 0;

    internal void SetCurrentZenoRouteState(ZenoRouteFeatureState state)
    {
        ZenoRoutePayload = new ZenoRouteDecodeResult(
            ZenoRoutePayloadClassification.Current,
            state,
            null);
        ZenoRouteOriginalEncodedState = null;
    }

    internal void SetCurrentZenoRouteState(
        ZenoRouteFeatureState state,
        ZenoRouteValidationCatalog catalog)
    {
        ZenoRouteValidationCatalog = catalog;
        SetCurrentZenoRouteState(state);
    }

    internal void SetZenoRouteValidationCatalog(ZenoRouteValidationCatalog catalog)
    {
        ZenoRouteValidationCatalog = catalog;
    }

    internal void RestoreZenoRoutePayload(ZenoRouteDecodeResult result, string? originalEncodedState)
    {
        ZenoRoutePayload = result;
        ZenoRouteOriginalEncodedState = result.Classification is
            ZenoRoutePayloadClassification.Current or ZenoRoutePayloadClassification.PreFeatureLegacy
            ? null
            : originalEncodedState;
    }

    internal void PreserveSaveMarkerEntries(IEnumerable<string> entries)
    {
        PreservedSaveMarkerEntries = entries.ToArray();
    }

    public ActBehaviorState GetOrCreateActBehaviorState(int actIndex)
    {
        if (!ActBehaviorStates.TryGetValue(actIndex, out ActBehaviorState? state))
        {
            state = new ActBehaviorState { ActIndex = actIndex };
            ActBehaviorStates.Add(actIndex, state);
        }

        return state;
    }

    public void RecordCurrentDoctrine(
        string thinkerId,
        string doctrineId,
        IEnumerable<string>? routeTags = null)
    {
        List<string> recordedRouteTags = routeTags?
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
        CurrentDoctrine = new CurrentDoctrine
        {
            ThinkerId = thinkerId,
            DoctrineId = doctrineId,
        };

        ThoughtImprint? existing = ThoughtImprints.FirstOrDefault(imprint =>
            string.Equals(imprint.ThinkerId, thinkerId, StringComparison.Ordinal)
            && string.Equals(imprint.DoctrineId, doctrineId, StringComparison.Ordinal));
        if (existing is null)
        {
            ThoughtImprints.Add(new ThoughtImprint
            {
                ThinkerId = thinkerId,
                DoctrineId = doctrineId,
                RouteTags = recordedRouteTags,
                RelationshipState = ThoughtImprintRelationship.Current,
            });
        }
        else
        {
            existing.RouteTags = recordedRouteTags;
            existing.RelationshipState = ThoughtImprintRelationship.Current;
        }
    }

    internal void NormalizeAfterLoad()
    {
        ThoughtImprints ??= [];
        ActBehaviorStates ??= [];
        GeneratedCandidates ??= [];
        WesternJourney?.NormalizeAfterLoad();
        foreach (ActBehaviorState behaviorState in ActBehaviorStates.Values)
        {
            behaviorState.NormalizeAfterLoad();
        }
    }
}

internal sealed class CurrentDoctrine
{
    public string ThinkerId { get; set; } = string.Empty;
    public string DoctrineId { get; set; } = string.Empty;
}

internal sealed class ThoughtImprint
{
    public string ThinkerId { get; set; } = string.Empty;
    public string DoctrineId { get; set; } = string.Empty;
    public List<string> RouteTags { get; set; } = [];
    public string RelationshipState { get; set; } = string.Empty;
}

internal static class ThoughtImprintRelationship
{
    public const string Current = "CURRENT";
}

internal sealed class ActBehaviorState
{
    public int ActIndex { get; set; }
    public int CompletedCombatCount { get; set; }
    public Dictionary<string, int> GameFacts { get; set; } = [];
    public Dictionary<string, int> ExpressionOpportunities { get; set; } = [];
    public Dictionary<string, int> Impressions { get; set; } = [];
    public CombatBehaviorState? ActiveCombat { get; set; }

    public bool TryBeginCombat(string combatId)
    {
        if (string.IsNullOrWhiteSpace(combatId))
        {
            return false;
        }

        if (ActiveCombat is not null)
        {
            return false;
        }

        ActiveCombat = new CombatBehaviorState { CombatId = combatId };
        return true;
    }

    public bool RecordGameFact(string factId, int amount = 1)
    {
        if (ActiveCombat is null
            || string.IsNullOrWhiteSpace(factId)
            || amount <= 0)
        {
            return false;
        }

        ActiveCombat.GameFacts[factId] = ActiveCombat.GameFacts.GetValueOrDefault(factId) + amount;
        return true;
    }

    public bool RecordExpressionOpportunity(string impressionId)
    {
        return ActiveCombat is not null
            && !string.IsNullOrWhiteSpace(impressionId)
            && ActiveCombat.ExpressionOpportunities.Add(impressionId);
    }

    public bool RecordImpression(string impressionId)
    {
        return ActiveCombat is not null
            && !string.IsNullOrWhiteSpace(impressionId)
            && ActiveCombat.Impressions.Add(impressionId);
    }

    public bool CompleteCombat()
    {
        if (ActiveCombat is null)
        {
            return false;
        }

        foreach ((string factId, int amount) in ActiveCombat.GameFacts)
        {
            GameFacts[factId] = GameFacts.GetValueOrDefault(factId) + amount;
        }

        foreach (string impressionId in ActiveCombat.ExpressionOpportunities)
        {
            ExpressionOpportunities[impressionId] =
                ExpressionOpportunities.GetValueOrDefault(impressionId) + 1;
        }

        foreach (string impressionId in ActiveCombat.Impressions)
        {
            Impressions[impressionId] = Impressions.GetValueOrDefault(impressionId) + 1;
        }

        CompletedCombatCount++;
        ActiveCombat = null;
        return true;
    }

    public bool DiscardActiveCombat()
    {
        if (ActiveCombat is null)
        {
            return false;
        }

        ActiveCombat = null;
        return true;
    }

    internal void NormalizeAfterLoad()
    {
        GameFacts ??= [];
        ExpressionOpportunities ??= [];
        Impressions ??= [];
        ActiveCombat?.NormalizeAfterLoad();
    }
}

internal sealed class CombatBehaviorState
{
    public string CombatId { get; set; } = string.Empty;
    public Dictionary<string, int> GameFacts { get; set; } = [];
    public HashSet<string> ExpressionOpportunities { get; set; } = [];
    public HashSet<string> Impressions { get; set; } = [];

    internal void NormalizeAfterLoad()
    {
        GameFacts ??= [];
        ExpressionOpportunities ??= [];
        Impressions ??= [];
    }
}

internal sealed class GeneratedCandidates
{
    public string GenerationKey { get; set; } = string.Empty;
    public List<string> CandidateIds { get; set; } = [];
}
