namespace BlitzBridge.McpServer.Models.ToolResponses
{
    public sealed class AzureSqlHealthCheckResponse
    {
        public string Target { get; set; } = string.Empty;
        public string ToolName { get; set; } = "azure_sql_health_check";
        public int TotalFindings { get; set; }
        public List<DiagnosticSummary> Summary { get; set; } = new();
        public List<Dictionary<string, object?>> Findings { get; set; } = new();
        public List<ProcedureResultSet> ResultSets { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }
}
