namespace BlitzBridge.McpServer.Models.ToolRequests;

/// <summary>
/// Request model for the <c>azure_sql_blitz_cache</c> tool.
/// </summary>
public sealed class AzureSqlBlitzCacheRequest
{
    /// <summary>
    /// Target profile name.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Optional database name override for procedure execution.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Sort order used by <c>sp_BlitzCache</c>.
    /// </summary>
    public string SortOrder { get; set; } = "cpu";

    /// <summary>
    /// Number of rows requested from <c>sp_BlitzCache</c>.
    /// </summary>
    public int Top { get; set; } = 10;

    /// <summary>
    /// Enables FRK expert mode output when supported.
    /// </summary>
    public bool ExpertMode { get; set; }

    /// <summary>
    /// FRK AI mode (0, 1, or 2).
    /// </summary>
    public int AiMode { get; set; }

    /// <summary>
    /// Optional AI prompt configuration table name.
    /// </summary>
    public string? AiPromptConfigTable { get; set; }

    /// <summary>
    /// Optional AI prompt name.
    /// </summary>
    public string? AiPromptName { get; set; }

    /// <summary>
    /// Maximum rows retained in compacted response payloads.
    /// </summary>
    public int MaxRows { get; set; } = 50;

    /// <summary>
    /// Includes raw FRK result sets when true.
    /// </summary>
    public bool IncludeVerboseResults { get; set; }
}
