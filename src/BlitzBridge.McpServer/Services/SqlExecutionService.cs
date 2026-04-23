using System.Data;

using BlitzBridge.McpServer.Configuration;
using BlitzBridge.McpServer.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace BlitzBridge.McpServer.Services;

public interface ISqlExecutionService
{
    Task<DataSet> ExecuteStoredProcedureAsync(
        string target,
        string procedureName,
        string? requestedDatabaseName,
        IEnumerable<SqlParameter> parameters,
        CancellationToken cancellationToken = default);

    Task<SqlTargetCapabilities> GetTargetCapabilitiesAsync(
        string target,
        CancellationToken cancellationToken = default);
}

public sealed class SqlExecutionService : ISqlExecutionService
{
    private static readonly string[] DefaultAllowedProcedures =
    [
        "sp_Blitz",
        "sp_BlitzAnalysis",
        "sp_BlitzCache",
        "sp_BlitzFirst",
        "sp_BlitzIndex",
        "sp_BlitzLock",
        "sp_BlitzWho"
    ];

    private const string TargetCapabilitiesQuery = """
        SELECT
            DB_NAME() AS CurrentDatabase,
            CAST(SERVERPROPERTY('EngineEdition') AS int) AS EngineEdition,
            CASE
                WHEN EXISTS (
                    SELECT 1
                    FROM sys.database_scoped_credentials
                    WHERE name IN (N'https://api.openai.com/', N'https://generativelanguage.googleapis.com/')
                ) THEN CAST(1 AS bit)
                ELSE CAST(0 AS bit)
            END AS HasAiCredential,
            CASE WHEN USER_ID(N'DBA_AI') IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HasDbaAiRole,
            CASE WHEN OBJECT_ID(N'dbo.Blitz_AI_Prompts', N'U') IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HasPromptConfigTable,
            CASE WHEN OBJECT_ID(N'dbo.Blitz_AI_Providers', N'U') IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HasProviderConfigTable;

        SELECT
            name AS ProcedureName
        FROM sys.procedures
        WHERE name IN (
            N'sp_Blitz',
            N'sp_BlitzAnalysis',
            N'sp_BlitzCache',
            N'sp_BlitzFirst',
            N'sp_BlitzIndex',
            N'sp_BlitzLock',
            N'sp_BlitzWho'
        )
        ORDER BY name;
        """;

    private readonly SqlTargetOptions _options;

    public SqlExecutionService(IOptions<SqlTargetOptions> options)
    {
        _options = options.Value;
    }

    public async Task<DataSet> ExecuteStoredProcedureAsync(
        string target,
        string procedureName,
        string? requestedDatabaseName,
        IEnumerable<SqlParameter> parameters,
        CancellationToken cancellationToken = default)
    {
        var targetContext = GetTargetContext(target, procedureName, requestedDatabaseName);

        await using var connection = new SqlConnection(targetContext.ConnectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(procedureName, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = targetContext.Profile.CommandTimeoutSeconds
        };

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        using var adapter = new SqlDataAdapter(command);
        var dataSet = new DataSet();
        adapter.Fill(dataSet);

        return dataSet;
    }

    public async Task<SqlTargetCapabilities> GetTargetCapabilitiesAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        var targetContext = GetTargetContext(target, "sp_BlitzCache", null);
        var dataSet = await ExecuteTextQueryAsync(targetContext, TargetCapabilitiesQuery, cancellationToken);

        var metadata = dataSet.Tables.Count > 0 && dataSet.Tables[0].Rows.Count > 0
            ? dataSet.Tables[0].Rows[0]
            : null;

        var installedProcedures = new List<string>();
        if (dataSet.Tables.Count > 1)
        {
            foreach (DataRow row in dataSet.Tables[1].Rows)
            {
                if (row["ProcedureName"] is string procedureName)
                {
                    installedProcedures.Add(procedureName);
                }
            }
        }

        var engineEdition = GetValue<int>(metadata, "EngineEdition");
        var hasAiCredential = GetValue<bool>(metadata, "HasAiCredential");
        var hasDbaAiRole = GetValue<bool>(metadata, "HasDbaAiRole");

        return new SqlTargetCapabilities
        {
            Target = target,
            CurrentDatabase = GetValue<string>(metadata, "CurrentDatabase") ?? targetContext.CurrentDatabase,
            EngineEdition = engineEdition,
            EngineEditionName = ToEngineEditionName(engineEdition),
            Enabled = targetContext.Profile.Enabled,
            AllowedDatabases = [.. targetContext.Profile.AllowedDatabases],
            AllowedProcedures = [.. GetAllowedProcedures(targetContext.Profile)],
            InstalledProcedures = installedProcedures,
            SupportsAiPromptGeneration = installedProcedures.Contains("sp_BlitzCache", StringComparer.OrdinalIgnoreCase)
                || installedProcedures.Contains("sp_BlitzIndex", StringComparer.OrdinalIgnoreCase),
            SupportsDirectAiCalls = hasAiCredential && hasDbaAiRole,
            HasPromptConfigTable = GetValue<bool>(metadata, "HasPromptConfigTable"),
            HasProviderConfigTable = GetValue<bool>(metadata, "HasProviderConfigTable"),
            Notes = BuildCapabilityNotes(targetContext.CurrentDatabase, hasAiCredential, hasDbaAiRole)
        };
    }

