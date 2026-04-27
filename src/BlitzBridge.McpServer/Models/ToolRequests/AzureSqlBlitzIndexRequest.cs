namespace BlitzBridge.McpServer.Models.ToolRequests;

/// <summary>
/// Request model for the <c>azure_sql_blitz_index</c> tool.
/// </summary>
public sealed class AzureSqlBlitzIndexRequest
{
    /// <summary>
    /// Target profile name.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Database name containing the table to analyze.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Schema name for the target table.
    /// </summary>
    public string SchemaName { get; set; } = "dbo";

    /// <summary>
    /// Table name to analyze with <c>sp_BlitzIndex</c>.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// FRK mode value passed to <c>sp_BlitzIndex</c>.
    /// </summary>
    public int Mode { get; set; }

    /// <summary>
    /// Threshold in megabytes for index recommendations.
    /// </summary>
    public int ThresholdMb { get; set; } = 250;

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
    public int MaxRows { get; set; } = 100;

    /// <summary>
    /// Includes raw FRK result sets when true.
    /// </summary>
    public bool IncludeVerboseResults { get; set; }
}
