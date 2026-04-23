namespace BlitzBridge.McpServer.Models.ToolRequests;

public sealed class AzureSqlBlitzIndexRequest
{
    public string Target { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    public string SchemaName { get; set; } = "dbo";

    public string TableName { get; set; } = string.Empty;

    public int Mode { get; set; }

    public int ThresholdMb { get; set; } = 250;

    public bool ExpertMode { get; set; }

    public int AiMode { get; set; }

    public string? AiPromptConfigTable { get; set; }

    public string? AiPromptName { get; set; }

    public int MaxRows { get; set; } = 100;

    public bool IncludeVerboseResults { get; set; }
}
