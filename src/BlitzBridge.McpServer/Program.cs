using BlitzBridge.McpServer.Configuration;
using BlitzBridge.McpServer.Services;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<SqlTargetOptions>(
    builder.Configuration.GetSection(SqlTargetOptions.SectionName));

builder.Services.AddSingleton<ISqlExecutionService, SqlExecutionService>();
builder.Services.AddSingleton<FrkProcedureService>();
builder.Services.AddSingleton<FrkResultMapper>();

builder.Services.AddMcpServer()
    .WithHttpTransport(s => {
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

app.UseCors();

app.MapDefaultEndpoints();
app.MapGet("/health", () => Results.Ok("healthy"));
app.MapMcp("/mcp");

app.Run();
