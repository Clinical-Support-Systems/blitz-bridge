using System.Data;
using System.Text.RegularExpressions;

using BlitzBridge.McpServer.Configuration;
using BlitzBridge.McpServer.Models.ToolRequests;
using BlitzBridge.McpServer.Models.ToolResponses;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace BlitzBridge.McpServer.Services;

/// <summary>
/// Orchestrates validated FRK procedure calls for MCP tool handlers.
/// </summary>
public sealed partial class FrkProcedureService
{
    private static readonly string[] AllowedSortOrders = ["cpu", "duration", "executions", "reads"];
    private static readonly string[] ParentTools =
    [
        "azure_sql_health_check",
        "azure_sql_blitz_cache",
        "azure_sql_blitz_index",
        "azure_sql_current_incident"
    ];

    private static readonly Dictionary<string, string[]> ValidKindsByParentTool = new(StringComparer.OrdinalIgnoreCase)
    {
        ["azure_sql_health_check"] = ["findings"],
        ["azure_sql_blitz_cache"] = ["queries", "warning_glossary", "ai_prompt", "ai_advice"],
        ["azure_sql_blitz_index"] = ["existing_indexes", "missing_indexes", "column_data_types", "foreign_keys", "ai_prompt", "ai_advice"],
        ["azure_sql_current_incident"] = ["waits", "findings"]
    };

    private readonly ISqlExecutionService _sqlExecutionService;
    private readonly FrkResultMapper _mapper;
    private readonly IOptionsMonitor<SqlTargetOptions> _targetOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrkProcedureService"/> class.
    /// </summary>
    /// <param name="sqlExecutionService">SQL execution abstraction.</param>
    /// <param name="mapper">Result mapper for tool responses.</param>
    /// <param name="targetOptions">Target profile options snapshot.</param>
    public FrkProcedureService(
        ISqlExecutionService sqlExecutionService,
        FrkResultMapper mapper,
        IOptionsMonitor<SqlTargetOptions> targetOptions)
    {
        _sqlExecutionService = sqlExecutionService;
        _mapper = mapper;
        _targetOptions = targetOptions;
    }

    /// <summary>
    /// Resolves a tool-supplied target value. When the input is blank and exactly one
    /// enabled profile is configured, the sole profile name is returned. Otherwise an
    /// <see cref="ArgumentException"/> with the available profile list is thrown.
    /// </summary>
    /// <param name="target">Caller-supplied target value (may be null/blank).</param>
    /// <returns>Resolved target profile name.</returns>
    public string ResolveTarget(string? target)
    {
        if (!string.IsNullOrWhiteSpace(target))
        {
            return target;
        }

        var profiles = _targetOptions.CurrentValue.Profiles ?? new();
        var enabled = profiles
            .Where(p => p.Value is { Enabled: true })
            .Select(p => p.Key)
            .ToArray();

        if (enabled.Length == 1)
        {
            return enabled[0];
        }

        var available = enabled.Length == 0
            ? "(none configured)"
            : string.Join(", ", enabled);
        throw new ArgumentException(
            $"Target is required. Available profiles: {available}.");
    }

    /// <summary>
    /// Runs <c>sp_Blitz</c> and maps the result to a health check response payload.
    /// </summary>
    /// <param name="request">Tool request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mapped health check response.</returns>
    public async Task<AzureSqlHealthCheckResponse> RunHealthCheckAsync(
        AzureSqlHealthCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Target = ResolveTarget(request.Target);
        ValidateHealthCheckRequest(request);
        var dataSet = await ExecuteHealthCheckAsync(request, cancellationToken);
        return _mapper.MapHealthCheck(request, dataSet);
    }

    /// <summary>
    /// Runs <c>sp_BlitzCache</c> and maps the result.
    /// </summary>
    /// <param name="request">Tool request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mapped BlitzCache response.</returns>
    public async Task<AzureSqlBlitzCacheResponse> RunBlitzCacheAsync(
        AzureSqlBlitzCacheRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Target = ResolveTarget(request.Target);
        ValidateBlitzCacheRequest(request);
        var dataSet = await ExecuteBlitzCacheAsync(request, cancellationToken);
        return _mapper.MapBlitzCache(request, dataSet);
    }

