namespace STS2Philosophers;

public sealed class RitsuRunPersistenceProbeState
{
    public const int CurrentSchemaVersion = 1;
    public const string ExpectedToken = "ENC68_RITSULIB_RUN_DATA";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string ProbeToken { get; set; } = string.Empty;
    public int WriteCount { get; set; }

    internal void RecordWrite()
    {
        SchemaVersion = CurrentSchemaVersion;
        ProbeToken = ExpectedToken;
        WriteCount = checked(WriteCount + 1);
    }

    internal bool IsValid() =>
        SchemaVersion == CurrentSchemaVersion &&
        ProbeToken == ExpectedToken &&
        WriteCount > 0;
}
