using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

if (args.Length < 2)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools\\Cbs.Mcp.SmokeClient -- <server-dll> <CBS-directory>");
    return 2;
}

var serverDll = Path.GetFullPath(args[0]);
var cbsDirectory = Path.GetFullPath(args[1]);
if (!File.Exists(serverDll))
{
    Console.Error.WriteLine($"Server DLL not found: {serverDll}");
    return 3;
}

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "cbs-lab-smoke",
    Command = "dotnet",
    Arguments = [serverDll]
});

await using var client = await McpClient.CreateAsync(transport);
var tools = await client.ListToolsAsync();
var expected = new[]
{
    "inspect_cbs",
    "diagnose_cbs",
    "show_cbs_evidence",
    "trace_cbs_failure"
};

foreach (var name in expected)
{
    if (!tools.Any(tool => tool.Name == name))
    {
        Console.Error.WriteLine($"Missing MCP tool: {name}");
        return 4;
    }
}

Console.WriteLine($"Discovered tools: {string.Join(", ", tools.Select(tool => tool.Name))}");

var inspection = await client.CallToolAsync(
    "inspect_cbs",
    new Dictionary<string, object?> { ["path"] = cbsDirectory },
    cancellationToken: CancellationToken.None);
var inspectionJson = inspection.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
Console.WriteLine($"inspect_cbs => {inspectionJson}");

if (string.IsNullOrWhiteSpace(inspectionJson))
{
    Console.Error.WriteLine("inspect_cbs returned no text content.");
    return 5;
}

using var inspectionDocument = JsonDocument.Parse(inspectionJson);
var analysisId = inspectionDocument.RootElement
    .GetProperty("data")
    .GetProperty("analysisId")
    .GetString();
if (string.IsNullOrWhiteSpace(analysisId))
{
    Console.Error.WriteLine("inspect_cbs returned no analysisId.");
    return 6;
}

var diagnosis = await client.CallToolAsync(
    "diagnose_cbs",
    new Dictionary<string, object?> { ["analysisId"] = analysisId },
    cancellationToken: CancellationToken.None);
var diagnosisJson = diagnosis.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
Console.WriteLine($"diagnose_cbs => {diagnosisJson}");

if (string.IsNullOrWhiteSpace(diagnosisJson))
{
    Console.Error.WriteLine("diagnose_cbs returned no text content.");
    return 7;
}

using var diagnosisDocument = JsonDocument.Parse(diagnosisJson);
var findings = diagnosisDocument.RootElement
    .GetProperty("data")
    .GetProperty("findings");
if (findings.GetArrayLength() == 0)
{
    Console.Error.WriteLine("diagnose_cbs returned no finding for the sample data.");
    return 8;
}

var findingId = findings[0].GetProperty("findingId").GetString();
var evidence = await client.CallToolAsync(
    "show_cbs_evidence",
    new Dictionary<string, object?>
    {
        ["analysisId"] = analysisId,
        ["findingId"] = findingId
    },
    cancellationToken: CancellationToken.None);
Console.WriteLine(
    $"show_cbs_evidence => {evidence.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text}");

var trace = await client.CallToolAsync(
    "trace_cbs_failure",
    new Dictionary<string, object?>
    {
        ["analysisId"] = analysisId,
        ["findingId"] = findingId
    },
    cancellationToken: CancellationToken.None);
Console.WriteLine(
    $"trace_cbs_failure => {trace.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text}");

return 0;
