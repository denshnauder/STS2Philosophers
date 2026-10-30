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
        if (journey.LastFixedAct != run.CurrentActIndex + 1) return false;
        EventModel? model;
        using (FixedEntryScope.Begin()) model = NativeQuestionNodeEvents.TrySelectAtActBoundary(run);
        if (model is null) return false;
        room = new EventRoom(model);
        return true;
    }
}
