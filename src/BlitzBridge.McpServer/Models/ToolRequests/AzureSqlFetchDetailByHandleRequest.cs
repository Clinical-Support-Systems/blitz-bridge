namespace BlitzBridge.McpServer.Models.ToolRequests;

/// <summary>
/// Request model for the <c>azure_sql_fetch_detail_by_handle</c> tool.
/// </summary>
public sealed class AzureSqlFetchDetailByHandleRequest
{
    /// <summary>
    /// Target profile name.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Parent tool that emitted the handle.
    /// </summary>
    public string ParentTool { get; set; } = string.Empty;

    /// <summary>
    /// Section kind requested from the parent tool.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Opaque section handle returned by the parent tool.
    /// </summary>
    public string Handle { get; set; } = string.Empty;

    /// <summary>
    /// Maximum rows retained in the detail payload.
    /// </summary>
    public int MaxRows { get; set; } = 100;
}
