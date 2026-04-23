# Blitz Bridge v1 PRD
**Status:** Implementation-Ready  
**Last Updated:** 2026-04-23  
**Owner:** Kori Francis  

---

## Problem Statement

Azure SQL deployments require operational observability to diagnose performance issues, lock contention, missing indexes, and resource constraints. Brent Ozar's First Responder Kit (FRK) provides battle-tested T-SQL procedures that uncover these problems. However, integrating FRK into agent-friendly workflows requires:

1. **Safe execution boundaries** — prevent arbitrary SQL or schema mutations
2. **Deterministic configuration** — avoid runtime discovery of databases or procedures
3. **Structured diagnostics** — expose FRK results as machine-readable tools
4. **Preconfigured targets** — no connection-string discovery; all SQL targets must be known server-side

Blitz Bridge solves this by acting as a read-only MCP server bridge that exposes FRK diagnostics through a constrained, agent-friendly surface.

---

## Goals

1. **Enable agents to run FRK diagnostics** — agents can invoke `sp_Blitz`, `sp_BlitzCache`, `sp_BlitzIndex`, `sp_BlitzFirst`, and `sp_BlitzLock` against preconfigured targets without access to underlying connection strings
2. **Enforce least-privilege access** — no write operations, no arbitrary SQL, no schema changes
3. **Provide AI-optimized output** — surface FRK-generated AI prompts and advice when available (AiMode 2 and 1)
4. **Enable local development with Aspire** — support parameterized configuration and local testing through Aspire orchestration
5. **Standardize diagnostic workflows** — offer a single MCP server that multiple AI agents can query for database health, query performance, and index diagnostics

---

## Non-Goals

- **Arbitrary SQL execution** — no general-purpose query runner
- **Real-time monitoring dashboard** — this is a diagnostic bridge, not a monitoring system
- **SQL Server management** — no backups, restores, or DDL operations
- **Multi-tenant isolation** — Azure SQL targets are preconfigured by administrators; runtime isolation is not a goal
- **Direct cloud cost optimization** — cost is context; diagnostics inform optimization, but are not substitutes for it
- **Replication or data movement** — this is read-only diagnostics only

---

## Users / Personas

1. **AI/LLM Agents** (primary)
   - Need deterministic, repeatable access to SQL diagnostics
   - Consume structured FRK output
   - Cannot manage connections or credentials
   - Example: autonomous database tuning agents, performance incident responders

2. **Database Administrators** (secondary)
   - Configure profiles and allowlists server-side
   - Operate the Blitz Bridge deployment
   - Monitor diagnostic invocations (via logs/telemetry)
   - Decide which targets agents can access

3. **Development Teams** (tertiary)
   - Run diagnostics against dev/test databases during local development
   - Use Aspire AppHost to spin up isolated diagnostic environments
   - Validate schema or query performance locally before production

---

## Scope

### MVP (v1)

**MCP Tools:**
- `azure_sql_target_capabilities` — probe available FRK procedures and configured databases
- `azure_sql_blitz_cache` — run `sp_BlitzCache` with SortOrder control (executions, cpu, reads, duration)
- `azure_sql_blitz_index` — run `sp_BlitzIndex` for single-table index analysis
- `azure_sql_health_check` — run `sp_Blitz` for overall health assessment
- `azure_sql_current_incident` — run `sp_BlitzFirst` for immediate incident diagnostics

**Configuration:**
- Profile-based target registration (name → connection string)
- Per-profile allowed databases list (optional restrictive allowlist)
- Per-profile allowed procedures list (default: FRK subset)
- Per-profile command timeout (default: 60 seconds)
- AiMode setting per profile (0: off, 1: FRK direct AI calls, 2: FRK-generated prompts only)

**Deployment:**
- .NET 8+ console/hosted service
- Aspire AppHost for local orchestration with parameterized secrets
- OpenTelemetry observability integration
- Health checks via `/health` endpoint

**Safety Constraints:**
- No write operations; read-only connection intent enforced
- No dynamic procedure allowlist bypass
- No database enumeration outside configured allowlists
- Connection strings stored server-side only; never exposed to clients

### Out of Scope (Future Releases)

