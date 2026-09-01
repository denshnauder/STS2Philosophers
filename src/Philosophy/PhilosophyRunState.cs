namespace STS2Philosophers;

internal sealed class PhilosophyRunState
{
    public CurrentDoctrine? CurrentDoctrine { get; set; }
    public List<ThoughtImprint> ThoughtImprints { get; set; } = [];
    public Dictionary<int, ActBehaviorState> ActBehaviorStates { get; set; } = [];
    public Dictionary<string, GeneratedCandidates> GeneratedCandidates { get; set; } = [];

    public bool HasData => CurrentDoctrine is not null
        || ThoughtImprints.Count > 0
        || ActBehaviorStates.Count > 0
        || GeneratedCandidates.Count > 0;

    public ActBehaviorState GetOrCreateActBehaviorState(int actIndex)
    {
        if (!ActBehaviorStates.TryGetValue(actIndex, out ActBehaviorState? state))
        {
            state = new ActBehaviorState { ActIndex = actIndex };
            ActBehaviorStates.Add(actIndex, state);
        }

        return state;
    }

    public void RecordCurrentDoctrine(string thinkerId, string doctrineId)
    {
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
                RelationshipState = ThoughtImprintRelationship.Current,
            });
        }
        else
        {
            existing.RelationshipState = ThoughtImprintRelationship.Current;
        }
    }

    internal void NormalizeAfterLoad()
    {
        ThoughtImprints ??= [];
        ActBehaviorStates ??= [];
        GeneratedCandidates ??= [];
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
    public Dictionary<string, int> Impressions { get; set; } = [];
}

internal sealed class GeneratedCandidates
{
    public string GenerationKey { get; set; } = string.Empty;
    public List<string> CandidateIds { get; set; } = [];
}
