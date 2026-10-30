using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

/// <summary>
/// Presents the act's fixed philosophy room at the boundary right after the
/// act's ancient event. The room's own selector decides whether it applies, so
/// an absent or ineligible room simply leaves the original proceed untouched.
/// </summary>
internal static class ActFixedEntryHost
{
    internal static bool TryPrepare(RunState run, out EventRoom? room)
    {
        room = null;
        if (run.CurrentActIndex is not (1 or 2)) return false;
        if (!PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared) || shared?.WesternJourney is not { } journey)
            return false;
        int act = run.CurrentActIndex + 1;
        foreach (var edge in WesternRouteGraph.LoadEmbedded().GetEligibleEdges(journey, "Fixed"))
        {
            if (edge.Act != act || !ActFixedEntryCatalog.IsRegistered(edge.ToNodeId)) continue;
            if (!ActFixedEntryCatalog.TrySelect(run, edge.ToNodeId, out EventModel? model) || model is null) continue;
            room = new EventRoom(model);
            return true;
        }
        return false;
    }
}
