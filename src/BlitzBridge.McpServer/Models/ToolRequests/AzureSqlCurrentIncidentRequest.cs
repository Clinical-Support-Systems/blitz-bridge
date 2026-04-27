namespace BlitzBridge.McpServer.Models.ToolRequests
{
    /// <summary>
    /// Request model for the <c>azure_sql_current_incident</c> tool.
    /// </summary>
    public sealed class AzureSqlCurrentIncidentRequest
    {
        /// <summary>
        /// Target profile name.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Optional database name (not used by <c>sp_BlitzFirst</c> in this server).
        /// </summary>
        public string? DatabaseName { get; set; }

        /// <summary>
        /// Enables FRK expert mode output when supported.
        /// </summary>
        public bool ExpertMode { get; set; } = false;

        /// <summary>
        /// Maximum rows retained in compacted response payloads.
        /// </summary>
        public int MaxRows { get; set; } = 25;

        /// <summary>
        /// Includes raw FRK result sets when true.
        /// </summary>
        public bool IncludeVerboseResults { get; set; }
    }
}
