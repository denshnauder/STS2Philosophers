using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ActFixedEntryCatalog
{
    private static readonly IReadOnlyDictionary<string, Func<RunState, EventModel?>> Selectors =
        new Dictionary<string, Func<RunState, EventModel?>>(StringComparer.Ordinal)
        {
            ["NICCOLO_MACHIAVELLI__HISTORY_AND_POWER__CORE"] =
                MachiavelliFloodPreparationPatch.TrySelectFixedEntry,
        };

    internal static bool TrySelect(RunState run, string? nodeId, out EventModel? model)
    {
        model = null;
        if (nodeId is null || !Selectors.TryGetValue(nodeId, out var selector)) return false;
        model = selector(run);
        return model is not null;
    }

    internal static bool IsRegistered(string? nodeId) =>
        nodeId is not null && Selectors.ContainsKey(nodeId);
}
