namespace STS2Philosophers;

/// <summary>
/// The act's fixed philosophy event belongs to the map boundary right after the
/// act's ancient event, not to an extra question mark. This policy and catalog
/// are the single entry used by the proceed interception; rooms register their
/// own selector and keep their question-node path untouched.
/// </summary>
internal readonly record struct ActFixedEntryContext(
    bool RunInProgress,
    bool CurrentRoomIsEventRoom,
    bool CurrentEventIsAncient,
    bool CurrentEventFinished,
    int ActIndex,
    bool HistoryContainsFixedEntry,
    bool FixedEdgeEligible,
    bool RoomAvailable,
    bool IsSingleplayer);

internal static class ActFixedEntryPolicy
{
    public static bool ShouldIntercept(ActFixedEntryContext context) =>
        context.RunInProgress
        && context.CurrentRoomIsEventRoom
        && context.CurrentEventIsAncient
        && context.CurrentEventFinished
        && context.ActIndex is 1 or 2
        && !context.HistoryContainsFixedEntry
        && context.FixedEdgeEligible
        && context.RoomAvailable
        && context.IsSingleplayer;
}
