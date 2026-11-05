using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

// This release adds only the reviewed Locke -> Hume handover. Other installed
// figures retain their existing selectors; unfinished development is not bundled.
internal static class ActFixedEntryHost
{
    internal static bool TryPrepare(RunState run, out EventRoom? room)
    {
        room = null;
        if (run.CurrentActIndex is not (1 or 2)) return false;
        using (FixedEntryScope.Begin())
        {
            EventModel? model = run.CurrentActIndex == 1
                ? LockeCargoIdentityPatch.TrySelectNative(run)
                : HumeInductionPatch.TrySelectNative(run);
            if (model is null) return false;
            room = new EventRoom(model);
            return true;
        }
    }
}
