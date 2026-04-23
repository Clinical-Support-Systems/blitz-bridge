namespace BlitzBridge.McpServer.Configuration;

internal enum BlitzBridgeAuthMode
{
    None,
    BearerToken
}

internal sealed class BlitzBridgeAuthOptions
{
    public const string SectionName = "BlitzBridge:Auth";
    public const string EnvironmentTokenListVariable = "BLITZBRIDGE_AUTH_TOKENS";

    public string Mode { get; set; } = nameof(BlitzBridgeAuthMode.None);

    public List<string> Tokens { get; set; } = [];
}
