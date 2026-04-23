using BlitzBridge.McpServer.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BlitzBridge.McpServer.Tests;

internal sealed class AuthWebApplicationFactory(
    string mode,
    string? tokens = null,
    IReadOnlyDictionary<string, string?>? extraConfig = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var config = new Dictionary<string, string?>
            {
                ["BlitzBridge:Auth:Mode"] = mode,
                [$"{SqlTargetOptions.SectionName}:Profiles:test:Enabled"] = "false"
            };

            if (!string.IsNullOrWhiteSpace(tokens))
            {
                config["BLITZBRIDGE_AUTH_TOKENS"] = tokens;
            }

            if (extraConfig is not null)
            {
                foreach (var (key, value) in extraConfig)
                {
                    config[key] = value;
                }
            }

            configBuilder.AddInMemoryCollection(config);
        });
    }
}
