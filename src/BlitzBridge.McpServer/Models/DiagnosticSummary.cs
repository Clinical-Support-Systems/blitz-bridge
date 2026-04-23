namespace BlitzBridge.McpServer.Models;

public sealed class DiagnosticSummary
{
    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = "info";

    public string Message { get; set; } = string.Empty;
}