    /// <summary>
    /// Runs <c>sp_BlitzIndex</c> for a single table and maps the result.
    /// </summary>
    /// <param name="request">Tool request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mapped BlitzIndex response.</returns>
    public async Task<AzureSqlBlitzIndexResponse> RunBlitzIndexAsync(
        AzureSqlBlitzIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Target = ResolveTarget(request.Target);
        ValidateBlitzIndexRequest(request);
        var dataSet = await ExecuteBlitzIndexAsync(request, cancellationToken);
        return _mapper.MapBlitzIndex(request, dataSet);
    }

    /// <summary>
    /// Runs <c>sp_BlitzFirst</c> and maps the result to an incident snapshot payload.
    /// </summary>
    /// <param name="request">Tool request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mapped current-incident response.</returns>
    public async Task<AzureSqlCurrentIncidentResponse> RunCurrentIncidentAsync(
        AzureSqlCurrentIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Target = ResolveTarget(request.Target);
        ValidateCurrentIncidentRequest(request);
        var dataSet = await ExecuteCurrentIncidentAsync(request, cancellationToken);
        return _mapper.MapCurrentIncident(request, dataSet);
    }

    /// <summary>
    /// Retrieves target capability metadata and maps it for tool output.
    /// </summary>
    /// <param name="request">Tool request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mapped target capabilities response.</returns>
    public async Task<AzureSqlTargetCapabilitiesResponse> RunTargetCapabilitiesAsync(
        AzureSqlTargetCapabilitiesRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Target = ResolveTarget(request.Target);
        ValidateTarget(request.Target);

        var capabilities = await _sqlExecutionService.GetTargetCapabilitiesAsync(
            request.Target,
            cancellationToken);

        return _mapper.MapTargetCapabilities(capabilities);
    }

    /// <summary>
    /// Re-runs the underlying FRK procedure and returns expanded section detail.
    /// </summary>
    /// <param name="request">Detail-fetch request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Expanded detail payload.</returns>
    public async Task<AzureSqlFetchDetailByHandleResponse> FetchDetailByHandleAsync(
        AzureSqlFetchDetailByHandleRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Target = ResolveTarget(request.Target);
        ValidateDetailFetchRequest(request);

        var handle = ProgressiveDisclosureHandleCodec.Decode(request.Handle);
        ValidateHandleMatchesRequest(request, handle);

        try
        {
            return handle.ParentTool switch
            {
                "azure_sql_health_check" => await FetchHealthCheckDetailAsync(request.Handle, handle, request.MaxRows, cancellationToken),
                "azure_sql_blitz_cache" => await FetchBlitzCacheDetailAsync(request.Handle, handle, request.MaxRows, cancellationToken),
                "azure_sql_blitz_index" => await FetchBlitzIndexDetailAsync(request.Handle, handle, request.MaxRows, cancellationToken),
                "azure_sql_current_incident" => await FetchCurrentIncidentDetailAsync(request.Handle, handle, request.MaxRows, cancellationToken),
                _ => throw CreateUnknownParentToolException(handle.ParentTool)
            };
        }
        catch (ProgressiveDisclosureException)
        {
            throw;
        }
        catch (InvalidOperationException ex) when (IsAccessDeniedFailure(ex))
        {
            throw new ProgressiveDisclosureException(
                "access_denied",
                $"Access to target '{handle.Target}' is not available. This may indicate the profile was disabled or your authorization has changed since the summary call.",
                403,
                ex);
        }
        catch (SqlException ex)
        {
            throw new ProgressiveDisclosureException(
                "sql_execution_error",
                $"Failed to execute detail request: {ex.Message}",
                500,
                ex);
        }
        catch (TimeoutException ex)
        {
            throw new ProgressiveDisclosureException(
                "sql_execution_error",
                $"Failed to execute detail request: {ex.Message}",
                504,
                ex);
        }
    }

    /// <summary>
    /// Gets the configured default AI mode for a target profile.
    /// </summary>
    /// <param name="target">Target profile name.</param>
    /// <returns>Configured AI mode.</returns>
    public int GetConfiguredAiMode(string target)
    {
        var resolved = ResolveTarget(target);
        return _sqlExecutionService.GetConfiguredAiMode(resolved);
    }

    private async Task<AzureSqlFetchDetailByHandleResponse> FetchHealthCheckDetailAsync(
        string requestHandle,
        ProgressiveDisclosureHandlePayload handle,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var request = new AzureSqlHealthCheckRequest
        {
            Target = handle.Target,
            DatabaseName = handle.DatabaseName,
            MinimumPriority = handle.MinimumPriority,
            ExpertMode = handle.ExpertMode ?? false,
            MaxRows = maxRows
        };

        ValidateHealthCheckRequest(request);
        var dataSet = await ExecuteHealthCheckAsync(request, cancellationToken);
        return _mapper.MapHealthCheckDetail(handle, request.MaxRows, requestHandle, dataSet);
    }

