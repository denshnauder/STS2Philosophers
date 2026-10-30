using STS2Philosophers;

internal static class ActFixedEntryChecks
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    internal static void Run()
    {
        ActFixedEntryContext context = new(
            RunInProgress: true, CurrentRoomIsEventRoom: true, CurrentEventIsAncient: true,
            CurrentEventFinished: true, ActIndex: 1, HistoryContainsFixedEntry: false,
            FixedEdgeEligible: true, RoomAvailable: true, IsSingleplayer: true);
        Check(ActFixedEntryPolicy.ShouldIntercept(context),
            "The second act's fixed room belongs right after its ancient event.");
        foreach (var denied in new[]
        {
            context with { RunInProgress = false },
            context with { CurrentRoomIsEventRoom = false },
            context with { CurrentEventIsAncient = false },
            context with { CurrentEventFinished = false },
            context with { ActIndex = 0 },
            context with { ActIndex = 3 },
            context with { HistoryContainsFixedEntry = true },
            context with { FixedEdgeEligible = false },
            context with { RoomAvailable = false },
            context with { IsSingleplayer = false },
        })
            Check(!ActFixedEntryPolicy.ShouldIntercept(denied),
                "An unfinished, repeated, ineligible, absent, or multiplayer entry must not intercept.");
        Check(ActFixedEntryPolicy.ShouldIntercept(context with { ActIndex = 2 }),
            "The third act's fixed room uses the same boundary rule.");
    }
}