    private async Task<DataSet> ExecuteTextQueryAsync(
        ResolvedTargetContext targetContext,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(targetContext.ConnectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = targetContext.Profile.CommandTimeoutSeconds
        };

        using var adapter = new SqlDataAdapter(command);
        var dataSet = new DataSet();
        adapter.Fill(dataSet);

        return dataSet;
    }

    private ResolvedTargetContext GetTargetContext(
        string target,
        string procedureName,
        string? requestedDatabaseName)
    {
        if (!_options.Profiles.TryGetValue(target, out var profile) || !profile.Enabled)
        {
            throw new InvalidOperationException($"Unknown or disabled target '{target}'.");
        }

        var connectionStringBuilder = new SqlConnectionStringBuilder(profile.ConnectionString);
        var currentDatabase = connectionStringBuilder.InitialCatalog;

        ValidateProcedureAccess(profile, procedureName);
        ValidateDatabaseAccess(profile, requestedDatabaseName, currentDatabase);

        return new ResolvedTargetContext(profile, connectionStringBuilder, currentDatabase);
    }

    private static void ValidateDatabaseAccess(
        SqlTargetProfile profile,
        string? requestedDatabaseName,
        string currentDatabase)
    {
        if (profile.AllowedDatabases.Count == 0)
        {
            return;
        }

        var effectiveDatabaseName = string.IsNullOrWhiteSpace(requestedDatabaseName)
            ? currentDatabase
            : requestedDatabaseName;

        if (string.IsNullOrWhiteSpace(effectiveDatabaseName))
        {
            throw new InvalidOperationException(
                "This target requires database allowlisting, but no effective database name was available.");
        }

        if (!profile.AllowedDatabases.Contains(effectiveDatabaseName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Database '{effectiveDatabaseName}' is not allowed for this target.");
        }
    }

    private static void ValidateProcedureAccess(SqlTargetProfile profile, string procedureName)
    {
        var allowedProcedures = GetAllowedProcedures(profile);
        if (!allowedProcedures.Contains(procedureName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Procedure '{procedureName}' is not allowed for this target.");
        }
    }

    private static IReadOnlyList<string> GetAllowedProcedures(SqlTargetProfile profile)
    {
        return profile.AllowedProcedures.Count > 0
            ? profile.AllowedProcedures
            : DefaultAllowedProcedures;
    }

    private static List<string> BuildCapabilityNotes(
        string currentDatabase,
        bool hasAiCredential,
        bool hasDbaAiRole)
    {
        var notes = new List<string>
        {
            $"This target executes FRK procedures from database context '{currentDatabase}'.",
            "sp_BlitzCache and sp_BlitzIndex AI prompt generation (@AI = 2) depends on the installed FRK version."
        };

        if (!hasAiCredential || !hasDbaAiRole)
        {
            notes.Add("Direct AI calls (@AI = 1) need both database-scoped credentials and DBA_AI role access in the execution database.");
        }

        return notes;
    }

    private static string ToEngineEditionName(int engineEdition)
    {
        return engineEdition switch
        {
            5 => "Azure SQL Database",
            8 => "Azure SQL Managed Instance",
            _ => "SQL Server"
        };
    }

    private static T GetValue<T>(DataRow? row, string columnName)
    {
        if (row is null || !row.Table.Columns.Contains(columnName) || row[columnName] is DBNull)
        {
            return default!;
        }

        return (T)Convert.ChangeType(row[columnName], typeof(T));
    }

    private sealed record ResolvedTargetContext(
        SqlTargetProfile Profile,
        SqlConnectionStringBuilder ConnectionStringBuilder,
        string CurrentDatabase);
}
