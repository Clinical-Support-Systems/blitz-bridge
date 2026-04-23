using BlitzBridge.McpServer.Configuration;
using BlitzBridge.McpServer.Middleware;
using BlitzBridge.McpServer.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

    var corsOptions = builder.Configuration
        .GetSection(BlitzBridge.McpServer.Configuration.CorsOptions.SectionName)
        .Get<BlitzBridge.McpServer.Configuration.CorsOptions>() ?? new BlitzBridge.McpServer.Configuration.CorsOptions();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            // CORS defaults to "deny by omission": no configured origins means no Access-Control-Allow-Origin header.
            // AllowAnyOrigin is an explicit opt-in intended for local development only.
            if (corsOptions.AllowAnyOrigin)
            {
                policy.AllowAnyOrigin();
            }
            else
            {
                var allowedOrigins = corsOptions.AllowedOrigins
                    .Where(origin => !string.IsNullOrWhiteSpace(origin))
                    .Select(origin => origin.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins);
                }
            }

            policy.AllowAnyHeader()
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
    app.UseMiddleware<McpHttpAuthMiddleware>();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    });
    app.MapHealthChecks("/alive", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live")
    });
    app.MapMcp("/mcp");

    await app.RunAsync();
    return 0;
}

static void ConfigureSharedServices(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<SqlTargetOptions>(
        configuration.GetSection(SqlTargetOptions.SectionName));
    services.Configure<BlitzBridgeAuthOptions>(
        configuration.GetSection(BlitzBridgeAuthOptions.SectionName));
    services.Configure<BlitzBridge.McpServer.Configuration.CorsOptions>(
        configuration.GetSection(BlitzBridge.McpServer.Configuration.CorsOptions.SectionName));

    services.AddSingleton<ISqlExecutionService, SqlExecutionService>();
    services.AddSingleton<FrkProcedureService>();
    services.AddSingleton<FrkResultMapper>();
    services.AddHealthChecks()
        .AddCheck<SqlProfilesHealthCheck>("sql-target-connectivity", failureStatus: HealthStatus.Degraded);
}

public partial class Program;