- Additional FRK procedures (`sp_BlitzTrace`, `sp_BlitzAnalysis`, custom stored procedures)
- Real-time execution plan capture (`sp_BlitzCache @LivePlan`)
- Async job coordination or scheduled diagnostics
- Multi-target aggregation (combining results from multiple servers)
- Performance baseline/trend tracking
- Agent-driven remediation (e.g., automatic index creation)
- Alternative databases (SQL Server on-premises, other cloud providers in v1)

---

## Functional Requirements

### FR1: MCP Tool Interface

**Requirement:** The server SHALL expose the following MCP tools:

| Tool | Purpose | Key Parameters | Output |
|------|---------|-----------------|--------|
| `azure_sql_target_capabilities` | Query target profile capabilities | `target_profile` (string) | Installed FRK procedures, current execution database, allowlisted databases, AiMode, FRK version hints |
| `azure_sql_blitz_cache` | Query plan cache analysis | `target_profile`, `sort_order` (enum: executions\|cpu\|reads\|duration) | Query execution summary, FRK recommendations, AI advice if AiMode > 0 |
| `azure_sql_blitz_index` | Analyze single table indexes | `target_profile`, `database_name`, `table_name` | Index recommendations, missing index suggestions, FRK advice, AI hints |
| `azure_sql_health_check` | Full database health scan | `target_profile`, `database_name` (optional) | Priority-ranked findings, blocking issues, disk usage, CPU, memory stats, FRK severity |
| `azure_sql_current_incident` | Immediate incident diagnostics | `target_profile`, `database_name` (optional) | Active blocks, deadlock victims, long-running queries, FRK immediate actions |

### FR2: Target Configuration

**Requirement:** The server SHALL support a configuration model:

```
SqlTargets:
  Profiles:
    <profile_name>:
      ConnectionString: <ADO.NET connection string>
      AllowedDatabases: [ <db>, ... ] (optional; omit for all configured databases)
      AllowedProcedures: [ "sp_Blitz", "sp_BlitzCache", ... ]
      Enabled: true|false
      CommandTimeoutSeconds: <integer, default 60>
      AiMode: 0|1|2 (default: 2)
```

- Profiles are configured at startup and validated on first use
- Connection strings are **never** logged or exposed to clients
- AllowedDatabases restricts procedure execution to named databases only

### FR3: Read-Only Enforcement

**Requirement:** The server SHALL:

- Open SQL connections in read-only mode (MARS disabled, snapshot isolation or read-only intent)
- Reject any T-SQL containing INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, or EXECUTE of unlisted procedures
- Log all procedure invocation attempts (including rejected ones)
- Return errors to the client if a procedure is not in the AllowedProcedures list

### FR4: FRK Output Handling

**Requirement:** The server SHALL:

- Execute FRK procedures with correct parameter mapping (e.g., `@SortOrder` for `sp_BlitzCache`)
- Capture tabular results and return them as JSON
- Extract and surface FRK-generated AI advice when present (e.g., `[@Advice]` column output from `sp_BlitzCache @AI = 2`)
- Map AiMode to FRK procedure parameters:
  - AiMode 0: No `@AI` parameter
  - AiMode 1: `@AI = 1` (direct FRK AI provider calls)
  - AiMode 2: `@AI = 2` (FRK-generated prompts only)

### FR5: Error Handling and Feedback

**Requirement:** The server SHALL:

- Return structured error responses distinguishing between:
  - Configuration errors (profile not found, database not in allowlist)
  - Execution errors (timeout, connection failure, procedure error)
  - Authorization errors (procedure not allowed, database not allowed)
- Log all errors with request context (profile, procedure, elapsed time)
- Never expose raw T-SQL errors; sanitize them before returning to client

---

## Non-Functional Requirements

### NFR1: Security

1. **Read-Only Guarantee**
   - All SQL connections use read-only intent; connection strings contain `ApplicationIntent=ReadOnly` or equivalent
   - No transaction commits permitted
   - Monitoring/logging of any attempted write operations

2. **Credential Protection**
   - Connection strings stored in Aspire secrets or environment, never in code
   - No connection string logging (sanitize logs)
   - Use Azure AD authentication where possible (Active Directory Default)

3. **Authorization**
   - Allowlists for databases and procedures validated before execution
   - Profile access not differentiated by client identity (v1); profiles are server-side decisions
   - MCP request signing/validation deferred to MCP framework

