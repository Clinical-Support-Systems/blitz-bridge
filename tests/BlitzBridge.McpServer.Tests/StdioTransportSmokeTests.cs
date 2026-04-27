using System.Diagnostics;
using System.Text.Json;

namespace BlitzBridge.McpServer.Tests;

public class StdioTransportSmokeTests
{
    private static string ResolveServerDirectory()
    {
        var testBinDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var inferredConfiguration = testBinDirectory.Parent?.Parent?.Name;
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));

        var candidateConfigurations = new[]
        {
            inferredConfiguration,
            "Debug",
            "Release"
        }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var configuration in candidateConfigurations)
        {
            var candidateDirectory = Path.Combine(
                repoRoot,
                "src", "BlitzBridge.McpServer", "bin", configuration!, "net10.0");
            var candidatePath = OperatingSystem.IsWindows()
                ? Path.Combine(candidateDirectory, "BlitzBridge.McpServer.exe")
                : Path.Combine(candidateDirectory, "BlitzBridge.McpServer.dll");

            if (File.Exists(candidatePath))
            {
                return candidateDirectory;
            }
        }

        throw new InvalidOperationException("Unable to locate BlitzBridge.McpServer build output for stdio smoke tests.");
    }

    private static ProcessStartInfo CreateStdioProcessStartInfo(string serverDirectory, string configPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = Path.Combine(serverDirectory, "BlitzBridge.McpServer.exe"),
                Arguments = $"--transport stdio --config \"{configPath}\"",
                WorkingDirectory = serverDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        return new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{Path.Combine(serverDirectory, "BlitzBridge.McpServer.dll")}\" --transport stdio --config \"{configPath}\"",
            WorkingDirectory = serverDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    [Test]
    public async Task StdioTransport_CanParseInitializeRequest_FromStdin()
    {
        var serverDirectory = ResolveServerDirectory();
        var serverPath = OperatingSystem.IsWindows()
            ? Path.Combine(serverDirectory, "BlitzBridge.McpServer.exe")
            : Path.Combine(serverDirectory, "BlitzBridge.McpServer.dll");
        var configPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "profiles.json"));

        await Assert.That(File.Exists(serverPath)).IsTrue();
        await Assert.That(File.Exists(configPath)).IsTrue();

        using var process = new Process
        {
            StartInfo = CreateStdioProcessStartInfo(serverDirectory, configPath)
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
        var serverDirectory = ResolveServerDirectory();
        var serverPath = OperatingSystem.IsWindows()
            ? Path.Combine(serverDirectory, "BlitzBridge.McpServer.exe")
            : Path.Combine(serverDirectory, "BlitzBridge.McpServer.dll");
        var configPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "profiles.json"));

        await Assert.That(File.Exists(serverPath)).IsTrue();
        await Assert.That(File.Exists(configPath)).IsTrue();

        using var process = new Process
        {
            StartInfo = CreateStdioProcessStartInfo(serverDirectory, configPath)
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
