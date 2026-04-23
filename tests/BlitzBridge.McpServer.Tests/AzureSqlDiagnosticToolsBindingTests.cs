using System.Data;
using System.Text.Json;

using BlitzBridge.McpServer.Models;
using BlitzBridge.McpServer.Models.ToolRequests;
using BlitzBridge.McpServer.Models.ToolResponses;
using BlitzBridge.McpServer.Services;
using BlitzBridge.McpServer.Tools;
using Microsoft.Data.SqlClient;

namespace BlitzBridge.McpServer.Tests;

public class AzureSqlDiagnosticToolsBindingTests
{
    [Test]
    public async Task TargetCapabilities_UsesTopLevelTarget_WhenRequestObjectIsMissing()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = new AzureSqlDiagnosticTools(new FrkProcedureService(sqlService, new FrkResultMapper()));

        var result = await tools.AzureSqlTargetCapabilities(target: "primary-sql-target");

        await Assert.That(sqlService.LastCapabilitiesTarget).IsEqualTo("primary-sql-target");
        var response = (AzureSqlTargetCapabilitiesResponse)result;
        await Assert.That(response.Target).IsEqualTo("primary-sql-target");
    }

    [Test]
    public async Task TargetCapabilities_PrefersRequestTarget_WhenBothShapesAreProvided()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = new AzureSqlDiagnosticTools(new FrkProcedureService(sqlService, new FrkResultMapper()));

        var result = await tools.AzureSqlTargetCapabilities(
            target: "flat-target",
            request: new AzureSqlTargetCapabilitiesRequest { Target = "request-target" });

        await Assert.That(sqlService.LastCapabilitiesTarget).IsEqualTo("request-target");
        var response = (AzureSqlTargetCapabilitiesResponse)result;
        await Assert.That(response.Target).IsEqualTo("request-target");
    }

    [Test]
    public async Task BlitzCache_MarshalsTopLevelArguments_IntoRequest()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = new AzureSqlDiagnosticTools(new FrkProcedureService(sqlService, new FrkResultMapper()));

        var result = await tools.AzureSqlBlitzCache(
            target: "primary-sql-target",
            sortOrder: "reads",
            top: 7,
            aiMode: 2,
            maxRows: 9,
            includeVerboseResults: true);

        await Assert.That(sqlService.LastProcedureTarget).IsEqualTo("primary-sql-target");
        await Assert.That(sqlService.LastProcedureName).IsEqualTo("sp_BlitzCache");
        await Assert.That(sqlService.LastParameters["@SortOrder"]?.ToString()).IsEqualTo("reads");
        await Assert.That(sqlService.LastParameters["@Top"]?.ToString()).IsEqualTo("7");
        await Assert.That(sqlService.LastParameters["@AI"]?.ToString()).IsEqualTo("2");

        var response = (AzureSqlBlitzCacheResponse)result;
        await Assert.That(response.Target).IsEqualTo("primary-sql-target");
        await Assert.That(response.SortOrder).IsEqualTo("reads");
        await Assert.That(response.AiMode).IsEqualTo(2);
    }

    [Test]
    public async Task HttpSample_UsesExpectedMcpArgumentsShape_ForTargetCapabilities()
    {
        var httpFilePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "BlitzBridge.McpServer", "BlitzBridge.McpServer.http"));
        var httpContent = await File.ReadAllTextAsync(httpFilePath);
        var jsonStart = httpContent.IndexOf("\"jsonrpc\"", StringComparison.Ordinal);

        await Assert.That(jsonStart).IsGreaterThanOrEqualTo(1);

        jsonStart = httpContent.LastIndexOf('{', jsonStart);
        var jsonPayload = httpContent[jsonStart..];
        using var document = JsonDocument.Parse(jsonPayload);
        var root = document.RootElement;

        await Assert.That(root.GetProperty("method").GetString()).IsEqualTo("tools/call");

        var parameters = root.GetProperty("params");
        await Assert.That(parameters.GetProperty("name").GetString()).IsEqualTo("azure_sql_target_capabilities");

        var arguments = parameters.GetProperty("arguments");
        await Assert.That(arguments.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(arguments.GetProperty("target").GetString()).IsEqualTo("primary-sql-target");
    }

    private sealed class FakeSqlExecutionService : ISqlExecutionService
    {
        public string LastCapabilitiesTarget { get; private set; } = string.Empty;
        public string LastProcedureTarget { get; private set; } = string.Empty;
        public string LastProcedureName { get; private set; } = string.Empty;
        public Dictionary<string, object?> LastParameters { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<SqlTargetCapabilities> GetTargetCapabilitiesAsync(string target, CancellationToken cancellationToken = default)
        {
            LastCapabilitiesTarget = target;

            return Task.FromResult(new SqlTargetCapabilities
            {
                Target = target,
                CurrentDatabase = "DBAtools",
                EngineEdition = 5,
                EngineEditionName = "Azure SQL Database",
                AllowedDatabases = ["DBAtools"],
                AllowedProcedures = ["sp_Blitz", "sp_BlitzCache"],
                InstalledProcedures = ["sp_Blitz", "sp_BlitzCache"],
                SupportsAiPromptGeneration = true,
                SupportsDirectAiCalls = false,
                HasPromptConfigTable = true,
                HasProviderConfigTable = false,
                Notes = ["Capabilities response generated for test."]
            });
        }

        public Task<DataSet> ExecuteStoredProcedureAsync(
            string target,
            string procedureName,
            string? requestedDatabaseName,
            IEnumerable<SqlParameter> parameters,
            CancellationToken cancellationToken = default)
        {
            LastProcedureTarget = target;
            LastProcedureName = procedureName;
            LastParameters.Clear();

            foreach (var parameter in parameters)
            {
                LastParameters[parameter.ParameterName] = parameter.Value;
            }

            var dataSet = new DataSet();
            dataSet.Tables.Add(new DataTable("queries"));
            dataSet.Tables.Add(new DataTable("warning_glossary"));

            return Task.FromResult(dataSet);
        }
    }
}
