namespace BlitzBridge.McpServer.Configuration;

internal enum TransportMode
{
    Http,
    Stdio
}

internal sealed record StartupConfiguration(
    TransportMode TransportMode,
    string? ConfigPath,
    bool ShouldFailFast,
    string FailureMessage,
    int ExitCode)
{
    private const string ConfigFlag = "--config";
    private const string TransportFlag = "--transport";

    public static StartupConfiguration Parse(string[] args)
    {
        return Parse(args, GetDefaultConfigPath, File.Exists);
    }

    internal static StartupConfiguration Parse(
        string[] args,
        Func<string> defaultConfigPathProvider,
        Func<string, bool> fileExists)
    {
        var transport = GetFlagValue(args, TransportFlag, out _, out _);
        var transportMode = transport?.ToLowerInvariant() switch
        {
            null or "http" => TransportMode.Http,
            "stdio" => TransportMode.Stdio,
            _ => throw new ArgumentException("Invalid --transport value. Supported values are 'http' and 'stdio'.")
        };

        var explicitConfigPath = GetFlagValue(args, ConfigFlag, out var configFlagPresent, out var configFlagHasValue);
        if (configFlagPresent && !configFlagHasValue)
        {
            return new StartupConfiguration(
                transportMode,
                null,
                true,
                "The --config option requires a path value.",
                2);
        }

        var configPath = !string.IsNullOrWhiteSpace(explicitConfigPath)
            ? Path.GetFullPath(explicitConfigPath)
            : null;

        if (transportMode == TransportMode.Stdio)
        {
            configPath ??= defaultConfigPathProvider();
            if (!fileExists(configPath))
            {
                return new StartupConfiguration(
                    transportMode,
                    configPath,
                    true,
                    $"Startup error: SQL target configuration file was not found at '{configPath}'. Provide --config <path> or create the default profiles.json.",
                    2);
            }
        }

        return new StartupConfiguration(transportMode, configPath, false, string.Empty, 0);
    }

    private static string? GetFlagValue(
        string[] args,
        string flag,
        out bool flagPresent,
        out bool hasValue)
    {
        flagPresent = false;
        hasValue = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
            {
                flagPresent = true;

                if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
                {
                    return null;
                }

                hasValue = true;
                return args[index + 1];
            }

            var prefix = $"{flag}=";
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                flagPresent = true;
                var inlineValue = argument[prefix.Length..];
                hasValue = !string.IsNullOrWhiteSpace(inlineValue);
                return hasValue ? inlineValue : null;
            }
        }

        return null;
    }

    private static string GetDefaultConfigPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "blitz-bridge", "profiles.json");
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userHome, ".config", "blitz-bridge", "profiles.json");
    }
}
