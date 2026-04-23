using System.Diagnostics;
using System.Text.Json;

namespace BlitzBridge.McpServer.Tests;

public class StdioTransportSmokeTests
{
    [Test]
    public async Task StdioTransport_CanParseInitializeRequest_FromStdin()
    {
        var serverPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "BlitzBridge.McpServer", "bin", "Debug", "net10.0", "BlitzBridge.McpServer.exe"));
        var configPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "profiles.json"));

        await Assert.That(File.Exists(serverPath)).IsTrue();
        await Assert.That(File.Exists(configPath)).IsTrue();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = serverPath,
                Arguments = $"--transport stdio --config \"{configPath}\"",
                WorkingDirectory = Path.GetDirectoryName(serverPath)!,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var responseCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrLines = new List<string>();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            var line = eventArgs.Data;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (line.StartsWith("{", StringComparison.Ordinal) &&
                line.Contains("\"jsonrpc\":\"2.0\"", StringComparison.Ordinal) &&
                line.Contains("\"id\":1", StringComparison.Ordinal))
            {
                responseCompletion.TrySetResult(line);
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                stderrLines.Add(eventArgs.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        const string initializeRequestJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"mcmanus-smoke\",\"version\":\"1.0.0\"}}}";

        await process.StandardInput.WriteLineAsync(initializeRequestJson);
        await process.StandardInput.FlushAsync();

        var completion = await Task.WhenAny(responseCompletion.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        var initializeResponse = completion == responseCompletion.Task
            ? await responseCompletion.Task
            : string.Empty;

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        var stderr = string.Join(Environment.NewLine, stderrLines);
        await Assert.That(stderr).DoesNotContain("Startup error:");
        await Assert.That(initializeResponse).IsNotEmpty();

        using var document = JsonDocument.Parse(initializeResponse);
        var root = document.RootElement;

        await Assert.That(root.TryGetProperty("id", out var idElement)).IsTrue();
        await Assert.That(idElement.GetInt32()).IsEqualTo(1);
        await Assert.That(root.TryGetProperty("result", out var resultElement)).IsTrue();
        await Assert.That(resultElement.TryGetProperty("protocolVersion", out var protocolVersion)).IsTrue();
        await Assert.That(protocolVersion.GetString()).IsEqualTo("2024-11-05");
    }

    [Test]
    public async Task StdioTransport_BypassesHttpAuthMiddleware_AndStillInitializes()
    {
        var serverPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "BlitzBridge.McpServer", "bin", "Debug", "net10.0", "BlitzBridge.McpServer.exe"));
        var configPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "profiles.json"));

        await Assert.That(File.Exists(serverPath)).IsTrue();
        await Assert.That(File.Exists(configPath)).IsTrue();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = serverPath,
                Arguments = $"--transport stdio --config \"{configPath}\"",
                WorkingDirectory = Path.GetDirectoryName(serverPath)!,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.Environment["BlitzBridge__Auth__Mode"] = "BearerToken";
        process.StartInfo.Environment["BLITZBRIDGE_AUTH_TOKENS"] = "stdio-ignored";

        var responseCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, eventArgs) =>
        {
            var line = eventArgs.Data;
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (line.StartsWith("{", StringComparison.Ordinal) &&
                line.Contains("\"jsonrpc\":\"2.0\"", StringComparison.Ordinal) &&
                line.Contains("\"id\":1", StringComparison.Ordinal))
            {
                responseCompletion.TrySetResult(line);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        const string initializeRequestJson = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"mcmanus-stdio-auth-bypass\",\"version\":\"1.0.0\"}}}";
        await process.StandardInput.WriteLineAsync(initializeRequestJson);
        await process.StandardInput.FlushAsync();

        var completion = await Task.WhenAny(responseCompletion.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        var initializeResponse = completion == responseCompletion.Task
            ? await responseCompletion.Task
            : string.Empty;

        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        await Assert.That(initializeResponse).IsNotEmpty();

        using var document = JsonDocument.Parse(initializeResponse);
        var root = document.RootElement;
        await Assert.That(root.GetProperty("id").GetInt32()).IsEqualTo(1);
        await Assert.That(root.GetProperty("result").GetProperty("protocolVersion").GetString()).IsEqualTo("2024-11-05");
    }
}
