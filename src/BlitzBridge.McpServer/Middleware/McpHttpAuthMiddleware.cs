using System.Security.Cryptography;
using System.Text;
using BlitzBridge.McpServer.Configuration;
using Microsoft.Extensions.Options;

namespace BlitzBridge.McpServer.Middleware;

internal sealed class McpHttpAuthMiddleware(
    RequestDelegate next,
    IOptions<BlitzBridgeAuthOptions> authOptionsAccessor,
    IConfiguration configuration,
    ILogger<McpHttpAuthMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly BlitzBridgeAuthOptions _authOptions = authOptionsAccessor.Value;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<McpHttpAuthMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var authMode = ResolveAuthMode(_authOptions.Mode);
        if (authMode != BlitzBridgeAuthMode.BearerToken)
        {
            await _next(context);
            return;
        }

        var configuredTokens = ResolveConfiguredTokens(_authOptions, _configuration);
        if (configuredTokens.Count == 0)
        {
            LogFailedAuth(context, "<missing-config>");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!TryExtractBearerToken(context.Request.Headers.Authorization, out var presentedToken))
        {
            LogFailedAuth(context, "<missing-or-malformed>");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!IsValidBearerToken(presentedToken, configuredTokens))
        {
            LogFailedAuth(context, presentedToken);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await _next(context);
    }

    private static BlitzBridgeAuthMode ResolveAuthMode(string mode)
    {
        return Enum.TryParse<BlitzBridgeAuthMode>(mode, ignoreCase: true, out var parsedMode)
            ? parsedMode
            : BlitzBridgeAuthMode.None;
    }

    internal static IReadOnlyList<string> ResolveConfiguredTokens(BlitzBridgeAuthOptions authOptions, IConfiguration configuration)
    {
        // Precedence rule:
        // 1) BLITZBRIDGE_AUTH_TOKENS (semicolon-separated) when present with at least one non-empty token.
        // 2) BlitzBridge:Auth:Tokens from config providers (json/env indexed keys/etc.).
        var envTokenList = configuration[BlitzBridgeAuthOptions.EnvironmentTokenListVariable];
        if (!string.IsNullOrWhiteSpace(envTokenList))
        {
            var envTokens = envTokenList
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (envTokens.Length > 0)
            {
                return envTokens;
            }
        }

        return authOptions.Tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool TryExtractBearerToken(string? authorizationHeader, out string token)
    {
        token = string.Empty;
        const string bearerPrefix = "Bearer ";
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extractedToken = authorizationHeader[bearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(extractedToken))
        {
            return false;
        }

        token = extractedToken;
        return true;
    }

    internal static bool IsValidBearerToken(string presentedToken, IReadOnlyList<string> configuredTokens)
    {
        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedToken));
        foreach (var configuredToken in configuredTokens)
        {
            var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredToken));
            if (CryptographicOperations.FixedTimeEquals(presentedHash, configuredHash))
            {
                return true;
            }
        }

        return false;
    }

    private void LogFailedAuth(HttpContext context, string tokenForHashing)
    {
        var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "<unknown>";
        var tokenHash = TruncateHash(tokenForHashing);
        _logger.LogWarning(
            "Rejected /mcp HTTP request due to failed auth. SourceIp={SourceIp} TokenHashPrefix={TokenHashPrefix}",
            sourceIp,
            tokenHash);
    }

    private static string TruncateHash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..12];
    }
}
