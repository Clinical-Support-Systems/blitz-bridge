namespace BlitzBridge.McpServer.Models.ToolResponses;

/// <summary>
/// Response payload for the <c>azure_sql_fetch_detail_by_handle</c> tool.
/// </summary>
public sealed class AzureSqlFetchDetailByHandleResponse
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
    /// Scope metadata reconstructed from the handle payload.
    /// </summary>
    public Dictionary<string, object?> Scope { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Detail rows for tabular sections.
    /// </summary>
    public List<Dictionary<string, object?>> Items { get; set; } = [];

    /// <summary>
    /// Content type for text-based detail sections.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Text payload for non-tabular detail sections.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Additional notes for interpretation.
    /// </summary>
    public List<string> Notes { get; set; } = [];
}