    private async Task<AzureSqlFetchDetailByHandleResponse> FetchBlitzCacheDetailAsync(
        string requestHandle,
        ProgressiveDisclosureHandlePayload handle,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var request = new AzureSqlBlitzCacheRequest
        {
            Target = handle.Target,
            DatabaseName = handle.DatabaseName,
            SortOrder = handle.SortOrder ?? "cpu",
            Top = handle.Top ?? 10,
            ExpertMode = handle.ExpertMode ?? false,
            AiMode = handle.AiMode ?? GetConfiguredAiMode(handle.Target),
            AiPromptConfigTable = handle.AiPromptConfigTable,
            AiPromptName = handle.AiPromptName,
            MaxRows = maxRows
        };

        ValidateBlitzCacheRequest(request);
        var dataSet = await ExecuteBlitzCacheAsync(request, cancellationToken);
        return _mapper.MapBlitzCacheDetail(handle, request.MaxRows, requestHandle, dataSet);
    }

    private async Task<AzureSqlFetchDetailByHandleResponse> FetchBlitzIndexDetailAsync(
        string requestHandle,
        ProgressiveDisclosureHandlePayload handle,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var request = new AzureSqlBlitzIndexRequest
        {
            Target = handle.Target,
            DatabaseName = handle.DatabaseName ?? string.Empty,
            SchemaName = handle.SchemaName ?? "dbo",
            TableName = handle.TableName ?? string.Empty,
            Mode = handle.Mode ?? 0,
            ThresholdMb = handle.ThresholdMb ?? 250,
            ExpertMode = handle.ExpertMode ?? false,
            AiMode = handle.AiMode ?? GetConfiguredAiMode(handle.Target),
            AiPromptConfigTable = handle.AiPromptConfigTable,
            AiPromptName = handle.AiPromptName,
            MaxRows = maxRows
        };

        ValidateBlitzIndexRequest(request);
        var dataSet = await ExecuteBlitzIndexAsync(request, cancellationToken);
        return _mapper.MapBlitzIndexDetail(handle, request.MaxRows, requestHandle, dataSet);
    }

    private async Task<AzureSqlFetchDetailByHandleResponse> FetchCurrentIncidentDetailAsync(
        string requestHandle,
        ProgressiveDisclosureHandlePayload handle,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var request = new AzureSqlCurrentIncidentRequest
        {
            Target = handle.Target,
            ExpertMode = handle.ExpertMode ?? false,
            MaxRows = maxRows
        };

        ValidateCurrentIncidentRequest(request);
        var dataSet = await ExecuteCurrentIncidentAsync(request, cancellationToken);
        return _mapper.MapCurrentIncidentDetail(handle, request.MaxRows, requestHandle, dataSet);
    }

    private async Task<DataSet> ExecuteHealthCheckAsync(
        AzureSqlHealthCheckRequest request,
        CancellationToken cancellationToken)
    {
        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(request.DatabaseName))
        {
            parameters.Add(new SqlParameter("@DatabaseName", request.DatabaseName));
        }

        if (request.MinimumPriority.HasValue)
        {
            parameters.Add(new SqlParameter("@IgnorePrioritiesAbove", request.MinimumPriority.Value));
        }

        if (request.ExpertMode)
        {
            parameters.Add(new SqlParameter("@ExpertMode", 1));
        }

