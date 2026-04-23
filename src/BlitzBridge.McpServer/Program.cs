using BlitzBridge.McpServer.Configuration;
using BlitzBridge.McpServer.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

var startup = StartupConfiguration.Parse(args);
if (startup.ShouldFailFast)
{
    Console.Error.WriteLine(startup.FailureMessage);
    return startup.ExitCode;
}

if (startup.TransportMode == TransportMode.Stdio)
{
    return await RunStdioAsync(startup);
}

return await RunHttpAsync(startup, args);

static async Task<int> RunStdioAsync(StartupConfiguration startup)
{
    var builder = Host.CreateApplicationBuilder();
    builder.Configuration.Sources.Clear();
    builder.Configuration.AddJsonFile(startup.ConfigPath!, optional: false, reloadOnChange: false);
    builder.Configuration.AddEnvironmentVariables();

    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(LogLevel.None);

    ConfigureSharedServices(builder.Services, builder.Configuration);
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    var host = builder.Build();
    var profileValidationErrors = SqlTargetOptionsValidator.Validate(
        host.Services.GetRequiredService<IOptions<SqlTargetOptions>>().Value);
    if (profileValidationErrors.Count > 0)
    {
        foreach (var error in profileValidationErrors)
        {
            Console.Error.WriteLine(error);
        }

        return 2;
    }

    await host.RunAsync();
    return 0;
}

static async Task<int> RunHttpAsync(StartupConfiguration startup, string[] args)
{
    if (!string.IsNullOrWhiteSpace(startup.ConfigPath))
    {
        Console.Error.WriteLine("--config is ignored when --transport http is used.");
    }

    var builder = WebApplication.CreateBuilder(args);
    builder.AddServiceDefaults();

    ConfigureSharedServices(builder.Services, builder.Configuration);
    builder.Services.AddMcpServer()
        .WithHttpTransport(s =>
        {
            s.Stateless = true;
        })
        .WithToolsFromAssembly();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    var app = builder.Build();
    var profileValidationErrors = SqlTargetOptionsValidator.Validate(
        app.Services.GetRequiredService<IOptions<SqlTargetOptions>>().Value);
    if (profileValidationErrors.Count > 0)
    {
        foreach (var error in profileValidationErrors)
        {
            Console.Error.WriteLine(error);
        }

        return 2;
    }

    app.UseCors();

    app.MapDefaultEndpoints();
    app.MapGet("/health", () => Results.Ok("healthy"));
    app.MapMcp("/mcp");

    await app.RunAsync();
    return 0;
}

static void ConfigureSharedServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<SqlTargetOptions>(
        configuration.GetSection(SqlTargetOptions.SectionName));

    services.AddSingleton<ISqlExecutionService, SqlExecutionService>();
    services.AddSingleton<FrkProcedureService>();
    services.AddSingleton<FrkResultMapper>();
}
