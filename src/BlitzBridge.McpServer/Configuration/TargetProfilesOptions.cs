namespace BlitzBridge.McpServer.Configuration;

/// <summary>
/// Root options object for configured SQL target profiles.
/// </summary>
public sealed class SqlTargetOptions
{
    /// <summary>
    /// Configuration section name for SQL targets.
    /// </summary>
    public const string SectionName = "SqlTargets";

    /// <summary>
    /// Named SQL target profiles keyed by target name.
    /// </summary>
    public Dictionary<string, SqlTargetProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Configuration for a single SQL diagnostic target.
/// </summary>
public sealed class SqlTargetProfile
{
    /// <summary>
    /// ADO.NET connection string for the target.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Optional database allowlist for this target.
    /// </summary>
    public List<string> AllowedDatabases { get; set; } = [];

    /// <summary>
    /// Optional FRK procedure allowlist for this target.
    /// </summary>
    public List<string> AllowedProcedures { get; set; } = [];

    /// <summary>
    /// Indicates whether this target profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Command timeout in seconds for procedure execution.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// AI mode override for FRK procedures (0, 1, or 2).
    /// </summary>
    public int AiMode { get; set; } = 2;
}
