namespace BlitzBridge.McpServer.Models.ToolRequests
{
    public sealed class AzureSqlHealthCheckRequest
    {
        public string Target { get; set; } = string.Empty;
        public string? DatabaseName { get; set; }
        public int? MinimumPriority { get; set; }
        public bool ExpertMode { get; set; } = false;
        public int MaxRows { get; set; } = 25;
        public bool IncludeVerboseResults { get; set; }
    }
}
