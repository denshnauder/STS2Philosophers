namespace STS2Philosophers;

internal sealed class PhilosophersGazeResolutionGate
{
    private int _isResolving;

    public bool TryBegin()
    {
        return Interlocked.CompareExchange(ref _isResolving, 1, 0) == 0;
    }

    public void End()
    {
        Volatile.Write(ref _isResolving, 0);
    }
}

internal static class PhilosophersGazeReplacementPolicy
{
    public static int CaptureInheritedVirtue(int virtue)
    {
        return Math.Max(0, virtue);
    }

    public static bool IsMengziReplacementVerified(
        bool hasOriginal,
        bool hasReplacement,
        int expectedVirtue,
        int actualVirtue)
    {
        return !hasOriginal
            && hasReplacement
            && actualVirtue == CaptureInheritedVirtue(expectedVirtue);
    }

    public static bool IsXunziReplacementVerified(
        bool hasOriginal,
        bool hasReplacement)
    {
        return !hasOriginal && hasReplacement;
    }

    public static bool IsSimpleReplacementVerified(
        bool hasOriginal,
        bool hasReplacement)
    {
        return !hasOriginal && hasReplacement;
    }
}
