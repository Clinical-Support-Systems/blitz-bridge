using System.Data;

using BlitzBridge.McpServer.Models;
using BlitzBridge.McpServer.Models.ToolRequests;
using BlitzBridge.McpServer.Models.ToolResponses;

namespace BlitzBridge.McpServer.Services;

/// <summary>
/// Maps FRK <see cref="DataSet"/> results into compact MCP response models.
/// </summary>
public sealed class FrkResultMapper
{
    private static readonly string[] HealthCheckPreferredColumns =
    [
        "Priority",
        "FindingsGroup",
        "Finding",
        "DatabaseName",
        "Details",
        "CheckID",
        "URL"
    ];

    private static readonly string[] IncidentWaitPreferredColumns =
    [
        "CheckDate",
        "WaitType",
        "WaitTimeSeconds",
        "WaitTimeMsPerMinute",
        "SignalWaitTimeMs",
        "ResourceWaitTimeMs"
    ];

    private static readonly string[] IncidentFindingPreferredColumns =
    [
        "Priority",
        "FindingsGroup",
        "Finding",
        "Details",
        "URL"
    ];

    /// <summary>
    /// Maps <c>sp_Blitz</c> results to the health-check response model.
    /// </summary>
    /// <param name="request">Original tool request.</param>
    /// <param name="dataSet">Returned FRK data set.</param>
    /// <returns>Mapped health-check response.</returns>
    public AzureSqlHealthCheckResponse MapHealthCheck(AzureSqlHealthCheckRequest request, DataSet dataSet)
    {
        var findings = ToProjectedRows(dataSet, 0, request.MaxRows, HealthCheckPreferredColumns, 280);
        var totalFindings = GetRowCount(dataSet, 0);
        var summary = BuildHealthCheckSummary(findings, totalFindings);

        return new AzureSqlHealthCheckResponse
        {
            Target = request.Target,
            TotalFindings = totalFindings,
            Summary = summary,
            Findings = findings,
            ResultSets = request.IncludeVerboseResults
                ? BuildResultSets(dataSet, request.MaxRows, (_, index) => $"result_set_{index + 1}")
                : [],
            Notes =
            [
                "Results are compacted by default to reduce agent token usage.",
                "Set IncludeVerboseResults to true when you need the raw FRK-shaped result sets.",
                "Some checks may be reduced or skipped depending on Azure SQL platform capabilities."
            ]
        };
    }

