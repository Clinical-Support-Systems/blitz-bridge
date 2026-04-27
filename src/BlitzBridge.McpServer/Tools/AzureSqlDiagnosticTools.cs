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
    [McpServerTool, Description("Run an Azure-safe SQL health check using sp_Blitz. IncludeVerboseResults is deprecated; prefer azure_sql_fetch_detail_by_handle for expanded sections.")]
    public async Task<object> AzureSqlHealthCheck(
        string? target = null,
        string? databaseName = null,
        int? minimumPriority = null,
        bool? expertMode = null,
        int? maxRows = null,
        bool? includeVerboseResults = null,
        AzureSqlHealthCheckRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var toolRequest = new AzureSqlHealthCheckRequest
        {
            Target = !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty,
            DatabaseName = request?.DatabaseName ?? databaseName,
            MinimumPriority = request?.MinimumPriority ?? minimumPriority,
            ExpertMode = request?.ExpertMode ?? expertMode ?? false,
            MaxRows = request?.MaxRows ?? maxRows ?? 25,
            IncludeVerboseResults = request?.IncludeVerboseResults ?? includeVerboseResults ?? false
        };

        var response = await _frkProcedureService.RunHealthCheckAsync(toolRequest, cancellationToken);
        return ResponseTelemetry.Capture("azure_sql_health_check", toolRequest.Target, toolRequest.IncludeVerboseResults, response);
    }

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
    [McpServerTool, Description("Run sp_BlitzCache for the requested sort order and surface any FRK AI prompt or advice output. IncludeVerboseResults is deprecated; prefer azure_sql_fetch_detail_by_handle for expanded sections.")]
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
    {
        var effectiveTarget = !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty;
        var toolRequest = new AzureSqlBlitzCacheRequest
        {
            Target = effectiveTarget,
            DatabaseName = request?.DatabaseName ?? databaseName,
            SortOrder = request?.SortOrder ?? sortOrder ?? "cpu",
            Top = request?.Top ?? top ?? 10,
            ExpertMode = request?.ExpertMode ?? expertMode ?? false,
            AiMode = ResolveAiMode(
                effectiveTarget,
                request?.AiMode,
                aiMode),
            AiPromptConfigTable = request?.AiPromptConfigTable ?? aiPromptConfigTable,
            AiPromptName = request?.AiPromptName ?? aiPromptName,
            MaxRows = request?.MaxRows ?? maxRows ?? 50,
            IncludeVerboseResults = request?.IncludeVerboseResults ?? includeVerboseResults ?? false
        };

        var response = await _frkProcedureService.RunBlitzCacheAsync(toolRequest, cancellationToken);
        return ResponseTelemetry.Capture("azure_sql_blitz_cache", toolRequest.Target, toolRequest.IncludeVerboseResults, response);
    }

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
    [McpServerTool, Description("Run single-table sp_BlitzIndex analysis and surface any FRK AI prompt or advice output. IncludeVerboseResults is deprecated; prefer azure_sql_fetch_detail_by_handle for expanded sections.")]
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
    {
        var effectiveTarget = !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty;
        var toolRequest = new AzureSqlBlitzIndexRequest
        {
            Target = effectiveTarget,
            DatabaseName = request?.DatabaseName ?? databaseName ?? string.Empty,
            SchemaName = request?.SchemaName ?? schemaName ?? "dbo",
            TableName = request?.TableName ?? tableName ?? string.Empty,
            Mode = request?.Mode ?? mode ?? 0,
            ThresholdMb = request?.ThresholdMb ?? thresholdMb ?? 250,
            ExpertMode = request?.ExpertMode ?? expertMode ?? false,
            AiMode = ResolveAiMode(
                effectiveTarget,
                request?.AiMode,
                aiMode),
            AiPromptConfigTable = request?.AiPromptConfigTable ?? aiPromptConfigTable,
            AiPromptName = request?.AiPromptName ?? aiPromptName,
            MaxRows = request?.MaxRows ?? maxRows ?? 100,
            IncludeVerboseResults = request?.IncludeVerboseResults ?? includeVerboseResults ?? false
        };

        var response = await _frkProcedureService.RunBlitzIndexAsync(toolRequest, cancellationToken);
        return ResponseTelemetry.Capture("azure_sql_blitz_index", toolRequest.Target, toolRequest.IncludeVerboseResults, response);
    }

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
    [McpServerTool, Description("Capture a current incident snapshot using sp_BlitzFirst. IncludeVerboseResults is deprecated; prefer azure_sql_fetch_detail_by_handle for expanded sections.")]
    public async Task<object> AzureSqlCurrentIncident(
        string? target = null,
        string? databaseName = null,
        bool? expertMode = null,
        int? maxRows = null,
        bool? includeVerboseResults = null,
        AzureSqlCurrentIncidentRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var toolRequest = new AzureSqlCurrentIncidentRequest
        {
            Target = !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty,
            DatabaseName = request?.DatabaseName ?? databaseName,
            ExpertMode = request?.ExpertMode ?? expertMode ?? false,
            MaxRows = request?.MaxRows ?? maxRows ?? 25,
            IncludeVerboseResults = request?.IncludeVerboseResults ?? includeVerboseResults ?? false
        };

        var response = await _frkProcedureService.RunCurrentIncidentAsync(toolRequest, cancellationToken);
        return ResponseTelemetry.Capture("azure_sql_current_incident", toolRequest.Target, toolRequest.IncludeVerboseResults, response);
    }

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

        var response = await _frkProcedureService.RunTargetCapabilitiesAsync(
            new AzureSqlTargetCapabilitiesRequest
            {
                Target = effectiveTarget
            },
            cancellationToken);

        return ResponseTelemetry.Capture("azure_sql_target_capabilities", effectiveTarget, false, response);
    }

    /// <summary>
    /// Fetches expanded detail for a previously returned progressive-disclosure handle.
    /// </summary>
    /// <param name="target">Optional target profile name.</param>
    /// <param name="parentTool">Optional parent tool name.</param>
    /// <param name="kind">Optional detail kind.</param>
    /// <param name="handle">Optional opaque handle.</param>
    /// <param name="maxRows">Optional maximum rows for tabular payloads.</param>
    /// <param name="request">Optional request object form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Expanded detail payload.</returns>
    [McpServerTool, Description("Fetch expanded detail for a summary handle. parentTool and kind are required explicit dispatch inputs and must match the opaque handle.")]
    public async Task<object> AzureSqlFetchDetailByHandle(
        string? target = null,
        string? parentTool = null,
        string? kind = null,
        string? handle = null,
        int? maxRows = null,
        AzureSqlFetchDetailByHandleRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var toolRequest = new AzureSqlFetchDetailByHandleRequest
        {
            Target = !string.IsNullOrWhiteSpace(request?.Target) ? request.Target : target ?? string.Empty,
            ParentTool = !string.IsNullOrWhiteSpace(request?.ParentTool) ? request.ParentTool : parentTool ?? string.Empty,
            Kind = !string.IsNullOrWhiteSpace(request?.Kind) ? request.Kind : kind ?? string.Empty,
            Handle = !string.IsNullOrWhiteSpace(request?.Handle) ? request.Handle : handle ?? string.Empty,
            MaxRows = request?.MaxRows ?? maxRows ?? 100
        };

        var response = await _frkProcedureService.FetchDetailByHandleAsync(toolRequest, cancellationToken);
        return ResponseTelemetry.Capture("azure_sql_fetch_detail_by_handle", toolRequest.Target, false, response);
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
