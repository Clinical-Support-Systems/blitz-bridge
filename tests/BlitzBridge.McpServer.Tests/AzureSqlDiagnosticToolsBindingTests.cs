using System.Data;
using System.Diagnostics;
using System.Text.Json;

using BlitzBridge.McpServer.Configuration;
using BlitzBridge.McpServer.Models;
using BlitzBridge.McpServer.Models.ToolRequests;
using BlitzBridge.McpServer.Models.ToolResponses;
using BlitzBridge.McpServer.Services;
using BlitzBridge.McpServer.Tools;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace BlitzBridge.McpServer.Tests;

public class AzureSqlDiagnosticToolsBindingTests
{
    [Test]
    public async Task TargetCapabilities_UsesTopLevelTarget_WhenRequestObjectIsMissing()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);

        var result = await tools.AzureSqlTargetCapabilities(target: "primary-sql-target");

        await Assert.That(sqlService.LastCapabilitiesTarget).IsEqualTo("primary-sql-target");
        var response = (AzureSqlTargetCapabilitiesResponse)result;
        await Assert.That(response.Target).IsEqualTo("primary-sql-target");
    }

    [Test]
    public async Task TargetCapabilities_PrefersRequestTarget_WhenBothShapesAreProvided()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);

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
        var tools = CreateTools(sqlService);

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
        await Assert.That(response.QueryCount).IsEqualTo(2);
        await Assert.That(response.WarningGlossaryCount).IsEqualTo(1);
        await Assert.That(response.HasAiPrompt).IsTrue();
        await Assert.That(response.HasAiAdvice).IsTrue();
    }

    [Test]
    public async Task BlitzCache_UsesConfiguredAiMode_WhenAiModeIsNotSupplied()
    {
        var sqlService = new FakeSqlExecutionService { ConfiguredAiMode = 1 };
        var tools = CreateTools(sqlService);

        var result = await tools.AzureSqlBlitzCache(
            target: "primary-sql-target",
            sortOrder: "reads",
            top: 7);

        await Assert.That(sqlService.LastProcedureTarget).IsEqualTo("primary-sql-target");
        await Assert.That(sqlService.LastParameters["@AI"]?.ToString()).IsEqualTo("1");

        var response = (AzureSqlBlitzCacheResponse)result;
        await Assert.That(response.AiMode).IsEqualTo(1);
    }

    [Test]
    public async Task BlitzCache_Response_EmitsProgressiveDisclosureHandles()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);

        var result = (AzureSqlBlitzCacheResponse)await tools.AzureSqlBlitzCache(
            target: "primary-sql-target",
            sortOrder: "cpu",
            top: 10);

        await Assert.That(result.Handles.Count).IsEqualTo(4);
        await Assert.That(result.Handles.Select(handle => handle.Kind).ToArray())
            .IsEquivalentTo(["queries", "warning_glossary", "ai_prompt", "ai_advice"]);

        var queryHandle = result.Handles.Single(handle => handle.Kind == "queries");
        await Assert.That(queryHandle.ParentTool).IsEqualTo("azure_sql_blitz_cache");
        await Assert.That(queryHandle.Handle).StartsWith("v1:");
        await Assert.That(queryHandle.ItemCount).IsEqualTo(2);
        await Assert.That(queryHandle.TotalCount).IsEqualTo(2);
    }

    [Test]
    public async Task FetchDetailByHandle_UsesExplicitDispatchAndReturnsRequestedSection()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);
        var parent = (AzureSqlBlitzCacheResponse)await tools.AzureSqlBlitzCache(
            target: "primary-sql-target",
            sortOrder: "cpu",
            top: 10);
        var warningHandle = parent.Handles.Single(handle => handle.Kind == "warning_glossary");

        var detail = (AzureSqlFetchDetailByHandleResponse)await tools.AzureSqlFetchDetailByHandle(
            target: "primary-sql-target",
            parentTool: "azure_sql_blitz_cache",
            kind: "warning_glossary",
            handle: warningHandle.Handle,
            maxRows: 25);

        await Assert.That(sqlService.LastProcedureName).IsEqualTo("sp_BlitzCache");
        await Assert.That(detail.ParentTool).IsEqualTo("azure_sql_blitz_cache");
        await Assert.That(detail.Kind).IsEqualTo("warning_glossary");
        await Assert.That(detail.Handle).IsEqualTo(warningHandle.Handle);
        await Assert.That(detail.Scope["sortOrder"]).IsEqualTo("cpu");
        await Assert.That(detail.Items.Count).IsEqualTo(1);
        await Assert.That(detail.Items[0]["Warning"]?.ToString()).IsEqualTo("implicit conversion");
    }

    [Test]
    public async Task FetchDetailByHandle_RejectsUnknownKind()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);

        await Assert.That(async () => await tools.AzureSqlFetchDetailByHandle(
                target: "primary-sql-target",
                parentTool: "azure_sql_blitz_cache",
                kind: "not-real",
                handle: "v1:ignored"))
            .Throws<ProgressiveDisclosureException>()
            .WithMessage("Unknown kind 'not-real' for parentTool 'azure_sql_blitz_cache'. Valid kinds: queries, warning_glossary, ai_prompt, ai_advice");
    }

    [Test]
    public async Task FetchDetailByHandle_RejectsUnknownParentTool()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);

        await Assert.That(async () => await tools.AzureSqlFetchDetailByHandle(
                target: "primary-sql-target",
                parentTool: "azure_sql_unknown_tool",
                kind: "queries",
                handle: "v1:ignored"))
            .Throws<ProgressiveDisclosureException>()
            .WithMessage("Unknown parentTool: 'azure_sql_unknown_tool'. Valid tools: azure_sql_health_check, azure_sql_blitz_cache, azure_sql_blitz_index, azure_sql_current_incident");
    }

    [Test]
    public async Task FetchDetailByHandle_RejectsMalformedHandle()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);

        await Assert.That(async () => await tools.AzureSqlFetchDetailByHandle(
                target: "primary-sql-target",
                parentTool: "azure_sql_blitz_cache",
                kind: "queries",
                handle: "not-base64"))
            .Throws<ProgressiveDisclosureException>()
            .WithMessage("Handle must be a valid base64-encoded JSON object with fields: version, parentTool, kind, target, ...");
    }

    [Test]
    public async Task FetchDetailByHandle_RejectsMismatchedDispatchMetadata()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);
        var parent = (AzureSqlBlitzCacheResponse)await tools.AzureSqlBlitzCache(
            target: "primary-sql-target",
            sortOrder: "cpu",
            top: 10);
        var warningHandle = parent.Handles.Single(handle => handle.Kind == "warning_glossary");

        await Assert.That(async () => await tools.AzureSqlFetchDetailByHandle(
                target: "primary-sql-target",
                parentTool: "azure_sql_blitz_cache",
                kind: "queries",
                handle: warningHandle.Handle))
            .Throws<ProgressiveDisclosureException>()
            .WithMessage("Handle dispatch metadata does not match the requested target, parentTool, and kind. Re-run the parent tool and use the returned handle unchanged.");
    }

    [Test]
    public async Task CurrentIncident_Response_UsesSectionLevelHandlesOnly()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);

        var result = (AzureSqlCurrentIncidentResponse)await tools.AzureSqlCurrentIncident(target: "primary-sql-target");

        await Assert.That(result.Handles.Count).IsEqualTo(2);
        await Assert.That(result.Handles.Select(handle => handle.Kind).ToArray())
            .IsEquivalentTo(["waits", "findings"]);
    }

    [Test]
    public async Task ToolCalls_RecordResponseTelemetry()
    {
        var sqlService = new FakeSqlExecutionService();
        var tools = CreateTools(sqlService);
        using var activity = new Activity("response-telemetry-test").Start();

        _ = await tools.AzureSqlTargetCapabilities(target: "primary-sql-target");

        var estimatedTokens = activity.GetTagItem("blitzbridge.response.estimated_tokens");
        await Assert.That(estimatedTokens).IsNotNull();
        await Assert.That(Convert.ToInt32(estimatedTokens)).IsGreaterThan(0);
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

    private static AzureSqlDiagnosticTools CreateTools(FakeSqlExecutionService sqlService)
        => new(new FrkProcedureService(sqlService, new FrkResultMapper(), CreateTargetOptionsMonitor()));

    private static IOptionsMonitor<SqlTargetOptions> CreateTargetOptionsMonitor()
        => new StaticOptionsMonitor<SqlTargetOptions>(new SqlTargetOptions());

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class FakeSqlExecutionService : ISqlExecutionService
    {
        public int ConfiguredAiMode { get; set; } = 2;
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

            return Task.FromResult(procedureName switch
            {
                "sp_Blitz" => CreateHealthCheckDataSet(),
                "sp_BlitzCache" => CreateBlitzCacheDataSet(),
                "sp_BlitzIndex" => CreateBlitzIndexDataSet(),
                "sp_BlitzFirst" => CreateCurrentIncidentDataSet(),
                _ => new DataSet()
            });
        }

        public int GetConfiguredAiMode(string target) => ConfiguredAiMode;

        private static DataSet CreateHealthCheckDataSet()
        {
            var findings = new DataTable("findings");
            findings.Columns.Add("Priority", typeof(int));
            findings.Columns.Add("FindingsGroup", typeof(string));
            findings.Columns.Add("Finding", typeof(string));
            findings.Columns.Add("DatabaseName", typeof(string));
            findings.Columns.Add("Details", typeof(string));
            findings.Columns.Add("CheckID", typeof(int));
            findings.Columns.Add("URL", typeof(string));
            findings.Rows.Add(1, "Reliability", "Corruption risk", "AppDb", "Run CHECKDB.", 42, "https://example.test/checkdb");
            findings.Rows.Add(50, "Performance", "Outdated stats", "AppDb", "Update statistics.", 77, "https://example.test/stats");

            var dataSet = new DataSet();
            dataSet.Tables.Add(findings);
            return dataSet;
        }

        private static DataSet CreateBlitzCacheDataSet()
        {
            var queries = new DataTable("queries");
            queries.Columns.Add("DatabaseName", typeof(string));
            queries.Columns.Add("QueryHash", typeof(string));
            queries.Columns.Add("StatementStartOffset", typeof(int));
            queries.Columns.Add("StatementEndOffset", typeof(int));
            queries.Columns.Add("QueryType", typeof(string));
            queries.Columns.Add("ExecutionCount", typeof(int));
            queries.Columns.Add("ExecutionsPerMinute", typeof(decimal));
            queries.Columns.Add("AvgCPU", typeof(int));
            queries.Columns.Add("TotalCPU", typeof(int));
            queries.Columns.Add("AvgReads", typeof(int));
            queries.Columns.Add("TotalReads", typeof(int));
            queries.Columns.Add("AvgDuration", typeof(int));
            queries.Columns.Add("TotalDuration", typeof(int));
            queries.Columns.Add("Warnings", typeof(string));
            queries.Columns.Add("QueryText", typeof(string));
            queries.Rows.Add("AppDb", "0xABC", 0, 42, "Statement", 4502, 33.4m, 1842, 8293484, 3201, 14414402, 12045, 54250590, "implicit conversion", "SELECT * FROM dbo.Orders;");
            queries.Rows.Add("AppDb", "0xDEF", 0, 84, "Procedure", 275, 2.2m, 932, 256000, 404, 111000, 845, 232000, "missing index", "EXEC dbo.RebuildOrders;");

            var glossary = new DataTable("warning_glossary");
            glossary.Columns.Add("Warning", typeof(string));
            glossary.Columns.Add("Description", typeof(string));
            glossary.Columns.Add("URL", typeof(string));
            glossary.Rows.Add("implicit conversion", "Conversion forces scans.", "https://example.test/warnings/implicit-conversion");

            var ai = new DataTable("ai_results");
            ai.Columns.Add("AI Prompt", typeof(string));
            ai.Columns.Add("AI Advice", typeof(string));
            ai.Rows.Add("Summarize the highest-cost cached plans.", "Investigate the implicit conversion warning first.");

            var dataSet = new DataSet();
            dataSet.Tables.Add(queries);
            dataSet.Tables.Add(glossary);
            dataSet.Tables.Add(ai);
            return dataSet;
        }

        private static DataSet CreateBlitzIndexDataSet()
        {
            var existingIndexes = new DataTable("existing_indexes");
            existingIndexes.Columns.Add("DatabaseName", typeof(string));
            existingIndexes.Columns.Add("SchemaName", typeof(string));
            existingIndexes.Columns.Add("TableName", typeof(string));
            existingIndexes.Columns.Add("IndexName", typeof(string));
            existingIndexes.Columns.Add("IndexType", typeof(string));
            existingIndexes.Columns.Add("KeyColumnNames", typeof(string));
            existingIndexes.Columns.Add("IncludeColumnNames", typeof(string));
            existingIndexes.Columns.Add("IndexUsageSummary", typeof(string));
            existingIndexes.Columns.Add("Impact", typeof(string));
            existingIndexes.Rows.Add("AppDb", "dbo", "Orders", "IX_Orders_Status", "NONCLUSTERED", "Status", "CreatedAt", "hot", "medium");

            var missingIndexes = new DataTable("missing_indexes");
            missingIndexes.Columns.Add("DatabaseName", typeof(string));
            missingIndexes.Columns.Add("SchemaName", typeof(string));
            missingIndexes.Columns.Add("TableName", typeof(string));
            missingIndexes.Columns.Add("MissingIndexDetails", typeof(string));
            missingIndexes.Columns.Add("MagicBenefitNumber", typeof(decimal));
            missingIndexes.Columns.Add("CreateTsql", typeof(string));
            missingIndexes.Rows.Add("AppDb", "dbo", "Orders", "Status, CustomerId", 900.1m, "CREATE INDEX IX_Orders_Status_CustomerId ON dbo.Orders(Status, CustomerId);");

            var columnDataTypes = new DataTable("column_data_types");
            columnDataTypes.Columns.Add("ColumnName", typeof(string));
            columnDataTypes.Columns.Add("SystemTypeName", typeof(string));
            columnDataTypes.Columns.Add("MaxLength", typeof(short));
            columnDataTypes.Columns.Add("IsNullable", typeof(bool));
            columnDataTypes.Columns.Add("IsIdentity", typeof(bool));
            columnDataTypes.Rows.Add("OrderId", "int", 4, false, true);

            var foreignKeys = new DataTable("foreign_keys");
            foreignKeys.Columns.Add("ForeignKeyName", typeof(string));
            foreignKeys.Columns.Add("ParentTableName", typeof(string));
            foreignKeys.Columns.Add("ReferencedTableName", typeof(string));
            foreignKeys.Rows.Add("FK_Orders_Customers", "Orders", "Customers");

            var dataSet = new DataSet();
            dataSet.Tables.Add(existingIndexes);
            dataSet.Tables.Add(missingIndexes);
            dataSet.Tables.Add(columnDataTypes);
            dataSet.Tables.Add(foreignKeys);
            return dataSet;
        }

        private static DataSet CreateCurrentIncidentDataSet()
        {
            var waits = new DataTable("waits");
            waits.Columns.Add("CheckDate", typeof(DateTime));
            waits.Columns.Add("WaitType", typeof(string));
            waits.Columns.Add("WaitTimeSeconds", typeof(int));
            waits.Columns.Add("WaitTimeMsPerMinute", typeof(int));
            waits.Columns.Add("SignalWaitTimeMs", typeof(int));
            waits.Columns.Add("ResourceWaitTimeMs", typeof(int));
            waits.Rows.Add(DateTime.UtcNow, "LCK_M_X", 12, 300, 12, 288);

            var findings = new DataTable("findings");
            findings.Columns.Add("Priority", typeof(int));
            findings.Columns.Add("FindingsGroup", typeof(string));
            findings.Columns.Add("Finding", typeof(string));
            findings.Columns.Add("Details", typeof(string));
            findings.Columns.Add("URL", typeof(string));
            findings.Rows.Add(5, "Performance", "Blocking detected", "Long blocker chain.", "https://example.test/blocking");

            var dataSet = new DataSet();
            dataSet.Tables.Add(waits);
            dataSet.Tables.Add(findings);
            return dataSet;
        }
    }
}
