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

    private static readonly string[] BlitzCacheQueryColumns =
    [
        "DatabaseName",
        "QueryHash",
        "StatementStartOffset",
        "StatementEndOffset",
        "QueryType",
        "ExecutionCount",
        "ExecutionsPerMinute",
        "AvgCPU",
        "TotalCPU",
        "AvgReads",
        "TotalReads",
        "AvgDuration",
        "TotalDuration",
        "Warnings",
        "QueryText"
    ];

    private static readonly string[] BlitzCacheWarningGlossaryColumns =
    [
        "Warning",
        "Description",
        "URL"
    ];

    private static readonly string[] BlitzIndexExistingColumns =
    [
        "DatabaseName",
        "SchemaName",
        "TableName",
        "IndexName",
        "IndexType",
        "KeyColumnNames",
        "IncludeColumnNames",
        "IndexUsageSummary",
        "Impact"
    ];

    private static readonly string[] BlitzIndexMissingColumns =
    [
        "DatabaseName",
        "SchemaName",
        "TableName",
        "MissingIndexDetails",
        "MagicBenefitNumber",
        "CreateTsql"
    ];

    private static readonly string[] BlitzIndexColumnDataTypeColumns =
    [
        "ColumnName",
        "SystemTypeName",
        "MaxLength",
        "IsNullable",
        "IsIdentity"
    ];

    private static readonly string[] BlitzIndexForeignKeyColumns =
    [
        "ForeignKeyName",
        "ParentTableName",
        "ReferencedTableName"
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
        var highestVisiblePriority = GetHighestVisiblePriority(findings);
        var visibleFindingGroups = GetDistinctStringValues(findings, "FindingsGroup", 5);
        var summary = BuildHealthCheckSummary(findings, totalFindings, highestVisiblePriority, visibleFindingGroups);

        return new AzureSqlHealthCheckResponse
        {
            Target = request.Target,
            DatabaseName = request.DatabaseName,
            TotalFindings = totalFindings,
            HighestVisiblePriority = highestVisiblePriority,
            VisibleFindingGroups = visibleFindingGroups,
            Summary = summary,
            Findings = findings,
            Handles = BuildHealthCheckHandles(request, findings.Count, totalFindings, highestVisiblePriority),
            ResultSets = request.IncludeVerboseResults
                ? BuildResultSets(dataSet, request.MaxRows, (_, index) => $"result_set_{index + 1}")
                : [],
            Notes =
            [
                "Results are compacted by default to reduce agent token usage.",
                "Use azure_sql_fetch_detail_by_handle with returned handles for expanded sections. IncludeVerboseResults remains available for compatibility.",
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
            BlitzCacheQueryColumns,
            320);
        var warningGlossary = ToProjectedRows(
            dataSet,
            1,
            Math.Min(request.MaxRows, 10),
            BlitzCacheWarningGlossaryColumns,
            240);
        var queryCount = GetRowCount(dataSet, 0);
        var warningGlossaryCount = GetRowCount(dataSet, 1);
        var aiPrompt = ExtractText(dataSet, "AI Prompt", "AIPrompt", "AI_Prompt");
        var aiAdvice = ExtractText(dataSet, "AI Advice", "AIAdvice", "AI_Advice");
        var hasAiPrompt = !string.IsNullOrWhiteSpace(aiPrompt);
        var hasAiAdvice = !string.IsNullOrWhiteSpace(aiAdvice);

        var summary = new List<DiagnosticSummary>
        {
            new()
            {
                Title = $"Top queries by {request.SortOrder}",
                Severity = "info",
                Message = $"Returned {queries.Count} compact query row(s) from {queryCount} total query row(s)."
            }
        };

        if (warningGlossaryCount > 0)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "Warning glossary",
                Severity = "info",
                Message = $"Returned {warningGlossary.Count} glossary row(s) from {warningGlossaryCount} total glossary row(s)."
            });
        }

        if (hasAiPrompt)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "AI prompt available",
                Severity = "info",
                Message = "The FRK result set included a generated AI prompt."
            });
        }

        if (hasAiAdvice)
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
            QueryCount = queryCount,
            WarningGlossaryCount = warningGlossaryCount,
            HasAiPrompt = hasAiPrompt,
            HasAiAdvice = hasAiAdvice,
            Summary = summary,
            Queries = queries,
            WarningGlossary = warningGlossary,
            AiPrompt = aiPrompt,
            AiAdvice = aiAdvice,
            Handles = BuildBlitzCacheHandles(request, queries.Count, queryCount, warningGlossary.Count, warningGlossaryCount, hasAiPrompt, hasAiAdvice),
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
            BlitzIndexExistingColumns,
            240);
        var missingIndexes = ToProjectedRows(
            dataSet,
            1,
            request.MaxRows,
            BlitzIndexMissingColumns,
            320);
        var columnDataTypes = ToProjectedRows(
            dataSet,
            2,
            Math.Min(request.MaxRows, 25),
            BlitzIndexColumnDataTypeColumns,
            180);
        var foreignKeys = ToProjectedRows(
            dataSet,
            3,
            Math.Min(request.MaxRows, 25),
            BlitzIndexForeignKeyColumns,
            180);
        var existingIndexCount = GetRowCount(dataSet, 0);
        var missingIndexCount = GetRowCount(dataSet, 1);
        var columnDataTypeCount = GetRowCount(dataSet, 2);
        var foreignKeyCount = GetRowCount(dataSet, 3);
        var aiPrompt = ExtractText(dataSet, "AI Prompt", "AIPrompt", "AI_Prompt");
        var aiAdvice = ExtractText(dataSet, "AI Advice", "AIAdvice", "AI_Advice");
        var hasAiPrompt = !string.IsNullOrWhiteSpace(aiPrompt);
        var hasAiAdvice = !string.IsNullOrWhiteSpace(aiAdvice);

        var summary = new List<DiagnosticSummary>
        {
            new()
            {
                Title = "Table analysis",
                Severity = missingIndexCount > 0 ? "warning" : "info",
                Message = $"Analyzed {request.DatabaseName}.{request.SchemaName}.{request.TableName} and returned {existingIndexCount + missingIndexCount + columnDataTypeCount + foreignKeyCount} total row(s) across visible sections."
            }
        };

        if (missingIndexCount > 0)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "Missing index suggestions",
                Severity = "warning",
                Message = $"Found {missingIndexCount} missing index row(s)."
            });
        }

        if (hasAiPrompt)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "AI prompt available",
                Severity = "info",
                Message = "The FRK result set included a generated AI prompt."
            });
        }

        if (hasAiAdvice)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "AI advice returned",
                Severity = "info",
                Message = "The FRK result set included direct AI advice."
            });
        }

        return new AzureSqlBlitzIndexResponse
        {
            Target = request.Target,
            DatabaseName = request.DatabaseName,
            SchemaName = request.SchemaName,
            TableName = request.TableName,
            AiMode = request.AiMode,
            ExistingIndexCount = existingIndexCount,
            MissingIndexCount = missingIndexCount,
            ColumnDataTypeCount = columnDataTypeCount,
            ForeignKeyCount = foreignKeyCount,
            HasAiPrompt = hasAiPrompt,
            HasAiAdvice = hasAiAdvice,
            Summary = summary,
            ExistingIndexes = existingIndexes,
            MissingIndexes = missingIndexes,
            ColumnDataTypes = columnDataTypes,
            ForeignKeys = foreignKeys,
            AiPrompt = aiPrompt,
            AiAdvice = aiAdvice,
            Handles = BuildBlitzIndexHandles(
                request,
                existingIndexes.Count,
                existingIndexCount,
                missingIndexes.Count,
                missingIndexCount,
                columnDataTypes.Count,
                columnDataTypeCount,
                foreignKeys.Count,
                foreignKeyCount,
                hasAiPrompt,
                hasAiAdvice),
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
        var topWaitTypes = GetDistinctStringValues(waits, "WaitType", 5);
        var summary = new List<DiagnosticSummary>
        {
            new()
            {
                Title = "Current incident snapshot",
                Severity = totalFindingRows > 0 || totalWaitRows > 0 ? "warning" : "info",
                Message = $"Returned {findings.Count} compact finding row(s) from {totalFindingRows} total finding row(s) and {waits.Count} compact wait row(s) from {totalWaitRows} total wait row(s)."
            }
        };

        return new AzureSqlCurrentIncidentResponse
        {
            Target = request.Target,
            TotalWaitRows = totalWaitRows,
            TotalFindingRows = totalFindingRows,
            TopWaitTypes = topWaitTypes,
            Summary = summary,
            Waits = waits,
            Findings = findings,
            Handles = BuildCurrentIncidentHandles(request, waits.Count, totalWaitRows, findings.Count, totalFindingRows),
            ResultSets = request.IncludeVerboseResults
                ? BuildResultSets(dataSet, request.MaxRows, (_, index) => $"result_set_{index + 1}")
                : [],
            Notes =
            [
                "This is a compact point-in-time snapshot.",
                "Use azure_sql_fetch_detail_by_handle with returned handles for expanded sections. IncludeVerboseResults remains available for compatibility.",
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

    /// <summary>
    /// Maps a health-check detail request to the detail response model.
    /// </summary>
    internal AzureSqlFetchDetailByHandleResponse MapHealthCheckDetail(
        ProgressiveDisclosureHandlePayload handle,
        int maxRows,
        string requestHandle,
        DataSet dataSet)
    {
        EnsureTableExists(dataSet, 0, handle);
        var items = ToProjectedRows(dataSet, 0, maxRows, HealthCheckPreferredColumns, 1024);

        return CreateItemsDetailResponse(
            handle,
            requestHandle,
            BuildScope(
                ("databaseName", handle.DatabaseName),
                ("minimumPriority", handle.MinimumPriority),
                ("expertMode", handle.ExpertMode ?? false)),
            items,
            "Detail fetched by re-running sp_Blitz with the original request scope.");
    }

    /// <summary>
    /// Maps a BlitzCache detail request to the detail response model.
    /// </summary>
    internal AzureSqlFetchDetailByHandleResponse MapBlitzCacheDetail(
        ProgressiveDisclosureHandlePayload handle,
        int maxRows,
        string requestHandle,
        DataSet dataSet)
    {
        var scope = BuildScope(
            ("databaseName", handle.DatabaseName),
            ("sortOrder", handle.SortOrder),
            ("top", handle.Top),
            ("expertMode", handle.ExpertMode ?? false),
            ("aiMode", handle.AiMode),
            ("aiPromptConfigTable", handle.AiPromptConfigTable),
            ("aiPromptName", handle.AiPromptName));

        return handle.Kind switch
        {
            "queries" => CreateItemsDetailResponse(
                handle,
                requestHandle,
                scope,
                ToProjectedRows(RequireTable(dataSet, 0, handle), maxRows, BlitzCacheQueryColumns, 1600),
                "Detail fetched by re-running sp_BlitzCache with the original request scope."),
            "warning_glossary" => CreateItemsDetailResponse(
                handle,
                requestHandle,
                scope,
                ToProjectedRows(RequireTable(dataSet, 1, handle), Math.Min(maxRows, 100), BlitzCacheWarningGlossaryColumns, 800),
                "Detail fetched by re-running sp_BlitzCache with the original request scope."),
            "ai_prompt" => CreateTextDetailResponse(
                handle,
                requestHandle,
                scope,
                RequireText(dataSet, handle, "AI Prompt", "AIPrompt", "AI_Prompt"),
                "Detail fetched by re-running sp_BlitzCache with the original request scope."),
            "ai_advice" => CreateTextDetailResponse(
                handle,
                requestHandle,
                scope,
                RequireText(dataSet, handle, "AI Advice", "AIAdvice", "AI_Advice"),
                "Detail fetched by re-running sp_BlitzCache with the original request scope."),
            _ => throw new ProgressiveDisclosureException(
                "unknown_kind",
                $"Unknown kind '{handle.Kind}' for parentTool '{handle.ParentTool}'.",
                400)
        };
    }

    /// <summary>
    /// Maps a BlitzIndex detail request to the detail response model.
    /// </summary>
    internal AzureSqlFetchDetailByHandleResponse MapBlitzIndexDetail(
        ProgressiveDisclosureHandlePayload handle,
        int maxRows,
        string requestHandle,
        DataSet dataSet)
    {
        var scope = BuildScope(
            ("databaseName", handle.DatabaseName),
            ("schemaName", handle.SchemaName),
            ("tableName", handle.TableName),
            ("mode", handle.Mode),
            ("thresholdMb", handle.ThresholdMb),
            ("expertMode", handle.ExpertMode ?? false),
            ("aiMode", handle.AiMode),
            ("aiPromptConfigTable", handle.AiPromptConfigTable),
            ("aiPromptName", handle.AiPromptName));

        return handle.Kind switch
        {
            "existing_indexes" => CreateItemsDetailResponse(
                handle,
                requestHandle,
                scope,
                ToProjectedRows(RequireTable(dataSet, 0, handle), maxRows, BlitzIndexExistingColumns, 1200),
                "Detail fetched by re-running sp_BlitzIndex with the original request scope."),
            "missing_indexes" => CreateItemsDetailResponse(
                handle,
                requestHandle,
                scope,
                ToProjectedRows(RequireTable(dataSet, 1, handle), maxRows, BlitzIndexMissingColumns, 1600),
                "Detail fetched by re-running sp_BlitzIndex with the original request scope."),
            "column_data_types" => CreateItemsDetailResponse(
                handle,
                requestHandle,
                scope,
                ToProjectedRows(RequireTable(dataSet, 2, handle), Math.Min(maxRows, 250), BlitzIndexColumnDataTypeColumns, 800),
                "Detail fetched by re-running sp_BlitzIndex with the original request scope."),
            "foreign_keys" => CreateItemsDetailResponse(
                handle,
                requestHandle,
                scope,
                ToProjectedRows(RequireTable(dataSet, 3, handle), Math.Min(maxRows, 250), BlitzIndexForeignKeyColumns, 800),
                "Detail fetched by re-running sp_BlitzIndex with the original request scope."),
            "ai_prompt" => CreateTextDetailResponse(
                handle,
                requestHandle,
                scope,
                RequireText(dataSet, handle, "AI Prompt", "AIPrompt", "AI_Prompt"),
                "Detail fetched by re-running sp_BlitzIndex with the original request scope."),
            "ai_advice" => CreateTextDetailResponse(
                handle,
                requestHandle,
                scope,
                RequireText(dataSet, handle, "AI Advice", "AIAdvice", "AI_Advice"),
                "Detail fetched by re-running sp_BlitzIndex with the original request scope."),
            _ => throw new ProgressiveDisclosureException(
                "unknown_kind",
                $"Unknown kind '{handle.Kind}' for parentTool '{handle.ParentTool}'.",
                400)
        };
    }

    /// <summary>
    /// Maps a current-incident detail request to the detail response model.
    /// </summary>
    internal AzureSqlFetchDetailByHandleResponse MapCurrentIncidentDetail(
        ProgressiveDisclosureHandlePayload handle,
        int maxRows,
        string requestHandle,
        DataSet dataSet)
    {
        var scope = BuildScope(("expertMode", handle.ExpertMode ?? false));

        return handle.Kind switch
        {
            "waits" => CreateItemsDetailResponse(
                handle,
                requestHandle,
                scope,
                ToProjectedRows(RequireTable(dataSet, 0, handle), maxRows, IncidentWaitPreferredColumns, 1024),
                "Detail fetched by re-running sp_BlitzFirst with the original request scope."),
            "findings" => CreateItemsDetailResponse(
                handle,
                requestHandle,
                scope,
                ToProjectedRows(RequireTable(dataSet, 1, handle), maxRows, IncidentFindingPreferredColumns, 1024),
                "Detail fetched by re-running sp_BlitzFirst with the original request scope."),
            _ => throw new ProgressiveDisclosureException(
                "unknown_kind",
                $"Unknown kind '{handle.Kind}' for parentTool '{handle.ParentTool}'.",
                400)
        };
    }

    private static List<string> BuildBlitzCacheNotes(int aiMode, bool includeVerboseResults)
    {
        var notes = new List<string>
        {
            "Query text and large text columns are truncated by default to reduce token usage.",
            "Interpret plan cache results alongside runtime workload context.",
            "Use azure_sql_fetch_detail_by_handle with returned handles for expanded sections. IncludeVerboseResults remains available for compatibility."
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
            "Review missing index suggestions alongside existing workload patterns before applying changes.",
            "Use azure_sql_fetch_detail_by_handle with returned handles for expanded sections. IncludeVerboseResults remains available for compatibility."
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
        int totalFindings,
        int? highestVisiblePriority,
        List<string> visibleFindingGroups)
    {
        var summary = new List<DiagnosticSummary>
        {
            new()
            {
                Title = "Health check",
                Severity = totalFindings > 0 ? "warning" : "info",
                Message = $"Returned {findings.Count} compact finding row(s) from {totalFindings} total finding row(s)."
            }
        };

        if (highestVisiblePriority.HasValue)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "Top priority finding",
                Severity = highestVisiblePriority.Value <= 50 ? "warning" : "info",
                Message = $"The highest visible priority in this compact response is {highestVisiblePriority.Value}."
            });
        }

        if (visibleFindingGroups.Count > 0)
        {
            summary.Add(new DiagnosticSummary
            {
                Title = "Finding groups",
                Severity = "info",
                Message = $"Visible groups: {string.Join(", ", visibleFindingGroups)}."
            });
        }

        return summary;
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
                Rows = ToProjectedRows(table, maxRows, null, 400)
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
                    if (row[column] is not DBNull and not null)
                    {
                        return Convert.ToString(row[column]);
                    }
                }
            }
        }

        return null;
    }

    private static string RequireText(DataSet dataSet, ProgressiveDisclosureHandlePayload handle, params string[] candidateNames)
    {
        var text = ExtractText(dataSet, candidateNames);
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        throw new ProgressiveDisclosureException(
            "section_not_found",
            $"The requested section '{handle.Kind}' is no longer available. Re-run {handle.ParentTool} to fetch a fresh handle.",
            404);
    }

    private static Dictionary<string, object?> BuildScope(params (string Key, object? Value)[] values)
    {
        var scope = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            if (value is not null)
            {
                scope[key] = value;
            }
        }

        return scope;
    }

    private static DataTable RequireTable(DataSet dataSet, int tableIndex, ProgressiveDisclosureHandlePayload handle)
    {
        EnsureTableExists(dataSet, tableIndex, handle);
        return dataSet.Tables[tableIndex];
    }

    private static void EnsureTableExists(DataSet dataSet, int tableIndex, ProgressiveDisclosureHandlePayload handle)
    {
        if (dataSet.Tables.Count > tableIndex)
        {
            return;
        }

        throw new ProgressiveDisclosureException(
            "section_not_found",
            $"The requested section '{handle.Kind}' is no longer available. Re-run {handle.ParentTool} to fetch a fresh handle.",
            404);
    }

    private static AzureSqlFetchDetailByHandleResponse CreateItemsDetailResponse(
        ProgressiveDisclosureHandlePayload handle,
        string requestHandle,
        Dictionary<string, object?> scope,
        List<Dictionary<string, object?>> items,
        string note)
    {
        return new AzureSqlFetchDetailByHandleResponse
        {
            Target = handle.Target,
            ParentTool = handle.ParentTool,
            Kind = handle.Kind,
            Handle = requestHandle,
            Scope = scope,
            Items = items,
            Notes = [note]
        };
    }

    private static AzureSqlFetchDetailByHandleResponse CreateTextDetailResponse(
        ProgressiveDisclosureHandlePayload handle,
        string requestHandle,
        Dictionary<string, object?> scope,
        string content,
        string note)
    {
        return new AzureSqlFetchDetailByHandleResponse
        {
            Target = handle.Target,
            ParentTool = handle.ParentTool,
            Kind = handle.Kind,
            Handle = requestHandle,
            Scope = scope,
            ContentType = "text/plain",
            Content = content,
            Notes = [note]
        };
    }

    private static List<AzureSqlDetailHandle> BuildHealthCheckHandles(
        AzureSqlHealthCheckRequest request,
        int visibleCount,
        int totalCount,
        int? highestVisiblePriority)
    {
        if (totalCount == 0)
        {
            return [];
        }

        return
        [
            new()
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "findings"),
                ParentTool = "azure_sql_health_check",
                Kind = "findings",
                Title = "Health findings",
                Preview = BuildCountPreview("finding", visibleCount, totalCount),
                Severity = highestVisiblePriority.HasValue && highestVisiblePriority.Value <= 50 ? "warning" : "info",
                ItemCount = visibleCount,
                TotalCount = totalCount
            }
        ];
    }

    private static List<AzureSqlDetailHandle> BuildBlitzCacheHandles(
        AzureSqlBlitzCacheRequest request,
        int visibleQueryCount,
        int totalQueryCount,
        int visibleWarningGlossaryCount,
        int totalWarningGlossaryCount,
        bool hasAiPrompt,
        bool hasAiAdvice)
    {
        var handles = new List<AzureSqlDetailHandle>();

        if (totalQueryCount > 0)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "queries"),
                ParentTool = "azure_sql_blitz_cache",
                Kind = "queries",
                Title = "Top cached queries",
                Preview = BuildCountPreview("query", visibleQueryCount, totalQueryCount),
                Severity = "warning",
                ItemCount = visibleQueryCount,
                TotalCount = totalQueryCount
            });
        }

        if (totalWarningGlossaryCount > 0)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "warning_glossary"),
                ParentTool = "azure_sql_blitz_cache",
                Kind = "warning_glossary",
                Title = "Warning glossary",
                Preview = BuildCountPreview("glossary row", visibleWarningGlossaryCount, totalWarningGlossaryCount),
                Severity = "info",
                ItemCount = visibleWarningGlossaryCount,
                TotalCount = totalWarningGlossaryCount
            });
        }

        if (hasAiPrompt)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "ai_prompt"),
                ParentTool = "azure_sql_blitz_cache",
                Kind = "ai_prompt",
                Title = "FRK AI prompt",
                Preview = "Prompt text available.",
                Severity = "info"
            });
        }

        if (hasAiAdvice)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "ai_advice"),
                ParentTool = "azure_sql_blitz_cache",
                Kind = "ai_advice",
                Title = "FRK AI advice",
                Preview = "AI advice text available.",
                Severity = "info"
            });
        }

        return handles;
    }

    private static List<AzureSqlDetailHandle> BuildBlitzIndexHandles(
        AzureSqlBlitzIndexRequest request,
        int visibleExistingIndexCount,
        int totalExistingIndexCount,
        int visibleMissingIndexCount,
        int totalMissingIndexCount,
        int visibleColumnDataTypeCount,
        int totalColumnDataTypeCount,
        int visibleForeignKeyCount,
        int totalForeignKeyCount,
        bool hasAiPrompt,
        bool hasAiAdvice)
    {
        var handles = new List<AzureSqlDetailHandle>();

        if (totalExistingIndexCount > 0)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "existing_indexes"),
                ParentTool = "azure_sql_blitz_index",
                Kind = "existing_indexes",
                Title = "Existing indexes",
                Preview = BuildCountPreview("existing index row", visibleExistingIndexCount, totalExistingIndexCount),
                Severity = "info",
                ItemCount = visibleExistingIndexCount,
                TotalCount = totalExistingIndexCount
            });
        }

        if (totalMissingIndexCount > 0)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "missing_indexes"),
                ParentTool = "azure_sql_blitz_index",
                Kind = "missing_indexes",
                Title = "Missing index suggestions",
                Preview = BuildCountPreview("missing index row", visibleMissingIndexCount, totalMissingIndexCount),
                Severity = "warning",
                ItemCount = visibleMissingIndexCount,
                TotalCount = totalMissingIndexCount
            });
        }

        if (totalColumnDataTypeCount > 0)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "column_data_types"),
                ParentTool = "azure_sql_blitz_index",
                Kind = "column_data_types",
                Title = "Column metadata",
                Preview = BuildCountPreview("column metadata row", visibleColumnDataTypeCount, totalColumnDataTypeCount),
                Severity = "info",
                ItemCount = visibleColumnDataTypeCount,
                TotalCount = totalColumnDataTypeCount
            });
        }

        if (totalForeignKeyCount > 0)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "foreign_keys"),
                ParentTool = "azure_sql_blitz_index",
                Kind = "foreign_keys",
                Title = "Foreign keys",
                Preview = BuildCountPreview("foreign key row", visibleForeignKeyCount, totalForeignKeyCount),
                Severity = "info",
                ItemCount = visibleForeignKeyCount,
                TotalCount = totalForeignKeyCount
            });
        }

        if (hasAiPrompt)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "ai_prompt"),
                ParentTool = "azure_sql_blitz_index",
                Kind = "ai_prompt",
                Title = "FRK AI prompt",
                Preview = "Prompt text available.",
                Severity = "info"
            });
        }

        if (hasAiAdvice)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "ai_advice"),
                ParentTool = "azure_sql_blitz_index",
                Kind = "ai_advice",
                Title = "FRK AI advice",
                Preview = "AI advice text available.",
                Severity = "info"
            });
        }

        return handles;
    }

    private static List<AzureSqlDetailHandle> BuildCurrentIncidentHandles(
        AzureSqlCurrentIncidentRequest request,
        int visibleWaitCount,
        int totalWaitCount,
        int visibleFindingCount,
        int totalFindingCount)
    {
        var handles = new List<AzureSqlDetailHandle>();

        if (totalWaitCount > 0)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "waits"),
                ParentTool = "azure_sql_current_incident",
                Kind = "waits",
                Title = "Current waits",
                Preview = BuildCountPreview("wait row", visibleWaitCount, totalWaitCount),
                Severity = "warning",
                ItemCount = visibleWaitCount,
                TotalCount = totalWaitCount
            });
        }

        if (totalFindingCount > 0)
        {
            handles.Add(new AzureSqlDetailHandle
            {
                Handle = ProgressiveDisclosureHandleCodec.CreateHandle(request, "findings"),
                ParentTool = "azure_sql_current_incident",
                Kind = "findings",
                Title = "Incident findings",
                Preview = BuildCountPreview("finding", visibleFindingCount, totalFindingCount),
                Severity = "warning",
                ItemCount = visibleFindingCount,
                TotalCount = totalFindingCount
            });
        }

        return handles;
    }

    private static string BuildCountPreview(string noun, int visibleCount, int totalCount)
    {
        return visibleCount == totalCount
            ? $"{visibleCount} {Pluralize(noun, visibleCount)} shown."
            : $"{visibleCount} of {totalCount} {Pluralize(noun, totalCount)} shown in the compact response.";
    }

    private static string Pluralize(string noun, int count)
        => count == 1 ? noun : $"{noun}s";

    private static int? GetHighestVisiblePriority(List<Dictionary<string, object?>> rows)
    {
        return rows
            .Select(row => TryGetInt(row, "Priority"))
            .Where(priority => priority.HasValue)
            .Min();
    }

    private static List<string> GetDistinctStringValues(
        List<Dictionary<string, object?>> rows,
        string key,
        int take)
    {
        return rows
            .Select(row => Convert.ToString(GetValue(row, key)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Cast<string>()
            .ToList();
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
        if (dataSet.Tables.Count <= tableIndex)
        {
            return [];
        }

        return ToProjectedRows(dataSet.Tables[tableIndex], maxRows, preferredColumns, maxStringLength);
    }

    private static List<Dictionary<string, object?>> ToProjectedRows(
        DataTable table,
        int maxRows,
        IReadOnlyList<string>? preferredColumns,
        int maxStringLength)
    {
        var results = new List<Dictionary<string, object?>>();
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
