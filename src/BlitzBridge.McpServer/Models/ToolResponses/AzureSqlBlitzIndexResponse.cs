using BlitzBridge.McpServer.Models;

namespace BlitzBridge.McpServer.Models.ToolResponses;

public sealed class AzureSqlBlitzIndexResponse
{
    public string Target { get; set; } = string.Empty;

    public string ToolName { get; set; } = "azure_sql_blitz_index";

    public string DatabaseName { get; set; } = string.Empty;

    public string SchemaName { get; set; } = "dbo";

    public string TableName { get; set; } = string.Empty;

    public int AiMode { get; set; }

    public List<DiagnosticSummary> Summary { get; set; } = [];

    public List<Dictionary<string, object?>> ExistingIndexes { get; set; } = [];

    public List<Dictionary<string, object?>> MissingIndexes { get; set; } = [];

    public List<Dictionary<string, object?>> ColumnDataTypes { get; set; } = [];

    public List<Dictionary<string, object?>> ForeignKeys { get; set; } = [];

    public string? AiPrompt { get; set; }

    public string? AiAdvice { get; set; }

    public List<ProcedureResultSet> ResultSets { get; set; } = [];

    public List<string> Notes { get; set; } = [];
}
