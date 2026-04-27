namespace BlitzBridge.McpServer.Models.ToolResponses
{
    /// <summary>
    /// Response payload for the <c>azure_sql_health_check</c> tool.
    /// </summary>
    public sealed class AzureSqlHealthCheckResponse
    {
        /// <summary>
        /// Target profile name.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Tool identifier.
        /// </summary>
        public string ToolName { get; set; } = "azure_sql_health_check";

        /// <summary>
        /// Optional database name override used for execution.
        /// </summary>
        public string? DatabaseName { get; set; }

        /// <summary>
        /// Total findings returned by FRK before compacting.
        /// </summary>
        public int TotalFindings { get; set; }

        /// <summary>
        /// Highest visible priority in the compact response.
        /// </summary>
        public int? HighestVisiblePriority { get; set; }

        /// <summary>
        /// Visible finding groups in the compact response.
        /// </summary>
        public List<string> VisibleFindingGroups { get; set; } = new();

        /// <summary>
        /// Compact summary entries.
        /// </summary>
        public List<DiagnosticSummary> Summary { get; set; } = new();

        /// <summary>
        /// Section-level detail handles.
        /// </summary>
        public List<AzureSqlDetailHandle> Handles { get; set; } = new();

        /// <summary>
        /// Compacted findings result rows.
        /// </summary>
        public List<Dictionary<string, object?>> Findings { get; set; } = new();

        /// <summary>
        /// Optional verbose raw result sets.
        /// </summary>
        public List<ProcedureResultSet> ResultSets { get; set; } = new();

        /// <summary>
        /// Additional notes for interpretation.
        /// </summary>
        public List<string> Notes { get; set; } = new();
    }
}
