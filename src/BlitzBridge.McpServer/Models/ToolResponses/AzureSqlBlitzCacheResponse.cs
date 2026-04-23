using BlitzBridge.McpServer.Models;

namespace BlitzBridge.McpServer.Models.ToolResponses;

public sealed class AzureSqlBlitzCacheResponse
{
    public string Target { get; set; } = string.Empty;

    public string ToolName { get; set; } = "azure_sql_blitz_cache";

    public string SortOrder { get; set; } = "cpu";

    public string? DatabaseName { get; set; }

    public int AiMode { get; set; }

    public List<DiagnosticSummary> Summary { get; set; } = [];

    public List<Dictionary<string, object?>> Queries { get; set; } = [];

    public List<Dictionary<string, object?>> WarningGlossary { get; set; } = [];

    public string? AiPrompt { get; set; }

    public string? AiAdvice { get; set; }

    public List<ProcedureResultSet> ResultSets { get; set; } = [];

    public List<string> Notes { get; set; } = [];
}
