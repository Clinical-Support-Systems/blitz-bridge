namespace BlitzBridge.McpServer.Models;

/// <summary>
/// Generic representation of a returned procedure result set.
/// </summary>
public sealed class ProcedureResultSet
{
    /// <summary>
    /// Logical name of the result set.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ordered column names for the result set.
    /// </summary>
    public List<string> Columns { get; set; } = [];

    /// <summary>
    /// Row data represented as key/value column maps.
    /// </summary>
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}
