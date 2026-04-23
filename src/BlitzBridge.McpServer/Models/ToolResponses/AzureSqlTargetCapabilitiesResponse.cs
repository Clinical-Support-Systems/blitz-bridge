using BlitzBridge.McpServer.Models;

namespace BlitzBridge.McpServer.Models.ToolResponses;

public sealed class AzureSqlTargetCapabilitiesResponse
{
    public string Target { get; set; } = string.Empty;

    public string ToolName { get; set; } = "azure_sql_target_capabilities";

    public string CurrentDatabase { get; set; } = string.Empty;

    public int EngineEdition { get; set; }

    public string EngineEditionName { get; set; } = "Unknown";

    public List<string> AllowedDatabases { get; set; } = [];

    public List<string> AllowedProcedures { get; set; } = [];

    public List<string> InstalledProcedures { get; set; } = [];

    public bool SupportsAiPromptGeneration { get; set; }

    public bool SupportsDirectAiCalls { get; set; }

    public bool HasPromptConfigTable { get; set; }

    public bool HasProviderConfigTable { get; set; }

    public List<DiagnosticSummary> Summary { get; set; } = [];

    public List<string> Notes { get; set; } = [];
}
