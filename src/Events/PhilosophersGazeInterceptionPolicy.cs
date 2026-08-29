namespace STS2Philosophers;

internal enum CurrentEventKind
{
    None,
    Neow,
    PhilosophersGaze,
    Other,
}

internal readonly record struct PhilosophersGazeInterceptionContext(
    bool RunInProgress,
    bool CurrentRoomIsEventRoom,
    CurrentEventKind CurrentEvent,
    bool HistoryContainsPhilosophersGaze,
    bool ModelAvailable,
    bool IsSingleplayer);

internal static class PhilosophersGazeInterceptionPolicy
{
    public static bool ShouldIntercept(PhilosophersGazeInterceptionContext context)
    {
        return context.RunInProgress
            && context.CurrentRoomIsEventRoom
            && context.CurrentEvent == CurrentEventKind.Neow
            && !context.HistoryContainsPhilosophersGaze
            && context.ModelAvailable
            && context.IsSingleplayer;
    }
}
