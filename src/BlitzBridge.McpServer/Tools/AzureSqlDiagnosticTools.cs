using System.ComponentModel;

using BlitzBridge.McpServer.Models.ToolRequests;
using BlitzBridge.McpServer.Services;
using ModelContextProtocol.Server;

namespace BlitzBridge.McpServer.Tools;

/// <summary>
/// MCP tool surface for Azure SQL FRK diagnostics.
/// </summary>
[McpServerToolType]
public sealed class AzureSqlDiagnosticTools
{
    private readonly FrkProcedureService _frkProcedureService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureSqlDiagnosticTools"/> class.
    /// </summary>
    /// <param name="frkProcedureService">FRK orchestration service.</param>
    public AzureSqlDiagnosticTools(FrkProcedureService frkProcedureService)
    {
        _frkProcedureService = frkProcedureService;
    }

    /// <summary>
    /// Runs a health-check diagnostic through <c>sp_Blitz</c>.
    /// </summary>
    /// <param name="target">Optional target profile name.</param>
    /// <param name="databaseName">Optional database name override.</param>
    /// <param name="minimumPriority">Optional minimum priority threshold.</param>
    /// <param name="expertMode">Optional expert mode override.</param>
    /// <param name="maxRows">Optional max-row limit for compacted payloads.</param>
    /// <param name="includeVerboseResults">Optional verbose result inclusion flag.</param>
    /// <param name="request">Optional request object form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool response payload.</returns>
    [McpServerTool, Description("Run an Azure-safe SQL health check using sp_Blitz.")]
    public Task<object> AzureSqlHealthCheck(
        string? target = null,
        string? databaseName = null,
        int? minimumPriority = null,
        bool? expertMode = null,
        int? maxRows = null,
        bool? includeVerboseResults = null,
        AzureSqlHealthCheckRequest? request = null,
        CancellationToken cancellationToken = default)
        => _frkProcedureService.RunHealthCheckAsync(
            new AzureSqlHealthCheckRequest
            {
                Target = !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty,
                DatabaseName = request?.DatabaseName ?? databaseName,
                MinimumPriority = request?.MinimumPriority ?? minimumPriority,
                ExpertMode = request?.ExpertMode ?? expertMode ?? false,
                MaxRows = request?.MaxRows ?? maxRows ?? 25,
                IncludeVerboseResults = request?.IncludeVerboseResults ?? includeVerboseResults ?? false
            },
            cancellationToken);

    /// <summary>
    /// Runs plan-cache diagnostics through <c>sp_BlitzCache</c>.
    /// </summary>
    /// <param name="target">Optional target profile name.</param>
    /// <param name="databaseName">Optional database name override.</param>
    /// <param name="sortOrder">Optional sort order override.</param>
    /// <param name="top">Optional top-row input for FRK.</param>
    /// <param name="expertMode">Optional expert mode override.</param>
    /// <param name="aiMode">Optional AI mode override.</param>
    /// <param name="aiPromptConfigTable">Optional AI prompt config table name.</param>
    /// <param name="aiPromptName">Optional AI prompt name.</param>
    /// <param name="maxRows">Optional max-row limit for compacted payloads.</param>
    /// <param name="includeVerboseResults">Optional verbose result inclusion flag.</param>
    /// <param name="request">Optional request object form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool response payload.</returns>
    [McpServerTool, Description("Run sp_BlitzCache for the requested sort order and surface any FRK AI prompt or advice output.")]
    public async Task<object> AzureSqlBlitzCache(
        string? target = null,
        string? databaseName = null,
        string? sortOrder = null,
        int? top = null,
        bool? expertMode = null,
        int? aiMode = null,
        string? aiPromptConfigTable = null,
        string? aiPromptName = null,
        int? maxRows = null,
        bool? includeVerboseResults = null,
        AzureSqlBlitzCacheRequest? request = null,
        CancellationToken cancellationToken = default)
        => await _frkProcedureService.RunBlitzCacheAsync(
            new AzureSqlBlitzCacheRequest
            {
                Target = !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty,
                DatabaseName = request?.DatabaseName ?? databaseName,
                SortOrder = request?.SortOrder ?? sortOrder ?? "cpu",
                Top = request?.Top ?? top ?? 10,
                ExpertMode = request?.ExpertMode ?? expertMode ?? false,
                AiMode = ResolveAiMode(
                    !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty,
                    request?.AiMode,
                    aiMode),
                AiPromptConfigTable = request?.AiPromptConfigTable ?? aiPromptConfigTable,
                AiPromptName = request?.AiPromptName ?? aiPromptName,
                MaxRows = request?.MaxRows ?? maxRows ?? 50,
                IncludeVerboseResults = request?.IncludeVerboseResults ?? includeVerboseResults ?? false
            },
            cancellationToken);

