using Cbs.Mcp.Server.Analysis;
using Cbs.Mcp.Server.Infrastructure;
using Cbs.Mcp.Server.Services;
using Cbs.Mcp.Server.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<CbsPathInspector>();
builder.Services.AddSingleton<CbsLogParser>();
builder.Services.AddSingleton<CbsDiagnosisEngine>();
builder.Services.AddSingleton<CbsFailureTracer>();
builder.Services.AddSingleton<AnalysisRepository>();
builder.Services.AddSingleton<CbsAnalysisArtifactWriter>();
builder.Services.AddSingleton<ILocalCbsToolService, LocalCbsToolService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
