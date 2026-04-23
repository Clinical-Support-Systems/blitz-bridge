using System.Net;
using System.Text;

namespace BlitzBridge.McpServer.Tests;

public class HttpAuthIntegrationTests
{
    private const string ExpectedToken = "expected-test-token";
    private const string InitializeRequestJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"mcmanus-http-auth\",\"version\":\"1.0.0\"}}}";

    [Test]
    public async Task BearerTokenMode_WithoutAuthHeader_Returns401()
    {
        await using var server = new AuthWebApplicationFactory("BearerToken", ExpectedToken);
        using var client = server.CreateClient();
        using var request = CreateInitializeRequest();

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task BearerTokenMode_WithWrongToken_Returns401()
    {
        await using var server = new AuthWebApplicationFactory("BearerToken", ExpectedToken);
        using var client = server.CreateClient();
        using var request = CreateInitializeRequest();
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer wrong-token");

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task BearerTokenMode_WithCorrectToken_Returns200()
    {
        await using var server = new AuthWebApplicationFactory("BearerToken", ExpectedToken);
        using var client = server.CreateClient();
        using var request = CreateInitializeRequest();
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {ExpectedToken}");

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task NoneMode_Returns200_RegardlessOfHeader()
    {
        await using var server = new AuthWebApplicationFactory("None", ExpectedToken);
        using var client = server.CreateClient();

        using var noHeaderRequest = CreateInitializeRequest();
        var noHeaderResponse = await client.SendAsync(noHeaderRequest);
        await Assert.That(noHeaderResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var wrongHeaderRequest = CreateInitializeRequest();
        wrongHeaderRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer not-used");
        var wrongHeaderResponse = await client.SendAsync(wrongHeaderRequest);
        await Assert.That(wrongHeaderResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static HttpRequestMessage CreateInitializeRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");
        request.Content = new StringContent(InitializeRequestJson, Encoding.UTF8, "application/json");
        return request;
    }
}
