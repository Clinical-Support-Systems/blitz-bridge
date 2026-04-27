using System.Text.RegularExpressions;

using BlitzBridge.McpServer.Models.ToolRequests;
using BlitzBridge.McpServer.Models.ToolResponses;
using Microsoft.Data.SqlClient;

namespace BlitzBridge.McpServer.Services;

/// <summary>
/// Orchestrates validated FRK procedure calls for MCP tool handlers.
/// </summary>
public sealed partial class FrkProcedureService
{
    private static readonly string[] AllowedSortOrders = ["cpu", "duration", "executions", "reads"];

    private readonly ISqlExecutionService _sqlExecutionService;
    private readonly FrkResultMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrkProcedureService"/> class.
    /// </summary>
    /// <param name="sqlExecutionService">SQL execution abstraction.</param>
    /// <param name="mapper">Result mapper for tool responses.</param>
    public FrkProcedureService(
        ISqlExecutionService sqlExecutionService,
        FrkResultMapper mapper)
    {
        _sqlExecutionService = sqlExecutionService;
        _mapper = mapper;
    }

    /// <summary>
    /// Runs <c>sp_Blitz</c> and maps the result to a health check response payload.
    /// </summary>
    /// <param name="request">Tool request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mapped health check response.</returns>
    public async Task<object> RunHealthCheckAsync(
        AzureSqlHealthCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(request.Target);
        ValidateMaxRows(request.MaxRows);
        ValidateOptionalIdentifier(request.DatabaseName, nameof(request.DatabaseName));

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

        var dataSet = await _sqlExecutionService.ExecuteStoredProcedureAsync(
            request.Target,
            "sp_Blitz",
            request.DatabaseName,
            parameters,
            cancellationToken);

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
        ValidateTarget(request.Target);
        ValidateSortOrder(request.SortOrder);
        ValidateTop(request.Top);
        ValidateMaxRows(request.MaxRows);
        ValidateAiMode(request.AiMode);
        ValidateOptionalIdentifier(request.DatabaseName, nameof(request.DatabaseName));
        ValidateOptionalQualifiedName(request.AiPromptConfigTable, nameof(request.AiPromptConfigTable));
        ValidateOptionalIdentifier(request.AiPromptName, nameof(request.AiPromptName));

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

        var dataSet = await _sqlExecutionService.ExecuteStoredProcedureAsync(
            request.Target,
            "sp_BlitzCache",
            request.DatabaseName,
            parameters,
            cancellationToken);

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
        ValidateTarget(request.Target);
        ValidateRequiredIdentifier(request.DatabaseName, nameof(request.DatabaseName));
        ValidateRequiredIdentifier(request.SchemaName, nameof(request.SchemaName));
        ValidateRequiredIdentifier(request.TableName, nameof(request.TableName));
        ValidateThresholdMb(request.ThresholdMb);
        ValidateMaxRows(request.MaxRows);
        ValidateAiMode(request.AiMode);
        ValidateOptionalQualifiedName(request.AiPromptConfigTable, nameof(request.AiPromptConfigTable));
        ValidateOptionalIdentifier(request.AiPromptName, nameof(request.AiPromptName));

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

        var dataSet = await _sqlExecutionService.ExecuteStoredProcedureAsync(
            request.Target,
            "sp_BlitzIndex",
            request.DatabaseName,
            parameters,
            cancellationToken);

        return _mapper.MapBlitzIndex(request, dataSet);
    }

    /// <summary>
    /// Runs <c>sp_BlitzFirst</c> and maps the result to an incident snapshot payload.
    /// </summary>
    /// <param name="request">Tool request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mapped current-incident response.</returns>
    public async Task<object> RunCurrentIncidentAsync(
        AzureSqlCurrentIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTarget(request.Target);
        ValidateMaxRows(request.MaxRows);

        if (!string.IsNullOrWhiteSpace(request.DatabaseName))
        {
            throw new ArgumentException(
                "sp_BlitzFirst runs in the target's current database context and does not support DatabaseName filtering in this server.");
        }

        var parameters = new List<SqlParameter>();

        if (request.ExpertMode)
        {
            parameters.Add(new SqlParameter("@ExpertMode", 1));
        }

        var dataSet = await _sqlExecutionService.ExecuteStoredProcedureAsync(
            request.Target,
            "sp_BlitzFirst",
            null,
            parameters,
            cancellationToken);

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
        ValidateTarget(request.Target);

        var capabilities = await _sqlExecutionService.GetTargetCapabilitiesAsync(
            request.Target,
            cancellationToken);

        return _mapper.MapTargetCapabilities(capabilities);
    }

    /// <summary>
    /// Gets the configured default AI mode for a target profile.
    /// </summary>
    /// <param name="target">Target profile name.</param>
    /// <returns>Configured AI mode.</returns>
    public int GetConfiguredAiMode(string target)
    {
        ValidateTarget(target);
        return _sqlExecutionService.GetConfiguredAiMode(target);
    }

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
