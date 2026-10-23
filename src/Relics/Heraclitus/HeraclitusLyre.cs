namespace STS2Philosophers;

public sealed class HeraclitusLyre : WesternPracticeRelic
{
    internal bool CanOfferCopernicus => IsMutable && Owner is not null && IsSoleDoctrine;
    protected override string ProblemId => "BEING_AND_CHANGE";
    protected override string ResourceName => "heraclitus_lyre";
}
