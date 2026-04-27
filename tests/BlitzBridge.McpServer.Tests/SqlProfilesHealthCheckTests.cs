using System.Net;
using System.Text;

namespace BlitzBridge.McpServer.Tests;

public class SqlProfilesHealthCheckTests
{
    private const string InitializeRequestJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"health-test\",\"version\":\"1.0.0\"}}}";

    [Test]
    public async Task Health_WhenNoEnabledProfiles_ReturnsOk()
    {
        await using var server = new AuthWebApplicationFactory("None");
        using var client = server.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains("Healthy");
    }

    [Test]
    public async Task Health_WhenSqlUnreachable_ReturnsDegradedWithOkStatus()
    {
        var config = new Dictionary<string, string?>
        {
            ["SqlTargets:Profiles:primary-sql-target:Enabled"] = "true",
            ["SqlTargets:Profiles:primary-sql-target:ConnectionString"] = "Server=tcp:127.0.0.1,1;Database=testdb;Authentication=Active Directory Default;Encrypt=True;ApplicationIntent=ReadOnly;Connection Timeout=1;",
            ["SqlTargets:Profiles:primary-sql-target:AllowedProcedures:0"] = "sp_Blitz",
            ["SqlTargets:Profiles:primary-sql-target:CommandTimeoutSeconds"] = "1"
        };

        await using var server = new AuthWebApplicationFactory("None", extraConfig: config);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains("Degraded");
    }

    [Test]
    public async Task McpEndpoint_StillServes_WhenHealthIsDegraded()
    {
        var config = new Dictionary<string, string?>
        {
            ["SqlTargets:Profiles:primary-sql-target:Enabled"] = "true",
            ["SqlTargets:Profiles:primary-sql-target:ConnectionString"] = "Server=tcp:127.0.0.1,1;Database=testdb;Authentication=Active Directory Default;Encrypt=True;ApplicationIntent=ReadOnly;Connection Timeout=1;",
            ["SqlTargets:Profiles:primary-sql-target:AllowedProcedures:0"] = "sp_Blitz",
            ["SqlTargets:Profiles:primary-sql-target:CommandTimeoutSeconds"] = "1"
        };

        await using var server = new AuthWebApplicationFactory("None", extraConfig: config);
        using var client = server.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");
        request.Content = new StringContent(InitializeRequestJson, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
