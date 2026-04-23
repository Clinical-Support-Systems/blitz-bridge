namespace BlitzBridge.McpServer.Models;

public sealed class ProcedureResultSet
{
    public string Name { get; set; } = string.Empty;

    public List<string> Columns { get; set; } = [];

    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}