4. **Audit Trail**
   - All procedure invocations logged with timestamp, profile, database, procedure, status (success/failure)
   - Warnings for repeated failures or anomalous patterns

### NFR2: Reliability

1. **Connection Pooling**
   - Use connection pooling to minimize connection overhead
   - Validate connections on borrow; replace failed connections
   - Configurable pool size (default: 10)

2. **Timeout Handling**
   - Command timeout configurable per profile (default: 60s)
   - Connection timeout: 30 seconds (hard limit)
   - Graceful cancellation of long-running queries on timeout

3. **Availability**
   - Health check endpoint (`GET /health`) returns connectivity status for each enabled profile
   - Startup validation: test each profile on application boot; warn if unavailable
   - Retry logic for transient failures (3 retries with exponential backoff for transient errors)

4. **Data Consistency**
   - Read consistency model: use default snapshot isolation or read-committed with READUNCOMMITTED hint
   - State per-profile (no cross-profile transactions)

### NFR3: Observability

1. **Logging**
   - Structured logging (JSON) of all diagnostic invocations
   - Fields: timestamp, profile, database, procedure, status, duration_ms, row_count, errors
   - Sensitive redaction: no raw connection strings, no actual passwords

2. **Metrics**
   - Invocation count per procedure, per profile
   - Execution duration (percentiles: p50, p95, p99)
   - Error rate per procedure
   - Connection pool utilization

3. **Distributed Tracing (OpenTelemetry)**
   - Trace each MCP request through to SQL execution
   - Span events for configuration validation, connection acquisition, query execution
   - Integration with Azure Application Insights (or compatible backends)

4. **Health Checks**
   - `/health` endpoint returns per-profile connectivity status
   - Liveness probe: server responds to requests
   - Readiness probe: all enabled profiles are connectable

### NFR4: Performance

1. **Latency Targets**
   - FRK procedure execution: typically 1–30 seconds depending on database size
   - Server-side latency (MCP overhead, JSON serialization): < 500ms for result sets < 50MB
   - Timeout after 60 seconds by default (configurable)

2. **Throughput**
   - Support concurrent requests (at least 10 simultaneous MCP calls)
   - Connection pooling ensures efficient reuse

3. **Memory and Compute**
   - Docker image footprint: < 200 MB (with .NET 8 trimmed runtime)
   - Peak memory per request: < 100 MB for typical diagnostic result sets

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                   AI Agents / MCP Clients                   │
│             (LLMs, autonomous diagnostic tools)             │
└────────────────────────────┬────────────────────────────────┘
                             │
                    MCP Protocol (JSON-RPC)
                             │
┌────────────────────────────▼────────────────────────────────┐
│        Blitz Bridge MCP Server (.NET 8 Hosted Service)      │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  MCP Handler Layer (Tool Invocation & Validation)   │  │
│  │  - azure_sql_target_capabilities                    │  │
│  │  - azure_sql_blitz_*                                │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Configuration & Security Layer                      │  │
│  │  - Profile validation                               │  │
│  │  - Database/procedure allowlist enforcement          │  │
│  │  - Read-only mode verification                       │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  SQL Execution Layer                                │  │
│  │  - Connection pooling (ADO.NET)                      │  │
│  │  - FRK parameter mapping                            │  │
│  │  - Result serialization (tabular → JSON)            │  │
│  │  - AI advice extraction                             │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Observability (OpenTelemetry)                       │  │
│  │  - Structured logging                               │  │
│  │  - Distributed tracing                              │  │
│  │  - Health checks                                    │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────────────────────┬────────────────────────────────┘
                             │
              SQL (read-only) connections over TLS/mTLS
                             │
