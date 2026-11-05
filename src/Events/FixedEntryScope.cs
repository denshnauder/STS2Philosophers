namespace STS2Philosophers;

// Only the finished Ancient proceed hook opens this scope.
internal static class FixedEntryScope
{
    [ThreadStatic] private static int _depth;
    internal static bool Active => _depth > 0;
    internal static IDisposable Begin() { _depth++; return new Scope(); }
    private sealed class Scope : IDisposable
    {
        private bool _disposed;
        public void Dispose() { if (!_disposed) { _disposed = true; _depth--; } }
    }
}
