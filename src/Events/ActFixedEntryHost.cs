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
        // The position is open when the previous act's fixed event was taken
        // (act index + 1) or when it was not (act index). Both are real runs.
        int act = run.CurrentActIndex;
        if (journey.LastFixedAct is var position && (position < act || position > act + 1)) return false;
        // The boundary fills this act's slot: the rooms and their own consistency
        // rules read the slot being filled, not the last one filled.
        int arrived = journey.LastFixedAct;
        if (arrived == act) journey.LastFixedAct = act + 1;
        // The boundary is where the run learns which act it is in. Rooms and
        // questions record their result against this act, not against the act
        // written on an edge.
        journey.Act = act + 1;
        // A run that never took the first act's question still has to follow a
        // line: the design gives every entry a default one, and it is the first
        // line the route table lists for that entry.
        WesternRouteGraph.NameDefaultRoute(journey);
        EventModel? model;
        using (FixedEntryScope.Begin()) model = NativeQuestionNodeEvents.TrySelectAtActBoundary(run);
        if (model is null)
        {
            journey.LastFixedAct = arrived;
            return false;
        }
        room = new EventRoom(model);
        return true;
    }
}
