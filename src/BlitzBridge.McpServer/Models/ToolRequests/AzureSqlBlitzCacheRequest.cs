namespace BlitzBridge.McpServer.Models.ToolRequests;

public sealed class AzureSqlBlitzCacheRequest
{
    public string Target { get; set; } = string.Empty;

    public string? DatabaseName { get; set; }

    public string SortOrder { get; set; } = "cpu";

    public int Top { get; set; } = 10;

    public bool ExpertMode { get; set; }

    public int AiMode { get; set; }

    public string? AiPromptConfigTable { get; set; }

    public string? AiPromptName { get; set; }

    public int MaxRows { get; set; } = 50;

    public bool IncludeVerboseResults { get; set; }
}
