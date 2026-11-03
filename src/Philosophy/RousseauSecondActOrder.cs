namespace STS2Philosophers;

// Rousseau's act-two slot is shared by two rooms. The seed's parity decides
// which one opens it, so the rule lives in one place: the boundary asks the
// pair in this order, and the tests can name the room that must win.
internal static class RousseauSecondActOrder
{
    internal static bool WollstonecraftFirst(string? seed) =>
        seed is { Length: > 0 } && (seed[^1] - '0') % 2 == 0;
}
