using BlitzBridge.McpServer.Configuration;
using BlitzBridge.McpServer.Middleware;
using Microsoft.Extensions.Configuration;

namespace BlitzBridge.McpServer.Tests;

public class HttpAuthAndCorsTests
{
    [Test]
    public async Task ResolveConfiguredTokens_UsesEnvironmentVariablePrecedence()
    {
        var authOptions = new BlitzBridgeAuthOptions
        {
            Tokens = ["config-token-1", "config-token-2"]
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BlitzBridgeAuthOptions.EnvironmentTokenListVariable] = "env-token-a;env-token-b"
            })
            .Build();

        var tokens = McpHttpAuthMiddleware.ResolveConfiguredTokens(authOptions, config);

        await Assert.That(tokens.Count).IsEqualTo(2);
        await Assert.That(tokens[0]).IsEqualTo("env-token-a");
        await Assert.That(tokens[1]).IsEqualTo("env-token-b");
    }

    [Test]
    public async Task ResolveConfiguredTokens_FallsBackToConfigTokens_WhenEnvironmentIsUnset()
    {
        var authOptions = new BlitzBridgeAuthOptions
        {
            Tokens = ["config-token-1", " ", "config-token-2", "config-token-1"]
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var tokens = McpHttpAuthMiddleware.ResolveConfiguredTokens(authOptions, config);

        await Assert.That(tokens.Count).IsEqualTo(2);
        await Assert.That(tokens[0]).IsEqualTo("config-token-1");
        await Assert.That(tokens[1]).IsEqualTo("config-token-2");
    }

    [Test]
    public async Task TryExtractBearerToken_ParsesAuthorizationHeader()
    {
        var parsed = McpHttpAuthMiddleware.TryExtractBearerToken("Bearer  abc123 ", out var token);

        await Assert.That(parsed).IsTrue();
        await Assert.That(token).IsEqualTo("abc123");
    }

    [Test]
    public async Task IsValidBearerToken_PerformsTokenAllowListMatch()
    {
        var valid = McpHttpAuthMiddleware.IsValidBearerToken("expected-token", ["foo", "expected-token", "bar"]);
        var invalid = McpHttpAuthMiddleware.IsValidBearerToken("expected-token", ["foo", "bar"]);

        await Assert.That(valid).IsTrue();
        await Assert.That(invalid).IsFalse();
    }

    [Test]
    public async Task CorsOptions_DefaultsToDenyByOmission()
    {
        var options = new CorsOptions();

        await Assert.That(options.AllowAnyOrigin).IsFalse();
        await Assert.That(options.AllowedOrigins.Count).IsEqualTo(0);
    }
}
