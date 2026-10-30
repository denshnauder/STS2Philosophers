namespace STS2Philosophers;

/// <summary>
/// Marks the short window in which a room is selected at the act boundary
/// right after the ancient event instead of from an unknown map point. Room
/// selectors keep their own route, relic, and saved-state gates; only the
/// native question-point anchor is relaxed inside the scope.
/// </summary>
internal static class FixedEntryScope
{
    [ThreadStatic] private static int _depth;

    internal static bool Active => _depth > 0;

    internal static IDisposable Begin()
    {
        _depth++;
        return new Lease();
    }

    private sealed class Lease : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            _depth--;
        }
    }
}
