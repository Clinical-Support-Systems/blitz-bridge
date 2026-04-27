namespace BlitzBridge.McpServer.Models.ToolResponses
{
    /// <summary>
    /// Response payload for the <c>azure_sql_current_incident</c> tool.
    /// </summary>
    public sealed class AzureSqlCurrentIncidentResponse
    {
        /// <summary>
        /// Target profile name.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Tool identifier.
        /// </summary>
        public string ToolName { get; set; } = "azure_sql_current_incident";

        /// <summary>
        /// Total wait rows returned by FRK before compacting.
        /// </summary>
        public int TotalWaitRows { get; set; }

        /// <summary>
        /// Total finding rows returned by FRK before compacting.
        /// </summary>
        public int TotalFindingRows { get; set; }

        /// <summary>
        /// Top wait types visible in the compact response.
        /// </summary>
        public List<string> TopWaitTypes { get; set; } = new();

        /// <summary>
        /// Compact summary entries.
        /// </summary>
        public List<DiagnosticSummary> Summary { get; set; } = new();

        /// <summary>
        /// Section-level detail handles.
        /// </summary>
        public List<AzureSqlDetailHandle> Handles { get; set; } = new();

        /// <summary>
        /// Compacted waits result rows.
        /// </summary>
        public List<Dictionary<string, object?>> Waits { get; set; } = new();

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
