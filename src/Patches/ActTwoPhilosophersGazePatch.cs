using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ActTwoPhilosophersGazePatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.CurrentActIndex != 1) return null;
        // The fixed boundary owns the act's one continuation slot. Once it has
        // taken that slot, a later question node must not present the same
        // successor a second time.
        if (PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.PhilosophersGazeFixedEntryAct == run.CurrentActIndex + 1)
        {
            return null;
        }

        EventModel? selected = SelectContinuation(run);
        if (selected is not null
            && PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? selectedState)
            && selectedState is not null)
        {
            // A legacy save can reach a map question before the fixed boundary.
            // Whichever entry opens first owns the one continuation slot.
            selectedState.PhilosophersGazeFixedEntryAct = run.CurrentActIndex + 1;
        }

        return selected;
    }

    internal static EventModel? TrySelectFixedNative(RunState run)
    {
        if (!FixedEntryScope.Active || run.CurrentActIndex != 1) return null;
        if (!NativeQuestionNodeEvents.IsEntry(run)) return null;
        if (PhilosophyRunStateService.TryGet(run, out PhilosophyRunState? shared)
            && shared?.PhilosophersGazeFixedEntryAct == run.CurrentActIndex + 1)
        {
            return null;
        }

        return SelectContinuation(run);
    }

    private static EventModel? SelectContinuation(RunState run)
    {
        var owner = run.Players[0];
        if (PhilosophersGazeContinuationPolicy.GetAvailableOptions(
                PhilosophersGaze.GetOwnership(owner), PhilosophersGaze.HasContinuationBeenRecorded(owner))
            == PhilosophersGazeContinuationOption.None) return null;
        try
        {
            // The opening has already visited this ModelId. The act and durable
            // continuation/replacement facts distinguish this second legal slot.
            return ModelDb.Event<PhilosophersGaze>();
        }
        catch (Exception exception)
        {
            Log.Error($"[STS2Philosophers] PhilosophersGaze continuation unavailable: {exception}");
            return null;
        }
    }
}