    /// <summary>
    /// Runs single-table index diagnostics through <c>sp_BlitzIndex</c>.
    /// </summary>
    /// <param name="target">Optional target profile name.</param>
    /// <param name="databaseName">Optional database name override.</param>
    /// <param name="schemaName">Optional schema name override.</param>
    /// <param name="tableName">Optional table name override.</param>
    /// <param name="mode">Optional FRK mode override.</param>
    /// <param name="thresholdMb">Optional threshold override in MB.</param>
    /// <param name="expertMode">Optional expert mode override.</param>
    /// <param name="aiMode">Optional AI mode override.</param>
    /// <param name="aiPromptConfigTable">Optional AI prompt config table name.</param>
    /// <param name="aiPromptName">Optional AI prompt name.</param>
    /// <param name="maxRows">Optional max-row limit for compacted payloads.</param>
    /// <param name="includeVerboseResults">Optional verbose result inclusion flag.</param>
    /// <param name="request">Optional request object form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool response payload.</returns>
    [McpServerTool, Description("Run single-table sp_BlitzIndex analysis and surface any FRK AI prompt or advice output.")]
    public async Task<object> AzureSqlBlitzIndex(
        string? target = null,
        string? databaseName = null,
        string? schemaName = null,
        string? tableName = null,
        int? mode = null,
        int? thresholdMb = null,
        bool? expertMode = null,
        int? aiMode = null,
        string? aiPromptConfigTable = null,
        string? aiPromptName = null,
        int? maxRows = null,
        bool? includeVerboseResults = null,
        AzureSqlBlitzIndexRequest? request = null,
        CancellationToken cancellationToken = default)
        => await _frkProcedureService.RunBlitzIndexAsync(
            new AzureSqlBlitzIndexRequest
            {
                Target = !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty,
                DatabaseName = request?.DatabaseName ?? databaseName ?? string.Empty,
                SchemaName = request?.SchemaName ?? schemaName ?? "dbo",
                TableName = request?.TableName ?? tableName ?? string.Empty,
                Mode = request?.Mode ?? mode ?? 0,
                ThresholdMb = request?.ThresholdMb ?? thresholdMb ?? 250,
                ExpertMode = request?.ExpertMode ?? expertMode ?? false,
                AiMode = ResolveAiMode(
                    !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty,
                    request?.AiMode,
                    aiMode),
                AiPromptConfigTable = request?.AiPromptConfigTable ?? aiPromptConfigTable,
                AiPromptName = request?.AiPromptName ?? aiPromptName,
                MaxRows = request?.MaxRows ?? maxRows ?? 100,
                IncludeVerboseResults = request?.IncludeVerboseResults ?? includeVerboseResults ?? false
            },
            cancellationToken);

    /// <summary>
    /// Runs incident snapshot diagnostics through <c>sp_BlitzFirst</c>.
    /// </summary>
    /// <param name="target">Optional target profile name.</param>
    /// <param name="databaseName">Optional database name input.</param>
    /// <param name="expertMode">Optional expert mode override.</param>
    /// <param name="maxRows">Optional max-row limit for compacted payloads.</param>
    /// <param name="includeVerboseResults">Optional verbose result inclusion flag.</param>
    /// <param name="request">Optional request object form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool response payload.</returns>
    [McpServerTool, Description("Capture a current incident snapshot using sp_BlitzFirst.")]
    public Task<object> AzureSqlCurrentIncident(
        string? target = null,
        string? databaseName = null,
        bool? expertMode = null,
        int? maxRows = null,
        bool? includeVerboseResults = null,
        AzureSqlCurrentIncidentRequest? request = null,
        CancellationToken cancellationToken = default)
        => _frkProcedureService.RunCurrentIncidentAsync(
            new AzureSqlCurrentIncidentRequest
            {
                Target = !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty,
                DatabaseName = request?.DatabaseName ?? databaseName,
                ExpertMode = request?.ExpertMode ?? expertMode ?? false,
                MaxRows = request?.MaxRows ?? maxRows ?? 25,
                IncludeVerboseResults = request?.IncludeVerboseResults ?? includeVerboseResults ?? false
            },
            cancellationToken);

    /// <summary>
    /// Returns capability metadata for a configured target profile.
    /// </summary>
    /// <param name="target">Optional target profile name.</param>
    /// <param name="request">Optional request object form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool response payload.</returns>
    [McpServerTool, Description("Return installed FRK procedures, AI readiness, and target-level safety metadata for a configured profile.")]
    public async Task<object> AzureSqlTargetCapabilities(
        string? target = null,
        AzureSqlTargetCapabilitiesRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTarget = !string.IsNullOrWhiteSpace(request?.Target)
            ? request.Target
            : target ?? string.Empty;

        return await _frkProcedureService.RunTargetCapabilitiesAsync(
            new AzureSqlTargetCapabilitiesRequest
            {
                Target = effectiveTarget
            },
            cancellationToken);
    }

    private int ResolveAiMode(string target, int? requestAiMode, int? scalarAiMode)
    {
        if (requestAiMode.HasValue)
        {
            return requestAiMode.Value;
        }

        if (scalarAiMode.HasValue)
        {
            return scalarAiMode.Value;
        }

        return _frkProcedureService.GetConfiguredAiMode(target);
    }
}
