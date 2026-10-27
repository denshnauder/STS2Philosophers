using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace STS2Philosophers;

internal static class ActTwoPhilosophersGazePatch
{
    internal static EventModel? TrySelectNative(RunState run)
    {
        if (!NativeQuestionNodeEvents.IsEntry(run) || run.CurrentActIndex != 1) return null;
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
