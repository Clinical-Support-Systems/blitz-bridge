using System.Diagnostics;
using System.Text.Json;

namespace BlitzBridge.McpServer.Tests;

public class ExamplesSmokeTests
{
    [Test]
    public async Task ClientConfigExamples_AreValidJson()
    {
        var repoRoot = GetRepoRoot();
        var configDirectory = Path.Combine(repoRoot, "examples", "client-configs");
        var jsonFiles = Directory.GetFiles(configDirectory, "*.json", SearchOption.TopDirectoryOnly);

        await Assert.That(jsonFiles.Length).IsGreaterThanOrEqualTo(4);

        foreach (var jsonFile in jsonFiles)
        {
            var jsonContent = await File.ReadAllTextAsync(jsonFile);
            using var _ = JsonDocument.Parse(jsonContent);
        }
    }

    [Test]
    public async Task PythonMcpExample_HasExpectedImports_AndCompiles()
    {
        var repoRoot = GetRepoRoot();
        var pythonFile = Path.Combine(repoRoot, "examples", "client-configs", "python-mcp-client.py");
        await Assert.That(File.Exists(pythonFile)).IsTrue();

        var fileContent = await File.ReadAllTextAsync(pythonFile);
        await Assert.That(fileContent).Contains("from mcp.client.session import ClientSession");
        await Assert.That(fileContent).Contains("from mcp.client.streamable_http import streamablehttp_client");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"-m py_compile \"{pythonFile}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        await Assert.That(process.ExitCode).IsEqualTo(0);
        await Assert.That(stderr).IsEqualTo(string.Empty);
    }

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));
    }
}
