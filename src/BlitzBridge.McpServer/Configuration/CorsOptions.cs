namespace BlitzBridge.McpServer.Configuration;

internal sealed class CorsOptions
{
    public const string SectionName = "BlitzBridge:Cors";

    public bool AllowAnyOrigin { get; set; }

    public List<string> AllowedOrigins { get; set; } = [];
}
