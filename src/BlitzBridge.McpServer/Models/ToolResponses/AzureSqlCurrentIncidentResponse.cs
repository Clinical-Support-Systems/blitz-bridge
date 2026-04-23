namespace BlitzBridge.McpServer.Models.ToolResponses
{
    public sealed class AzureSqlCurrentIncidentResponse
    {
        public string Target { get; set; } = string.Empty;
        public string ToolName { get; set; } = "azure_sql_current_incident";
        public int TotalWaitRows { get; set; }
        public int TotalFindingRows { get; set; }
        public List<DiagnosticSummary> Summary { get; set; } = new();
        public List<Dictionary<string, object?>> Waits { get; set; } = new();
        public List<Dictionary<string, object?>> Findings { get; set; } = new();
        public List<ProcedureResultSet> ResultSets { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }
}
