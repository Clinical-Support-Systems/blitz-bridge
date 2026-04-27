using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// The Azure Container Apps environment must be added to the builder
// before any projects are added, so that the environment variables
// are properly injected into the projects.
builder.AddAzureContainerAppEnvironment("env");

// Add parameters for the SQL target connection string, database name,
// and command timeout. These will be used to configure the
// BlitzBridge MCP Server project.
var targetConnectionString = builder.AddParameter("primary-sql-target-connection-string", true);
var targetDatabaseName = builder.AddParameter("primary-sql-target-database");
var commandTimeoutSeconds = builder.AddParameter("primary-sql-target-command-timeout-seconds");

// Add the BlitzBridge MCP Server project to the builder, and
// configure it with the necessary environment variables for the SQL
// target connection string, database name, command timeout, and
// allowed procedures. The allowed procedures are the stored procedures
// that the MCP Server will allow to be executed against the SQL target.
builder.AddProject<BlitzBridge_McpServer>("blitzbridge-mcp")
    .WithExternalHttpEndpoints()
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__ConnectionString", targetConnectionString)
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__AllowedDatabases__0", targetDatabaseName)
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__AllowedProcedures__0", "sp_Blitz")
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__AllowedProcedures__1", "sp_BlitzCache")
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__AllowedProcedures__2", "sp_BlitzFirst")
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__AllowedProcedures__3", "sp_BlitzIndex")
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__AllowedProcedures__4", "sp_BlitzLock")
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__AllowedProcedures__5", "sp_BlitzWho")
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__Enabled", "true")
    .WithEnvironment("SqlTargets__Profiles__primary-sql-target__CommandTimeoutSeconds", commandTimeoutSeconds)
    .WithEnvironment("BlitzBridge__DefaultTarget", "primary-sql-target");

builder.Build().Run();