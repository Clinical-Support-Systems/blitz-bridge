using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace BlitzBridge.McpServer.Services;

internal static class ResponseTelemetry
{
    private static readonly Meter Meter = new("BlitzBridge.Diagnostics");
    private static readonly Histogram<int> PayloadCharHistogram = Meter.CreateHistogram<int>("blitzbridge.tool.payload_chars");
    private static readonly Histogram<int> EstimatedTokenHistogram = Meter.CreateHistogram<int>("blitzbridge.tool.estimated_tokens");
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static T Capture<T>(string toolName, string target, bool includeVerboseResults, T response)
    {
        var payload = JsonSerializer.Serialize(response, SerializerOptions);
        var payloadChars = payload.Length;
        var estimatedTokens = (int)Math.Ceiling(payloadChars / 4d);

        Activity.Current?.SetTag("blitzbridge.response.estimated_tokens", estimatedTokens);

        var tags = new TagList
        {
            { "tool", toolName },
            { "target", target },
            { "include_verbose_results", includeVerboseResults }
        };

        PayloadCharHistogram.Record(payloadChars, tags);
        EstimatedTokenHistogram.Record(estimatedTokens, tags);

        return response;
    }
}