┌────────────────────────────▼────────────────────────────────┐
│              Azure SQL Database (or SQL Server)             │
│              (FRK procedures pre-installed)                 │
└────────────────────────────────────────────────────────────┘
```

**Key Design Principles:**
- **Single Responsibility:** MCP handlers only marshal requests; SQL logic is isolated
- **Fail Secure:** Reject unknown profiles, databases, or procedures immediately
- **Observability First:** All invocations are traced; errors are verbose in logs, silent to clients (for safety)
- **Stateless Requests:** No per-client state; each MCP call is independent

---

## Safety & Compliance Constraints

### Read-Only Guarantee

1. **Connection String Enforcement**
   - All connection strings include `ApplicationIntent=ReadOnly` (SQL Server 2016+) or equivalent
   - Audit: connection strings are validated on startup; any misconfiguration blocks server startup

2. **T-SQL Validation**
   - Before execution, all procedure names are cross-checked against AllowedProcedures
   - No dynamic SQL generation; parameters are passed as SP arguments, not string concatenation

3. **Logging & Audit**
   - All invocations logged with result row counts and status
   - Failed attempts (unauthorized profile, database, procedure) logged with full context
   - Integration with Azure Audit logs (optional, customer environment)

### Least-Privilege Model

1. **Database Allowlist**
   - Each profile's AllowedDatabases restricts scope
   - If AllowedDatabases is empty/omitted, the default execution database only is used
   - Rationale: prevent accidental scans of sensitive databases

2. **Procedure Allowlist**
   - Default: `sp_Blitz`, `sp_BlitzCache`, `sp_BlitzFirst`, `sp_BlitzIndex`, `sp_BlitzLock`, `sp_BlitzWho`
   - Administrators can restrict further (e.g., disable `sp_BlitzCache` if monitoring is preferred)

3. **Role Segregation (Azure SQL)**
   - MCP server connects as a service account with minimal privileges:
     - EXECUTE on allowed FRK procedures only
     - READ on system DMVs (required by FRK)
     - No CREATE, ALTER, DROP, or DML permissions

---

## Configuration Model

### Configuration Schema (JSON)

```json
{
  "SqlTargets": {
    "Profiles": {
      "primary-prod": {
        "ConnectionString": "Server=tcp:mydb.database.windows.net;Database=DBAtools;Authentication=Active Directory Default;Encrypt=True;ApplicationIntent=ReadOnly;",
        "AllowedDatabases": ["AppDb", "ReportingDb"],
        "AllowedProcedures": ["sp_Blitz", "sp_BlitzCache", "sp_BlitzIndex"],
        "Enabled": true,
        "CommandTimeoutSeconds": 120,
        "AiMode": 2
      },
      "dev-local": {
        "ConnectionString": "Server=(local);Database=DBAtools;Integrated Security=True;Encrypt=True;ApplicationIntent=ReadOnly;",
        "AllowedDatabases": [],
        "AllowedProcedures": ["sp_Blitz", "sp_BlitzCache", "sp_BlitzIndex", "sp_BlitzFirst", "sp_BlitzLock", "sp_BlitzWho"],
        "Enabled": true,
        "CommandTimeoutSeconds": 60,
        "AiMode": 0
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "BlitzBridge": "Debug"
    }
  },
  "Telemetry": {
    "Enabled": true,
    "ExportInterval": 5000
  }
}
```

### Configuration Validation Rules

1. **Startup Validation**
   - Each enabled profile must have a valid connection string
   - Connection strings are tested; if a test connection fails, server logs a warning but continues (degraded mode)
   - All profiles listed in AllowedProcedures must correspond to known FRK procedures

2. **Runtime Validation**
   - Profile exists in configuration
   - Database (if specified) is in AllowedDatabases
   - Procedure is in AllowedProcedures
   - Connection can be acquired within timeout
   - Command executes within CommandTimeoutSeconds

3. **Security Validation**
   - Connection strings contain `ApplicationIntent=ReadOnly` (warning if missing, but not blocking in v1)
   - AiMode is one of { 0, 1, 2 }

---

## API/Tool Surface

### MCP Tools (Detailed Specifications)

#### Tool: `azure_sql_target_capabilities`

**Purpose:** Discover available FRK procedures and configuration for a target profile.

**Input:**
```json
{
  "target_profile": "string (profile name)"
}
```

**Output:**
```json
{
  "profile": "string",
  "enabled": "boolean",
  "installed_procedures": ["sp_Blitz", "sp_BlitzCache", ...],
  "allowed_procedures": ["sp_Blitz", "sp_BlitzCache", ...],
  "allowed_databases": ["AppDb", "ReportingDb"] || null,
  "current_database": "string",
  "ai_mode": 0 | 1 | 2,
  "command_timeout_seconds": "integer",
  "connectivity_status": "OK" | "UNREACHABLE",
  "frk_version": "string or null"
}
```

**Errors:**
- Profile not found → 400 Bad Request
- Connection unreachable → 503 Service Unavailable

---

#### Tool: `azure_sql_blitz_cache`

**Purpose:** Analyze plan cache (query performance).

**Input:**
```json
{
  "target_profile": "string",
  "sort_order": "executions" | "cpu" | "reads" | "duration",
  "database_name": "string (optional; uses allowed default if omitted)"
}
```

**Output:**
```json
{
  "profile": "string",
  "database": "string",
  "executed_at_utc": "ISO8601",
  "sort_order": "executions" | "cpu" | "reads" | "duration",
  "rows_returned": "integer",
  "results": [
    {
      "query_hash": "string",
      "sql_text": "string (truncated to 512 chars)",
      "execution_count": "integer",
      "cpu_ms": "integer",
      "total_reads": "integer",
      "total_writes": "integer",
      "avg_duration_ms": "integer",
      "plan_handle": "string",
      "creation_time": "ISO8601"
    },
    ...
  ],
  "ai_advice": "string or null (from FRK if AiMode >= 2)"
}
```

**Errors:**
- Profile not found → 400 Bad Request
- Database not in allowlist → 403 Forbidden
- Command timeout → 504 Gateway Timeout
- Procedure not allowed → 403 Forbidden

---

#### Tool: `azure_sql_blitz_index`

**Purpose:** Analyze table indexes.

**Input:**
```json
{
  "target_profile": "string",
  "database_name": "string",
  "table_name": "string (schema.table format)",
  "index_priority": 1 | 2 | 3 | 4 | 5 (optional; default: all priorities)
}
```

**Output:**
```json
{
  "profile": "string",
  "database": "string",
  "table": "string",
  "executed_at_utc": "ISO8601",
  "findings": [
    {
      "index_name": "string or null",
      "index_type": "CLUSTERED" | "NONCLUSTERED" | "COLUMNSTORE" | "HEAPED",
      "column_names": "string",
      "size_mb": "decimal",
      "reads": "integer",
      "writes": "integer",
      "last_used": "ISO8601 or null",
      "recommendation": "string (from FRK)",
      "priority": 1 | 2 | 3 | 4 | 5
    },
    ...
  ],
  "ai_advice": "string or null (from FRK if AiMode >= 2)",
  "rows_returned": "integer"
}
```

**Errors:** Similar to `azure_sql_blitz_cache`.

---

#### Tool: `azure_sql_health_check`

**Purpose:** Run full database health diagnostics.

**Input:**
```json
{
  "target_profile": "string",
  "database_name": "string (optional; uses allowed default if omitted)"
}
```

**Output:**
```json
{
  "profile": "string",
  "database": "string",
  "executed_at_utc": "ISO8601",
  "findings": [
    {
      "check": "string (e.g., 'Missing Indexes')",
      "priority": 1 | 2 | 3 | 4 | 5,
      "finding_group": "string (e.g., 'Performance')",
      "details": "string (truncated to 1KB)",
      "recommendation": "string"
    },
    ...
  ],
  "summary": {
    "total_findings": "integer",
    "critical_count": "integer",
    "warning_count": "integer",
    "info_count": "integer"
  },
  "rows_returned": "integer"
}
```

---

#### Tool: `azure_sql_current_incident`

**Purpose:** Get immediate incident diagnostics (active blocks, deadlocks, long-running queries).

**Input:**
```json
{
  "target_profile": "string",
  "database_name": "string (optional)"
}
```

**Output:**
```json
{
  "profile": "string",
  "database": "string or null",
  "executed_at_utc": "ISO8601",
  "incidents": [
    {
      "incident_type": "BLOCKING_CHAIN" | "DEADLOCK_VICTIM" | "LONG_RUNNING_QUERY" | "HIGH_CPU",
      "severity": "CRITICAL" | "HIGH" | "MEDIUM",
      "session_id": "integer",
      "sql_text": "string (truncated)",
      "wait_time_seconds": "integer",
      "cpu_ms": "integer",
      "start_time": "ISO8601",
      "blocking_session_id": "integer or null",
      "recommendation": "string"
    },
    ...
  ],
  "rows_returned": "integer"
}
```

---

### MCP Resource Types

None in v1. All diagnostics are stateless tool invocations.

---

## Acceptance Criteria

### AC1: Core MCP Tools Functional

- [ ] `azure_sql_target_capabilities` returns accurate profile configuration and FRK version
- [ ] `azure_sql_blitz_cache` executes `sp_BlitzCache` with correct parameter mapping and sorts results by requested order
- [ ] `azure_sql_blitz_index` executes `sp_BlitzIndex` and surfaces index recommendations and AI advice
- [ ] `azure_sql_health_check` executes `sp_Blitz` and returns priority-ranked findings
- [ ] `azure_sql_current_incident` executes `sp_BlitzFirst` and identifies active incidents

### AC2: Security & Authorization

- [ ] Read-only connection intent enforced (ApplicationIntent=ReadOnly or equivalent)
- [ ] Requests for unauthorized profiles, databases, or procedures are rejected with 403 Forbidden
- [ ] Sensitive data (connection strings, raw errors) not exposed to clients
- [ ] All invocations are logged; failed attempts logged with full context

### AC3: Configuration & Validation

- [ ] Profiles load from configuration file at startup
- [ ] Invalid profiles or missing connection strings logged as warnings; startup not blocked
- [ ] AllowedDatabases allowlist enforced (if non-empty)
- [ ] AllowedProcedures allowlist enforced (default: FRK subset)

### AC4: Error Handling

- [ ] Profile not found → 400 Bad Request with message "Profile not found: {name}"
- [ ] Database not allowed → 403 Forbidden with message "Database not in allowlist"
- [ ] Procedure not allowed → 403 Forbidden with message "Procedure not allowed"
- [ ] Connection timeout → 504 Gateway Timeout with message "Connection timeout after {seconds}s"
- [ ] Command timeout → 504 Gateway Timeout with message "Query timeout after {seconds}s"

### AC5: AI Mode Functionality

- [ ] AiMode 0: FRK procedures execute without `@AI` parameter; no AI advice in output
- [ ] AiMode 1: `@AI = 1` passed to FRK procedures; direct AI provider calls invoked (if configured in database)
- [ ] AiMode 2: `@AI = 2` passed; FRK-generated prompts returned in `ai_advice` field

### AC6: Observability

- [ ] `/health` endpoint returns 200 OK when server is running and at least one profile is connectable
- [ ] Structured JSON logs for all invocations: timestamp, profile, procedure, status, duration_ms
- [ ] OpenTelemetry traces span MCP requests through to SQL execution
- [ ] Metrics exported for procedure invocations and execution durations

### AC7: Aspire Integration

- [ ] AppHost project loads profiles from `appsettings.json` and Aspire parameters
- [ ] Connection string can be supplied via Aspire secrets
- [ ] Aspire dashboard displays MCP server resource state and health
- [ ] Local development against dev/test Azure SQL databases works end-to-end

---

## Milestones / Work Breakdown

### Milestone 1: MCP Core & SQL Execution (Week 1–2)
- [ ] Set up MCP server scaffold (.NET 8, json-rpc)
- [ ] Implement configuration schema and profile validation
- [ ] Implement SQL connection pooling and read-only enforcement
- [ ] Implement `azure_sql_target_capabilities` tool
- [ ] Implement `azure_sql_blitz_cache` tool with SortOrder parameter mapping
- [ ] Unit tests for configuration, connection pooling, parameter validation

**Deliverable:** MCP server boots, loads profiles, and can execute `sp_BlitzCache` with correct output.

### Milestone 2: Remaining FRK Tools & Authorization (Week 2–3)
- [ ] Implement `azure_sql_blitz_index` tool
- [ ] Implement `azure_sql_health_check` tool
- [ ] Implement `azure_sql_current_incident` tool
- [ ] Implement allowlist enforcement (database + procedure)
- [ ] Implement error handling and structured responses
- [ ] Integration tests against real Azure SQL (or local SQL Server)

**Deliverable:** All five MCP tools functional; authorization policies enforced.

### Milestone 3: Observability & Health (Week 3–4)
- [ ] Implement `/health` endpoint
- [ ] Implement structured JSON logging
- [ ] Integrate OpenTelemetry for tracing and metrics
- [ ] Startup validation: test all enabled profiles on boot
- [ ] Configuration validation: reject invalid profiles or missing connection strings

**Deliverable:** Server emits logs and traces; health checks work; deployment observability in place.

### Milestone 4: Aspire Integration & Local Dev (Week 4)
- [ ] Set up AppHost project with MCP server and external SQL target
- [ ] Implement Aspire parameter binding for profiles
- [ ] Test local orchestration with Aspire dashboard
- [ ] Document setup and configuration for developers

**Deliverable:** Developers can `dotnet run` from AppHost and invoke diagnostics locally.

### Milestone 5: Testing, Docs & Release (Week 5)
- [ ] Comprehensive integration tests (all tools, error paths, authorization)
- [ ] Load testing (concurrent MCP requests)
- [ ] Security review: read-only enforcement, credential protection
- [ ] README updates with deployment and configuration examples
- [ ] v1.0 release notes

**Deliverable:** Production-ready v1.0 with full test coverage and documentation.

---

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|-----------|
| **Connection string exposure** | Medium | Critical | Use Aspire secrets; audit logging; sanitize logs; code review |
| **Write operation bypassed via dynamic SQL** | Low | Critical | Use parameterized SP execution only; reject dynamic SQL; static allowlist validation |
| **FRK procedure version mismatch** | Medium | High | Startup validation probes installed procedures; log version mismatches; document supported versions |
| **Long-running queries block other clients** | Medium | High | Per-profile command timeout (configurable); query cancellation on timeout |
| **Connection pool exhaustion** | Low | Medium | Monitor pool utilization; set reasonable pool size; log warnings on near-exhaustion |
| **Transient connection failures** | Medium | Medium | Retry logic with exponential backoff; fail-open for health checks (return degraded, not 500) |
| **Unauthorized database access** | Low | High | AllowedDatabases allowlist strictly enforced; startup validation; no dynamic database discovery |
| **Performance degradation under load** | Medium | Medium | Load testing before release; metrics and alerts; horizontal scaling via connection pooling |
| **Misconfigured AiMode causes unexpected costs** | Low | Medium | Document AiMode behavior and cost implications; provide examples; validation in startup |

---

## Open Questions

1. **Cost governance for AiMode 1:** When FRK uses external AI providers (e.g., OpenAI), who budgets and monitors costs? Should the MCP server apply quotas?
   - **Owner:** Kori Francis / Hockney
   - **Timeline:** Decide before AiMode 1 production use

2. **Multi-region targets:** Should v1 support targets across multiple regions? Or is a single region sufficient for MVP?
   - **Owner:** Keaton
   - **Timeline:** Scope decision before Milestone 1

3. **Database enumeration:** Should `azure_sql_target_capabilities` return detailed schema info (table count, size, etc.)? Or just capability flags?
   - **Owner:** Hockney
   - **Timeline:** Decide before Milestone 1

4. **Agent authentication:** In v1, all clients with MCP access get all allowed profiles. Should future versions support per-agent allowlists?
   - **Owner:** Keaton
   - **Timeline:** Post-v1 feature request; defer to v2 roadmap

5. **Result pagination:** Should large result sets be paginated (e.g., 1000 rows per page)? Or stream all results?
   - **Owner:** Fenster
   - **Timeline:** Decide before Milestone 1

6. **Baseline performance targets:** What are acceptable latencies? 1s, 5s, 10s for typical queries?
   - **Owner:** McManus
   - **Timeline:** Finalize before Milestone 2 testing

---

## Glossary

- **Blitz Bridge:** This MCP server; the read-only diagnostic bridge
- **FRK:** Brent Ozar's SQL Server First Responder Kit
- **MCP:** Model Context Protocol (LLM integration standard)
- **Profile:** Named configuration of a SQL target (connection string, allowlists, timeout)
- **Allowlist:** Restrictive set of databases or procedures a profile can access
- **AI Mode:** Configuration flag (0/1/2) controlling FRK's AI parameter usage
- **AiMode 0:** No AI parameters; raw FRK output only
- **AiMode 1:** FRK calls external AI provider directly (requires credentials in database context)
- **AiMode 2:** FRK generates AI prompts; server surfaces them without provider calls
- **Read-Only Intent:** SQL connection configured to disallow write operations
- **DMVs:** Dynamic Management Views (SQL system objects for diagnostics)

---

## Document History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-23 | Verbal (Copilot) | Initial v1 PRD bootstrap from project context |
|      |        |        |

