namespace BlitzBridge.McpServer.Models.ToolResponses;

/// <summary>
/// Opaque progressive-disclosure handle returned by a summary response.
/// </summary>
public sealed class AzureSqlDetailHandle
{
    /// <summary>
    /// Opaque token that can be passed to the detail tool.
    /// </summary>
    public string Handle { get; set; } = string.Empty;

    /// <summary>
    /// Parent tool name that emitted the handle.
    /// </summary>
    public string ParentTool { get; set; } = string.Empty;

    /// <summary>
    /// Section discriminator for explicit detail dispatch.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable title for the expandable section.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Short preview of what the handle expands.
    /// </summary>
    public string? Preview { get; set; }

    /// <summary>
    /// Severity label such as info or warning.
    /// </summary>
    public string Severity { get; set; } = "info";

    /// <summary>
    /// Number of visible items represented in the compact parent response.
    /// </summary>
    public int? ItemCount { get; set; }

    /// <summary>
    /// Total number of items available for the section before compacting.
    /// </summary>
    public int? TotalCount { get; set; }
}
