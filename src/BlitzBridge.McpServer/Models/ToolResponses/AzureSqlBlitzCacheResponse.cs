using BlitzBridge.McpServer.Models;

namespace BlitzBridge.McpServer.Models.ToolResponses;

/// <summary>
/// Response payload for the <c>azure_sql_blitz_cache</c> tool.
/// </summary>
public sealed class AzureSqlBlitzCacheResponse
{
    /// <summary>
    /// Target profile name.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Tool identifier.
    /// </summary>
    public string ToolName { get; set; } = "azure_sql_blitz_cache";

    /// <summary>
    /// Effective sort order used for results.
    /// </summary>
    public string SortOrder { get; set; } = "cpu";

    /// <summary>
    /// Optional database context used for execution.
    /// </summary>
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Effective FRK AI mode used for execution.
    /// </summary>
    public int AiMode { get; set; }

    /// <summary>
    /// Compact summary entries.
    /// </summary>
    public List<DiagnosticSummary> Summary { get; set; } = [];

    /// <summary>
    /// Compacted query rows.
    /// </summary>
    public List<Dictionary<string, object?>> Queries { get; set; } = [];

    /// <summary>
    /// Compacted warning glossary rows.
    /// </summary>
    public List<Dictionary<string, object?>> WarningGlossary { get; set; } = [];

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
