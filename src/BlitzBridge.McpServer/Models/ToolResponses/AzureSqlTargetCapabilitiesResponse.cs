using BlitzBridge.McpServer.Models;

namespace BlitzBridge.McpServer.Models.ToolResponses;

/// <summary>
/// Response payload for the <c>azure_sql_target_capabilities</c> tool.
/// </summary>
public sealed class AzureSqlTargetCapabilitiesResponse
{
    /// <summary>
    /// Target profile name.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Tool identifier.
    /// </summary>
    public string ToolName { get; set; } = "azure_sql_target_capabilities";

    /// <summary>
    /// Current execution database for the target connection.
    /// </summary>
    public string CurrentDatabase { get; set; } = string.Empty;

    /// <summary>
    /// SQL engine edition numeric value.
    /// </summary>
    public int EngineEdition { get; set; }

    /// <summary>
    /// SQL engine edition display name.
    /// </summary>
    public string EngineEditionName { get; set; } = "Unknown";

    /// <summary>
    /// Configured database allowlist for the profile.
    /// </summary>
    public List<string> AllowedDatabases { get; set; } = [];

    /// <summary>
    /// Configured procedure allowlist for the profile.
    /// </summary>
    public List<string> AllowedProcedures { get; set; } = [];

    /// <summary>
    /// Supported FRK procedures detected on the target.
    /// </summary>
    public List<string> InstalledProcedures { get; set; } = [];

    /// <summary>
    /// Indicates whether FRK AI prompt generation appears available.
    /// </summary>
    public bool SupportsAiPromptGeneration { get; set; }

    /// <summary>
    /// Indicates whether direct FRK AI calls appear available.
    /// </summary>
    public bool SupportsDirectAiCalls { get; set; }

    /// <summary>
    /// Indicates whether the AI prompt config table is present.
    /// </summary>
    public bool HasPromptConfigTable { get; set; }

    /// <summary>
    /// Indicates whether the AI provider config table is present.
    /// </summary>
    public bool HasProviderConfigTable { get; set; }

    /// <summary>
    /// Compact summary entries.
    /// </summary>
    public List<DiagnosticSummary> Summary { get; set; } = [];

    /// <summary>
    /// Additional capability notes.
    /// </summary>
    public List<string> Notes { get; set; } = [];
}
