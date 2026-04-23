using System.ComponentModel;

using BlitzBridge.McpServer.Models.ToolRequests;
using BlitzBridge.McpServer.Services;
using ModelContextProtocol.Server;

namespace BlitzBridge.McpServer.Tools;

[McpServerToolType]
public sealed class AzureSqlDiagnosticTools
{
    private readonly FrkProcedureService _frkProcedureService;

    public AzureSqlDiagnosticTools(FrkProcedureService frkProcedureService)
    {
        _frkProcedureService = frkProcedureService;
    }

    [McpServerTool, Description("Run an Azure-safe SQL health check using sp_Blitz.")]
    public Task<object> AzureSqlHealthCheck(
        AzureSqlHealthCheckRequest request,
        CancellationToken cancellationToken = default)
        => _frkProcedureService.RunHealthCheckAsync(request, cancellationToken);

    [McpServerTool, Description("Run sp_BlitzCache for the requested sort order and surface any FRK AI prompt or advice output.")]
    public async Task<object> AzureSqlBlitzCache(
        AzureSqlBlitzCacheRequest request,
        CancellationToken cancellationToken = default)
        => await _frkProcedureService.RunBlitzCacheAsync(request, cancellationToken);

    [McpServerTool, Description("Run single-table sp_BlitzIndex analysis and surface any FRK AI prompt or advice output.")]
    public async Task<object> AzureSqlBlitzIndex(
        AzureSqlBlitzIndexRequest request,
        CancellationToken cancellationToken = default)
        => await _frkProcedureService.RunBlitzIndexAsync(request, cancellationToken);

    [McpServerTool, Description("Capture a current incident snapshot using sp_BlitzFirst.")]
    public Task<object> AzureSqlCurrentIncident(
        AzureSqlCurrentIncidentRequest request,
        CancellationToken cancellationToken = default)
        => _frkProcedureService.RunCurrentIncidentAsync(request, cancellationToken);

    [McpServerTool, Description("Return installed FRK procedures, AI readiness, and target-level safety metadata for a configured profile.")]
    public async Task<object> AzureSqlTargetCapabilities(
        AzureSqlTargetCapabilitiesRequest request,
        CancellationToken cancellationToken = default)
        => await _frkProcedureService.RunTargetCapabilitiesAsync(request, cancellationToken);
}
