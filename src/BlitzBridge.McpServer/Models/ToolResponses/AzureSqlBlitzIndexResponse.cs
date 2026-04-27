using BlitzBridge.McpServer.Models;

namespace BlitzBridge.McpServer.Models.ToolResponses;

/// <summary>
/// Response payload for the <c>azure_sql_blitz_index</c> tool.
/// </summary>
public sealed class AzureSqlBlitzIndexResponse
{
    /// <summary>
    /// Target profile name.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Tool identifier.
    /// </summary>
    public string ToolName { get; set; } = "azure_sql_blitz_index";

    /// <summary>
    /// Database name used for table analysis.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Schema name used for table analysis.
    /// </summary>
    public string SchemaName { get; set; } = "dbo";

    /// <summary>
    /// Table name analyzed by <c>sp_BlitzIndex</c>.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// Effective FRK AI mode used for execution.
    /// </summary>
    public int AiMode { get; set; }

    /// <summary>
    /// Compact summary entries.
    /// </summary>
    public List<DiagnosticSummary> Summary { get; set; } = [];

    /// <summary>
    /// Compacted existing index rows.
    /// </summary>
    public List<Dictionary<string, object?>> ExistingIndexes { get; set; } = [];

    /// <summary>
    /// Compacted missing index rows.
    /// </summary>
    public List<Dictionary<string, object?>> MissingIndexes { get; set; } = [];

    /// <summary>
    /// Compacted column data type rows.
    /// </summary>
    public List<Dictionary<string, object?>> ColumnDataTypes { get; set; } = [];

    /// <summary>
    /// Compacted foreign key rows.
    /// </summary>
    public List<Dictionary<string, object?>> ForeignKeys { get; set; } = [];

    /// <summary>
    /// FRK-generated AI prompt when available.
    /// </summary>
    public string? AiPrompt { get; set; }

    /// <summary>
    /// FRK direct AI advice when available.
    /// </summary>
    public string? AiAdvice { get; set; }

    /// <summary>
    /// Optional verbose raw result sets.
    /// </summary>
    public List<ProcedureResultSet> ResultSets { get; set; } = [];

    /// <summary>
    /// Additional notes for interpretation.
    /// </summary>
    public List<string> Notes { get; set; } = [];
}
