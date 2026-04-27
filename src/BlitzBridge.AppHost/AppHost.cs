var builder = DistributedApplication.CreateBuilder(args);

// Add the following line to configure the Azure App Container environment
builder.AddAzureContainerAppEnvironment("env");

var targetConnectionString = builder.AddParameter("primary-sql-target-connection-string", secret: true);
var targetDatabaseName = builder.AddParameter("primary-sql-target-database");
var commandTimeoutSeconds = builder.AddParameter("primary-sql-target-command-timeout-seconds");

builder.AddProject<Projects.BlitzBridge_McpServer>("blitzbridge-mcp")
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
