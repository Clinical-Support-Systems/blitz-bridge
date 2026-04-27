using System.Text;
using System.Text.Json;

using BlitzBridge.McpServer.Models.ToolRequests;

namespace BlitzBridge.McpServer.Services;

internal static class ProgressiveDisclosureHandleCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string CreateHandle(AzureSqlHealthCheckRequest request, string kind)
        => CreateHandle(new ProgressiveDisclosureHandlePayload
        {
            Version = "v1",
            Target = request.Target,
            ParentTool = "azure_sql_health_check",
            Kind = kind,
            DatabaseName = request.DatabaseName,
            MinimumPriority = request.MinimumPriority,
            ExpertMode = request.ExpertMode
        });

    public static string CreateHandle(AzureSqlBlitzCacheRequest request, string kind)
        => CreateHandle(new ProgressiveDisclosureHandlePayload
        {
            Version = "v1",
            Target = request.Target,
            ParentTool = "azure_sql_blitz_cache",
            Kind = kind,
            DatabaseName = request.DatabaseName,
            SortOrder = request.SortOrder,
            Top = request.Top,
            ExpertMode = request.ExpertMode,
            AiMode = request.AiMode,
            AiPromptConfigTable = request.AiPromptConfigTable,
            AiPromptName = request.AiPromptName
        });

    public static string CreateHandle(AzureSqlBlitzIndexRequest request, string kind)
        => CreateHandle(new ProgressiveDisclosureHandlePayload
        {
            Version = "v1",
            Target = request.Target,
            ParentTool = "azure_sql_blitz_index",
            Kind = kind,
            DatabaseName = request.DatabaseName,
            SchemaName = request.SchemaName,
            TableName = request.TableName,
            Mode = request.Mode,
            ThresholdMb = request.ThresholdMb,
            ExpertMode = request.ExpertMode,
            AiMode = request.AiMode,
            AiPromptConfigTable = request.AiPromptConfigTable,
            AiPromptName = request.AiPromptName
        });

    public static string CreateHandle(AzureSqlCurrentIncidentRequest request, string kind)
        => CreateHandle(new ProgressiveDisclosureHandlePayload
        {
            Version = "v1",
            Target = request.Target,
            ParentTool = "azure_sql_current_incident",
            Kind = kind,
            ExpertMode = request.ExpertMode
        });

    public static ProgressiveDisclosureHandlePayload Decode(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            throw CreateMalformedHandleException();
        }

        var encodedPayload = handle.StartsWith("v1:", StringComparison.OrdinalIgnoreCase)
            ? handle[3..]
            : handle;

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPayload));
            var payload = JsonSerializer.Deserialize<ProgressiveDisclosureHandlePayload>(json, SerializerOptions);

            if (payload is null ||
                !string.Equals(payload.Version, "v1", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(payload.Target) ||
                string.IsNullOrWhiteSpace(payload.ParentTool) ||
                string.IsNullOrWhiteSpace(payload.Kind))
            {
                throw CreateMalformedHandleException();
            }

            return payload;
        }
        catch (ProgressiveDisclosureException)
        {
            throw;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or DecoderFallbackException)
        {
            throw CreateMalformedHandleException();
        }
    }

    private static string CreateHandle(ProgressiveDisclosureHandlePayload payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        return $"v1:{Convert.ToBase64String(Encoding.UTF8.GetBytes(json))}";
    }

    private static ProgressiveDisclosureException CreateMalformedHandleException()
        => new(
            "malformed_handle",
            "Handle must be a valid base64-encoded JSON object with fields: version, parentTool, kind, target, ...",
            400);
}

internal sealed class ProgressiveDisclosureHandlePayload
{
    public string Version { get; set; } = "v1";

    public string Target { get; set; } = string.Empty;

    public string ParentTool { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string? DatabaseName { get; set; }

    public int? MinimumPriority { get; set; }

    public bool? ExpertMode { get; set; }

    public string? SortOrder { get; set; }

    public int? Top { get; set; }

    public int? AiMode { get; set; }

    public string? AiPromptConfigTable { get; set; }

    public string? AiPromptName { get; set; }

    public string? SchemaName { get; set; }

    public string? TableName { get; set; }

    public int? Mode { get; set; }

    public int? ThresholdMb { get; set; }
}