        return await _sqlExecutionService.ExecuteStoredProcedureAsync(
            request.Target,
            "sp_Blitz",
            request.DatabaseName,
            parameters,
            cancellationToken);
    }

    private async Task<DataSet> ExecuteBlitzCacheAsync(
        AzureSqlBlitzCacheRequest request,
        CancellationToken cancellationToken)
    {
        var parameters = new List<SqlParameter>
        {
            new("@Top", request.Top),
            new("@SortOrder", request.SortOrder)
        };

        if (!string.IsNullOrWhiteSpace(request.DatabaseName))
        {
            parameters.Add(new SqlParameter("@DatabaseName", request.DatabaseName));
        }

        if (request.ExpertMode)
        {
            parameters.Add(new SqlParameter("@ExpertMode", 1));
        }

        if (request.AiMode > 0)
        {
            parameters.Add(new SqlParameter("@AI", request.AiMode));
        }

        if (!string.IsNullOrWhiteSpace(request.AiPromptConfigTable))
        {
            parameters.Add(new SqlParameter("@AIPromptConfigTable", request.AiPromptConfigTable));
        }

        if (!string.IsNullOrWhiteSpace(request.AiPromptName))
        {
            parameters.Add(new SqlParameter("@AIPrompt", request.AiPromptName));
        }

        return await _sqlExecutionService.ExecuteStoredProcedureAsync(
            request.Target,
            "sp_BlitzCache",
            request.DatabaseName,
            parameters,
            cancellationToken);
    }

    private async Task<DataSet> ExecuteBlitzIndexAsync(
        AzureSqlBlitzIndexRequest request,
        CancellationToken cancellationToken)
    {
        var parameters = new List<SqlParameter>
        {
            new("@DatabaseName", request.DatabaseName),
            new("@SchemaName", request.SchemaName),
            new("@TableName", request.TableName),
            new("@Mode", request.Mode),
            new("@ThresholdMB", request.ThresholdMb)
        };

        if (request.ExpertMode)
        {
            parameters.Add(new SqlParameter("@ExpertMode", 1));
        }

        if (request.AiMode > 0)
        {
            parameters.Add(new SqlParameter("@AI", request.AiMode));
        }

        if (!string.IsNullOrWhiteSpace(request.AiPromptConfigTable))
        {
            parameters.Add(new SqlParameter("@AIPromptConfigTable", request.AiPromptConfigTable));
        }

        if (!string.IsNullOrWhiteSpace(request.AiPromptName))
        {
            parameters.Add(new SqlParameter("@AIPrompt", request.AiPromptName));
        }

        return await _sqlExecutionService.ExecuteStoredProcedureAsync(
            request.Target,
            "sp_BlitzIndex",
            request.DatabaseName,
            parameters,
            cancellationToken);
    }

    private async Task<DataSet> ExecuteCurrentIncidentAsync(
        AzureSqlCurrentIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var parameters = new List<SqlParameter>();

        if (request.ExpertMode)
        {
            parameters.Add(new SqlParameter("@ExpertMode", 1));
        }

        return await _sqlExecutionService.ExecuteStoredProcedureAsync(
            request.Target,
            "sp_BlitzFirst",
            null,
            parameters,
            cancellationToken);
    }

    private static void ValidateHealthCheckRequest(AzureSqlHealthCheckRequest request)
    {
        ValidateTarget(request.Target);
        ValidateMaxRows(request.MaxRows);
        ValidateOptionalIdentifier(request.DatabaseName, nameof(request.DatabaseName));
    }

    private static void ValidateBlitzCacheRequest(AzureSqlBlitzCacheRequest request)
    {
        ValidateTarget(request.Target);
        ValidateSortOrder(request.SortOrder);
        ValidateTop(request.Top);
        ValidateMaxRows(request.MaxRows);
        ValidateAiMode(request.AiMode);
        ValidateOptionalIdentifier(request.DatabaseName, nameof(request.DatabaseName));
        ValidateOptionalQualifiedName(request.AiPromptConfigTable, nameof(request.AiPromptConfigTable));
        ValidateOptionalIdentifier(request.AiPromptName, nameof(request.AiPromptName));
    }

    private static void ValidateBlitzIndexRequest(AzureSqlBlitzIndexRequest request)
    {
        ValidateTarget(request.Target);
        ValidateRequiredIdentifier(request.DatabaseName, nameof(request.DatabaseName));
        ValidateRequiredIdentifier(request.SchemaName, nameof(request.SchemaName));
        ValidateRequiredIdentifier(request.TableName, nameof(request.TableName));
        ValidateThresholdMb(request.ThresholdMb);
        ValidateMaxRows(request.MaxRows);
        ValidateAiMode(request.AiMode);
        ValidateOptionalQualifiedName(request.AiPromptConfigTable, nameof(request.AiPromptConfigTable));
        ValidateOptionalIdentifier(request.AiPromptName, nameof(request.AiPromptName));
    }

    private static void ValidateCurrentIncidentRequest(AzureSqlCurrentIncidentRequest request)
    {
        ValidateTarget(request.Target);
        ValidateMaxRows(request.MaxRows);

        if (!string.IsNullOrWhiteSpace(request.DatabaseName))
        {
            throw new ArgumentException(
                "sp_BlitzFirst runs in the target's current database context and does not support DatabaseName filtering in this server.");
        }
    }

    private static void ValidateDetailFetchRequest(AzureSqlFetchDetailByHandleRequest request)
    {
        ValidateTarget(request.Target);
        ValidateMaxRows(request.MaxRows);
        ValidateRequiredDetailDispatchValue(request.ParentTool, nameof(request.ParentTool));
        ValidateRequiredDetailDispatchValue(request.Kind, nameof(request.Kind));
        ValidateRequiredDetailDispatchValue(request.Handle, nameof(request.Handle));
        ValidateParentTool(request.ParentTool);
        ValidateKind(request.ParentTool, request.Kind);
    }

    private static void ValidateRequiredDetailDispatchValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProgressiveDisclosureException(
                "invalid_request",
                $"{parameterName} is required.",
                400);
        }
    }

    private static void ValidateParentTool(string parentTool)
    {
        if (ParentTools.Contains(parentTool, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw CreateUnknownParentToolException(parentTool);
    }

    private static void ValidateKind(string parentTool, string kind)
    {
        if (ValidKindsByParentTool.TryGetValue(parentTool, out var kinds)
            && kinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw CreateUnknownKindException(parentTool, kind);
    }

    private static void ValidateHandleMatchesRequest(
        AzureSqlFetchDetailByHandleRequest request,
        ProgressiveDisclosureHandlePayload handle)
    {
        if (!string.Equals(request.Target, handle.Target, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.ParentTool, handle.ParentTool, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.Kind, handle.Kind, StringComparison.OrdinalIgnoreCase))
        {
            throw new ProgressiveDisclosureException(
                "malformed_handle",
                "Handle dispatch metadata does not match the requested target, parentTool, and kind. Re-run the parent tool and use the returned handle unchanged.",
                400);
        }
    }

    private static ProgressiveDisclosureException CreateUnknownParentToolException(string parentTool)
        => new(
            "unknown_parent_tool",
            $"Unknown parentTool: '{parentTool}'. Valid tools: {string.Join(", ", ParentTools)}",
            400);

    private static ProgressiveDisclosureException CreateUnknownKindException(string parentTool, string kind)
    {
        var validKinds = ValidKindsByParentTool.TryGetValue(parentTool, out var kinds)
            ? kinds
            : [];

        return new ProgressiveDisclosureException(
            "unknown_kind",
            $"Unknown kind '{kind}' for parentTool '{parentTool}'. Valid kinds: {string.Join(", ", validKinds)}",
            400);
    }

    private static bool IsAccessDeniedFailure(InvalidOperationException exception)
        => exception.Message.Contains("Unknown or disabled target", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("not allowed", StringComparison.OrdinalIgnoreCase);

    private static void ValidateTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("Target is required.");
        }
    }

    private static void ValidateSortOrder(string sortOrder)
    {
        if (!AllowedSortOrders.Contains(sortOrder, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Unsupported sort order '{sortOrder}'. Allowed values: {string.Join(", ", AllowedSortOrders)}.");
        }
    }

    private static void ValidateTop(int top)
    {
        if (top < 1 || top > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(top), "Top must be between 1 and 50.");
        }
    }

    private static void ValidateThresholdMb(int thresholdMb)
    {
        if (thresholdMb < 1 || thresholdMb > 102400)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdMb), "ThresholdMb must be between 1 and 102400.");
        }
    }

    private static void ValidateAiMode(int aiMode)
    {
        if (aiMode is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(aiMode), "AiMode must be 0, 1, or 2.");
        }
    }

    private static void ValidateMaxRows(int maxRows)
    {
        if (maxRows < 1 || maxRows > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRows), "MaxRows must be between 1 and 500.");
        }
    }

    private static void ValidateRequiredIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        ValidateIdentifier(value, parameterName);
    }

    private static void ValidateOptionalIdentifier(string? value, string parameterName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ValidateIdentifier(value, parameterName);
        }
    }

    private static void ValidateOptionalQualifiedName(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!QualifiedNameRegex().IsMatch(value))
        {
            throw new ArgumentException(
                $"{parameterName} must contain only letters, numbers, underscores, brackets, and dots.",
                parameterName);
        }
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (!IdentifierRegex().IsMatch(value))
        {
            throw new ArgumentException(
                $"{parameterName} must contain only letters, numbers, underscores, hyphens, or dollar signs.",
                parameterName);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_\\-$]+$", RegexOptions.Compiled)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("^[A-Za-z0-9_\\-\\[\\]$.]+$", RegexOptions.Compiled)]
    private static partial Regex QualifiedNameRegex();
}
