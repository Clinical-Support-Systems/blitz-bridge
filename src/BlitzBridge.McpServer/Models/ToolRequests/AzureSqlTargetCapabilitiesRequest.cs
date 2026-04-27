namespace BlitzBridge.McpServer.Models.ToolRequests;

/// <summary>
/// Request model for the <c>azure_sql_target_capabilities</c> tool.
/// </summary>
public sealed class AzureSqlTargetCapabilitiesRequest
{
    /// <summary>
    /// Target profile name.
    /// </summary>
    public string Target { get; set; } = string.Empty;
}
