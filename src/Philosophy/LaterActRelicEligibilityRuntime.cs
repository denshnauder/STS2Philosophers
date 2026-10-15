using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2Philosophers;

internal static class LaterActRelicEligibilityRuntime
{
    public static bool IsEligible(
        Player? player,
        string candidateId,
        int currentActIndex) =>
        player is not null
        && LaterActRelicEligibilityCatalog.IsEligible(
            candidateId,
            currentActIndex,
            player.Relics.Select(relic => relic.Id.Entry));
}
