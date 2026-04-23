namespace BlitzBridge.McpServer.Configuration;

public sealed class SqlTargetOptions
{
    public const string SectionName = "SqlTargets";

    public Dictionary<string, SqlTargetProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SqlTargetProfile
{
    public string ConnectionString { get; set; } = string.Empty;

    public List<string> AllowedDatabases { get; set; } = [];

    public List<string> AllowedProcedures { get; set; } = [];

    public bool Enabled { get; set; } = true;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public int AiMode { get; set; } = 2;
}
