using System.Text.Json;

namespace STS2Philosophers;

internal enum PhilosophyRunStateMarkerClassification
{
    None,
    Current,
    IdenticalCurrentDuplicates,
    UnknownVersion,
    Conflicting,
    InvalidCurrent,
}

internal sealed record PhilosophyRunStateMarkerRestoreResult(
    PhilosophyRunStateMarkerClassification Classification,
    PhilosophyRunState? State,
    int MarkerCount)
{
    public bool IsIsolated => Classification is
        PhilosophyRunStateMarkerClassification.UnknownVersion or
        PhilosophyRunStateMarkerClassification.Conflicting or
        PhilosophyRunStateMarkerClassification.InvalidCurrent;
}

internal static class PhilosophyRunStateMarkerCarrier
{
    public const string CurrentVersionPrefix = "V1_";

    public static PhilosophyRunStateMarkerRestoreResult Restore(IReadOnlyList<string> entries)
    {
        return Restore(entries, ZenoRouteValidationCatalogs.SaveRestore);
    }

    internal static PhilosophyRunStateMarkerRestoreResult Restore(
        IReadOnlyList<string> entries,
        ZenoRouteValidationCatalog zenoCatalog)
    {
        if (entries.Count == 0)
        {
            return new PhilosophyRunStateMarkerRestoreResult(
                PhilosophyRunStateMarkerClassification.None,
                null,
                0);
        }

        string first = entries[0];
        bool identical = entries.All(entry => string.Equals(entry, first, StringComparison.Ordinal));
        if (!identical)
        {
            return Isolated(
                PhilosophyRunStateMarkerClassification.Conflicting,
                entries);
        }

        if (!first.StartsWith(CurrentVersionPrefix, StringComparison.Ordinal))
        {
            return Isolated(
                PhilosophyRunStateMarkerClassification.UnknownVersion,
                entries);
        }

        try
        {
            PhilosophyRunState state = PhilosophyRunStateCodec.Decode(
                first[CurrentVersionPrefix.Length..],
                zenoCatalog);
            if (state.ZenoRoutePayload.Classification is not (
                    ZenoRoutePayloadClassification.PreFeatureLegacy or
                    ZenoRoutePayloadClassification.Current))
            {
                state.PreserveSaveMarkerEntries(entries);
                return new PhilosophyRunStateMarkerRestoreResult(
                    PhilosophyRunStateMarkerClassification.InvalidCurrent,
                    state,
                    entries.Count);
            }

            return new PhilosophyRunStateMarkerRestoreResult(
                entries.Count == 1
                    ? PhilosophyRunStateMarkerClassification.Current
                    : PhilosophyRunStateMarkerClassification.IdenticalCurrentDuplicates,
                state,
                entries.Count);
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or InvalidDataException or InvalidOperationException or
            ArgumentException or OverflowException or NotSupportedException)
        {
            return Isolated(
                PhilosophyRunStateMarkerClassification.InvalidCurrent,
                entries);
        }
    }

    public static IReadOnlyList<string> GetEntriesForSave(PhilosophyRunState state)
    {
        return GetEntriesForSave(state, state.ZenoRouteValidationCatalog);
    }

    internal static IReadOnlyList<string> GetEntriesForSave(
        PhilosophyRunState state,
        ZenoRouteValidationCatalog zenoCatalog)
    {
        if (state.PreservedSaveMarkerEntries.Count > 0)
        {
            return state.PreservedSaveMarkerEntries.ToArray();
        }

        return state.HasData
            ? [$"{CurrentVersionPrefix}{PhilosophyRunStateCodec.Encode(state, zenoCatalog)}"]
            : [];
    }

    private static PhilosophyRunStateMarkerRestoreResult Isolated(
        PhilosophyRunStateMarkerClassification classification,
        IReadOnlyList<string> entries)
    {
        PhilosophyRunState state = new();
        state.PreserveSaveMarkerEntries(entries);
        return new PhilosophyRunStateMarkerRestoreResult(classification, state, entries.Count);
    }
}