    /// <summary>
    /// Maps <c>sp_BlitzCache</c> results to the BlitzCache response model.
    /// </summary>
    /// <param name="request">Original tool request.</param>
    /// <param name="dataSet">Returned FRK data set.</param>
    /// <returns>Mapped BlitzCache response.</returns>
    public AzureSqlBlitzCacheResponse MapBlitzCache(AzureSqlBlitzCacheRequest request, DataSet dataSet)
    {
        var verboseResultSets = request.IncludeVerboseResults
            ? BuildResultSets(dataSet, request.MaxRows, ClassifyBlitzCacheResultSetName)
            : [];
        var queries = ToProjectedRows(
            dataSet,
            0,
            request.MaxRows,
            ["DatabaseName", "QueryType", "ExecutionCount", "ExecutionsPerMinute", "AvgCPU", "TotalCPU", "AvgReads", "TotalReads", "AvgDuration", "TotalDuration", "Warnings", "QueryText"],
            320);
        var warningGlossary = ToProjectedRows(
            dataSet,
            1,
            Math.Min(request.MaxRows, 10),
            ["Warning", "Description", "URL"],
            240);
        var aiPrompt = ExtractText(dataSet, "AI Prompt", "AIPrompt", "AI_Prompt");
        var aiAdvice = ExtractText(dataSet, "AI Advice", "AIAdvice", "AI_Advice");

        var summary = new List<DiagnosticSummary>
        {
            new()
            {
                Title = $"Top queries by {request.SortOrder}",
                Severity = "info",
                Message = $"Returned {queries.Count} query row(s)."
            }
        };

        if (!string.IsNullOrWhiteSpace(aiPrompt))
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "AI prompt available",
                Severity = "info",
                Message = "The FRK result set included a generated AI prompt."
            });
        }

        if (!string.IsNullOrWhiteSpace(aiAdvice))
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "AI advice returned",
                Severity = "info",
                Message = "The FRK result set included direct AI advice."
            });
        }

        return new AzureSqlBlitzCacheResponse
        {
            Target = request.Target,
            SortOrder = request.SortOrder,
            DatabaseName = request.DatabaseName,
            AiMode = request.AiMode,
            Summary = summary,
            Queries = queries,
            WarningGlossary = warningGlossary,
            AiPrompt = aiPrompt,
            AiAdvice = aiAdvice,
            ResultSets = verboseResultSets,
            Notes = BuildBlitzCacheNotes(request.AiMode, request.IncludeVerboseResults)
        };
    }

    /// <summary>
    /// Maps <c>sp_BlitzIndex</c> results to the BlitzIndex response model.
    /// </summary>
    /// <param name="request">Original tool request.</param>
    /// <param name="dataSet">Returned FRK data set.</param>
    /// <returns>Mapped BlitzIndex response.</returns>
    public AzureSqlBlitzIndexResponse MapBlitzIndex(AzureSqlBlitzIndexRequest request, DataSet dataSet)
    {
        var resultSets = request.IncludeVerboseResults
            ? BuildResultSets(dataSet, request.MaxRows, ClassifyBlitzIndexResultSetName)
            : [];
        var existingIndexes = ToProjectedRows(
            dataSet,
            0,
            request.MaxRows,
            ["DatabaseName", "SchemaName", "TableName", "IndexName", "IndexType", "KeyColumnNames", "IncludeColumnNames", "IndexUsageSummary", "Impact"],
            240);
        var missingIndexes = ToProjectedRows(
            dataSet,
            1,
            request.MaxRows,
            ["DatabaseName", "SchemaName", "TableName", "MissingIndexDetails", "MagicBenefitNumber", "CreateTsql"],
            320);
        var columnDataTypes = ToProjectedRows(
            dataSet,
            2,
            Math.Min(request.MaxRows, 25),
            ["ColumnName", "SystemTypeName", "MaxLength", "IsNullable", "IsIdentity"],
            180);
        var foreignKeys = ToProjectedRows(
            dataSet,
            3,
            Math.Min(request.MaxRows, 25),
            ["ForeignKeyName", "ParentTableName", "ReferencedTableName"],
            180);
        var aiPrompt = ExtractText(dataSet, "AI Prompt", "AIPrompt", "AI_Prompt");
        var aiAdvice = ExtractText(dataSet, "AI Advice", "AIAdvice", "AI_Advice");

        var summary = new List<DiagnosticSummary>
        {
            new()
            {
                Title = "Table analysis",
                Severity = "info",
                Message = $"Returned {resultSets.Count} result set(s) for {request.DatabaseName}.{request.SchemaName}.{request.TableName}."
            }
        };

        if (missingIndexes.Count > 0)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "Missing index suggestions",
                Severity = "info",
                Message = $"Found {missingIndexes.Count} missing index row(s)."
            });
        }

        if (!string.IsNullOrWhiteSpace(aiPrompt))
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "AI prompt available",
                Severity = "info",
                Message = "The FRK result set included a generated AI prompt."
            });
        }

        return new AzureSqlBlitzIndexResponse
        {
            Target = request.Target,
            DatabaseName = request.DatabaseName,
            SchemaName = request.SchemaName,
            TableName = request.TableName,
            AiMode = request.AiMode,
            Summary = summary,
            ExistingIndexes = existingIndexes,
            MissingIndexes = missingIndexes,
            ColumnDataTypes = columnDataTypes,
            ForeignKeys = foreignKeys,
            AiPrompt = aiPrompt,
            AiAdvice = aiAdvice,
            ResultSets = resultSets,
            Notes = BuildBlitzIndexNotes(request.AiMode, request.IncludeVerboseResults)
        };
    }

    /// <summary>
    /// Maps <c>sp_BlitzFirst</c> results to the current-incident response model.
    /// </summary>
    /// <param name="request">Original tool request.</param>
    /// <param name="dataSet">Returned FRK data set.</param>
    /// <returns>Mapped current-incident response.</returns>
    public AzureSqlCurrentIncidentResponse MapCurrentIncident(AzureSqlCurrentIncidentRequest request, DataSet dataSet)
    {
        var waits = ToProjectedRows(dataSet, 0, request.MaxRows, IncidentWaitPreferredColumns, 180);
        var findings = ToProjectedRows(dataSet, 1, request.MaxRows, IncidentFindingPreferredColumns, 240);
        var totalWaitRows = GetRowCount(dataSet, 0);
        var totalFindingRows = GetRowCount(dataSet, 1);
        var summary = new List<DiagnosticSummary>
        {
            new()
            {
                Title = "Current incident snapshot",
                Severity = "info",
                Message = $"Returned {findings.Count} compact finding row(s) from {totalFindingRows} total finding row(s)."
            }
        };

        return new AzureSqlCurrentIncidentResponse
        {
            Target = request.Target,
            TotalWaitRows = totalWaitRows,
            TotalFindingRows = totalFindingRows,
            Summary = summary,
            Waits = waits,
            Findings = findings,
            ResultSets = request.IncludeVerboseResults
                ? BuildResultSets(dataSet, request.MaxRows, (_, index) => $"result_set_{index + 1}")
                : [],
            Notes =
            [
                "This is a compact point-in-time snapshot.",
                "Set IncludeVerboseResults to true when you need raw wait and finding tables.",
                "Repeated executions may be needed to confirm trends."
            ]
        };
    }

    /// <summary>
    /// Maps target capability metadata to the tool response model.
    /// </summary>
    /// <param name="capabilities">Raw capability metadata.</param>
    /// <returns>Mapped target-capabilities response.</returns>
    public AzureSqlTargetCapabilitiesResponse MapTargetCapabilities(SqlTargetCapabilities capabilities)
    {
        var summary = new List<DiagnosticSummary>
        {
            new()
            {
                Title = "Target profile",
                Severity = "info",
                Message = $"Target points at {capabilities.EngineEditionName} in database '{capabilities.CurrentDatabase}'."
            },
            new()
            {
                Title = "Installed FRK procedures",
                Severity = "info",
                Message = capabilities.InstalledProcedures.Count == 0
                    ? "No supported FRK procedures were detected."
                    : $"Detected {capabilities.InstalledProcedures.Count} supported FRK procedure(s)."
            }
        };

        if (capabilities.SupportsAiPromptGeneration)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "AI prompt generation available",
                Severity = "info",
                Message = "This target can surface FRK-generated AI prompts when the underlying proc supports it."
            });
        }

        if (capabilities.SupportsDirectAiCalls)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "Direct AI calls available",
                Severity = "info",
                Message = "This target appears ready for FRK @AI = 1 direct provider calls."
            });
        }

        return new AzureSqlTargetCapabilitiesResponse
        {
            Target = capabilities.Target,
            CurrentDatabase = capabilities.CurrentDatabase,
            EngineEdition = capabilities.EngineEdition,
            EngineEditionName = capabilities.EngineEditionName,
            AllowedDatabases = capabilities.AllowedDatabases,
            AllowedProcedures = capabilities.AllowedProcedures,
            InstalledProcedures = capabilities.InstalledProcedures,
            SupportsAiPromptGeneration = capabilities.SupportsAiPromptGeneration,
            SupportsDirectAiCalls = capabilities.SupportsDirectAiCalls,
            HasPromptConfigTable = capabilities.HasPromptConfigTable,
            HasProviderConfigTable = capabilities.HasProviderConfigTable,
            Summary = summary,
            Notes = capabilities.Notes
        };
    }

    private static List<string> BuildBlitzCacheNotes(int aiMode, bool includeVerboseResults)
    {
        var notes = new List<string>
        {
            "Query text and large text columns are truncated by default to reduce token usage.",
            "Interpret plan cache results alongside runtime workload context."
        };

        if (aiMode == 1)
        {
            notes.Add("Direct AI calls depend on credentials and role access in the execution database context.");
        }
        else if (aiMode == 2)
        {
            notes.Add("AI prompt generation returns FRK-crafted prompt text without calling an external provider.");
        }

        if (!includeVerboseResults)
        {
            notes.Add("Verbose FRK result sets are omitted by default.");
        }

        return notes;
    }

    private static List<string> BuildBlitzIndexNotes(int aiMode, bool includeVerboseResults)
    {
        var notes = new List<string>
        {
            "This wrapper is tuned for single-table sp_BlitzIndex analysis.",
            "Review missing index suggestions alongside existing workload patterns before applying changes."
        };

        if (aiMode == 1)
        {
            notes.Add("Direct AI calls depend on credentials and role access in the execution database context.");
        }
        else if (aiMode == 2)
        {
            notes.Add("AI prompt generation returns FRK-crafted prompt text without calling an external provider.");
        }

        if (!includeVerboseResults)
        {
            notes.Add("Verbose FRK result sets are omitted by default.");
        }

        return notes;
    }

    private static List<DiagnosticSummary> BuildHealthCheckSummary(
        List<Dictionary<string, object?>> findings,
        int totalFindings)
    {
        var summary = new List<DiagnosticSummary>
        {
            new()
            {
                Title = "Health check",
                Severity = "info",
                Message = $"Returned {findings.Count} compact finding row(s) from {totalFindings} total finding row(s)."
            }
        };

        var topPriority = findings
            .Select(row => TryGetInt(row, "Priority"))
            .Where(priority => priority.HasValue)
            .Min();

        if (topPriority.HasValue)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "Top priority finding",
                Severity = topPriority.Value <= 50 ? "warning" : "info",
                Message = $"The highest visible priority in this compact response is {topPriority.Value}."
            });
        }

        var groups = findings
            .Select(row => Convert.ToString(GetValue(row, "FindingsGroup")))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        if (groups.Count > 0)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "Finding groups",
                Severity = "info",
                Message = $"Visible groups: {string.Join(", ", groups)}."
            });
        }

        return summary;
    }

    private static List<DiagnosticSummary> BuildBasicSummary(string title, int count)
    {
        return
        [
            new()
            {
                Title = title,
                Severity = "info",
                Message = $"Returned {count} row(s)."
            }
        ];
    }

    private static List<ProcedureResultSet> BuildResultSets(
        DataSet dataSet,
        int maxRows,
        Func<DataTable, int, string> nameFactory)
    {
        var resultSets = new List<ProcedureResultSet>();

        for (var i = 0; i < dataSet.Tables.Count; i++)
        {
            var table = dataSet.Tables[i];
            resultSets.Add(new ProcedureResultSet
            {
                Name = nameFactory(table, i),
                Columns = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToList(),
                Rows = ToProjectedRows(dataSet, i, maxRows, null, 400)
            });
        }

        return resultSets;
    }

    private static string ClassifyBlitzCacheResultSetName(DataTable table, int index)
    {
        if (HasAnyColumn(table, "AI Prompt", "AIPrompt", "AI_Prompt", "AI Advice", "AIAdvice", "AI_Advice"))
        {
            return "ai_results";
        }

        return index switch
        {
            0 => "queries",
            1 => "warning_glossary",
            _ => $"result_set_{index + 1}"
        };
    }

    private static string ClassifyBlitzIndexResultSetName(DataTable table, int index)
    {
        if (HasAnyColumn(table, "AI Prompt", "AIPrompt", "AI_Prompt", "AI Advice", "AIAdvice", "AI_Advice"))
        {
            return "ai_results";
        }

        if (HasAnyColumn(table, "create_tsql", "Create TSQL", "Missing Index Details", "magic_benefit_number"))
        {
            return "missing_indexes";
        }

        if (HasAnyColumn(table, "Index Usage Summary", "key_column_names", "Key Column Names", "include_column_names"))
        {
            return "existing_indexes";
        }

        if (HasAnyColumn(table, "Column Name", "column_name", "system_type_name", "Is Identity"))
        {
            return "column_data_types";
        }

        if (HasAnyColumn(table, "foreign_key_name", "Foreign Key Name", "Referenced Table Name"))
        {
            return "foreign_keys";
        }

        return $"result_set_{index + 1}";
    }

    private static bool HasAnyColumn(DataTable table, params string[] candidateNames)
    {
        return candidateNames.Any(candidateName =>
            table.Columns.Cast<DataColumn>().Any(column =>
                string.Equals(column.ColumnName, candidateName, StringComparison.OrdinalIgnoreCase)));
    }

    private static string? ExtractText(DataSet dataSet, params string[] candidateNames)
    {
        foreach (DataTable table in dataSet.Tables)
        {
            foreach (DataColumn column in table.Columns)
            {
                if (!candidateNames.Contains(column.ColumnName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (DataRow row in table.Rows)
                {
                    if (row[column] is not DBNull && row[column] is not null)
                    {
                        return Convert.ToString(row[column]);
                    }
                }
            }
        }

        return null;
    }

    private static List<Dictionary<string, object?>> GetRows(
        IEnumerable<ProcedureResultSet> resultSets,
        string resultSetName)
    {
        return resultSets.FirstOrDefault(resultSet =>
            string.Equals(resultSet.Name, resultSetName, StringComparison.OrdinalIgnoreCase))?.Rows ?? [];
    }

    private static int GetRowCount(DataSet dataSet, int tableIndex)
    {
        if (dataSet.Tables.Count <= tableIndex)
        {
            return 0;
        }

        return dataSet.Tables[tableIndex].Rows.Count;
    }

    private static List<Dictionary<string, object?>> ToProjectedRows(
        DataSet dataSet,
        int tableIndex,
        int maxRows,
        IReadOnlyList<string>? preferredColumns,
        int maxStringLength)
    {
        var results = new List<Dictionary<string, object?>>();

        if (dataSet.Tables.Count <= tableIndex)
        {
            return results;
        }

        var table = dataSet.Tables[tableIndex];
        var columns = ResolveColumns(table, preferredColumns);

        foreach (DataRow row in table.Rows.Cast<DataRow>().Take(maxRows))
        {
            var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                item[column.ColumnName] = NormalizeCellValue(row[column], maxStringLength);
            }

            results.Add(item);
        }

        return results;
    }

    private static List<DataColumn> ResolveColumns(DataTable table, IReadOnlyList<string>? preferredColumns)
    {
        if (preferredColumns is null || preferredColumns.Count == 0)
        {
            return table.Columns.Cast<DataColumn>().ToList();
        }

        var matchedColumns = preferredColumns
            .Select(name => table.Columns.Cast<DataColumn>().FirstOrDefault(column =>
                string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase)))
            .Where(column => column is not null)
            .Cast<DataColumn>()
            .ToList();

        return matchedColumns.Count > 0
            ? matchedColumns
            : table.Columns.Cast<DataColumn>().Take(8).ToList();
    }

    private static object? NormalizeCellValue(object value, int maxStringLength)
    {
        if (value == DBNull.Value)
        {
            return null;
        }

        if (value is string text)
        {
            return text.Length <= maxStringLength
                ? text
                : $"{text[..maxStringLength]}...";
        }

        return value;
    }

    private static object? GetValue(Dictionary<string, object?> row, string key)
    {
        return row.TryGetValue(key, out var value) ? value : null;
    }

    private static int? TryGetInt(Dictionary<string, object?> row, string key)
    {
        var value = GetValue(row, key);
        if (value is null)
        {
            return null;
        }

        return int.TryParse(Convert.ToString(value), out var result) ? result : null;
    }
}
