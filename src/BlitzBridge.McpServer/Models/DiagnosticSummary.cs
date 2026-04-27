namespace BlitzBridge.McpServer.Models;

/// <summary>
/// Compact summary item emitted with tool responses.
/// </summary>
public sealed class DiagnosticSummary
{
    /// <summary>
    /// Short title for the summary item.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Severity label such as info or warning.
    /// </summary>
    public string Severity { get; set; } = "info";

    /// <summary>
    /// Human-readable summary message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
