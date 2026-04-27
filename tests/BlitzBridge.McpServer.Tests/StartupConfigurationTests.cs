using BlitzBridge.McpServer.Configuration;

namespace BlitzBridge.McpServer.Tests;

public class StartupConfigurationTests
{
    [Test]
    public async Task Parse_Fails_WhenConfigFlagIsPresentWithoutValue()
    {
        var result = StartupConfiguration.Parse(
            ["--config"],
            () => @"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json",
            _ => false);

        await Assert.That(result.ShouldFailFast).IsTrue();
        await Assert.That(result.ExitCode).IsEqualTo(2);
        await Assert.That(result.FailureMessage).Contains("--config option requires a path value");
    }

    [Test]
    public async Task Parse_Fails_ForStdio_WhenDefaultConfigIsMissing()
    {
        var result = StartupConfiguration.Parse(
            ["--transport", "stdio"],
            () => @"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json",
            _ => false);

        await Assert.That(result.ShouldFailFast).IsTrue();
        await Assert.That(result.ExitCode).IsEqualTo(2);
        await Assert.That(result.FailureMessage).Contains("Startup error: SQL target configuration file was not found");
        await Assert.That(result.ConfigPath).IsEqualTo(@"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json");
    }

    [Test]
    public async Task Parse_DoesNotFail_ForHttp_WhenDefaultConfigIsMissing()
    {
        var result = StartupConfiguration.Parse(
            ["--transport", "http"],
            () => @"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json",
            _ => false);

        await Assert.That(result.ShouldFailFast).IsFalse();
        await Assert.That(result.ConfigPath).IsNull();
        await Assert.That(result.TransportMode).IsEqualTo(TransportMode.Http);
    }

    [Test]
    public async Task Parse_FailsFast_WhenNoArgumentsProvided()
    {
        var result = StartupConfiguration.Parse(
            [],
            () => @"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json",
            _ => false);

        await Assert.That(result.ShouldFailFast).IsTrue();
        await Assert.That(result.ExitCode).IsEqualTo(2);
        await Assert.That(result.FailureMessage).Contains("No arguments provided");
        await Assert.That(result.FailureMessage).Contains("--init-config");
    }

    [Test]
    public async Task Parse_UsesInlineConfigFlag_WhenProvided()
    {
        var result = StartupConfiguration.Parse(
            ["--transport=stdio", "--config=C:\\configs\\profiles.json"],
            () => @"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json",
            path => string.Equals(path, @"C:\configs\profiles.json", StringComparison.OrdinalIgnoreCase));

        await Assert.That(result.ShouldFailFast).IsFalse();
        await Assert.That(result.ConfigPath).IsEqualTo(@"C:\configs\profiles.json");
        await Assert.That(result.TransportMode).IsEqualTo(TransportMode.Stdio);
    }

    [Test]
    public async Task Parse_ThrowsForUnsupportedTransport()
    {
        await Assert.That(() => StartupConfiguration.Parse(
                ["--transport", "pipes"],
                () => @"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json",
                _ => false))
            .Throws<ArgumentException>()
            .WithMessage("Invalid --transport value. Supported values are 'http' and 'stdio'.");
    }

    [Test]
    public async Task Parse_InitConfig_UsesDefaultPath_WhenConfigNotProvided()
    {
        var result = StartupConfiguration.Parse(
            ["--init-config"],
            () => @"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json",
            _ => false);

        await Assert.That(result.ShouldFailFast).IsFalse();
        await Assert.That(result.ShouldInitializeConfig).IsTrue();
        await Assert.That(result.ConfigPath).IsEqualTo(@"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json");
    }

    [Test]
    public async Task Parse_InitConfig_UsesProvidedConfigPath()
    {
        var result = StartupConfiguration.Parse(
            ["--init-config", "--config", @"C:\configs\beta-profiles.json"],
            () => @"C:\Users\test\AppData\Roaming\blitz-bridge\profiles.json",
            _ => false);

        await Assert.That(result.ShouldFailFast).IsFalse();
        await Assert.That(result.ShouldInitializeConfig).IsTrue();
        await Assert.That(result.ConfigPath).IsEqualTo(@"C:\configs\beta-profiles.json");
    }
}
