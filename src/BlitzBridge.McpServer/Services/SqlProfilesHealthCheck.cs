using System.Data;

using BlitzBridge.McpServer.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BlitzBridge.McpServer.Services;

internal sealed class SqlProfilesHealthCheck(IOptions<SqlTargetOptions> options) : IHealthCheck
{
    private readonly SqlTargetOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var enabledProfiles = _options.Profiles
            .Where(pair => pair.Value.Enabled)
            .ToArray();

        if (enabledProfiles.Length == 0)
        {
            return HealthCheckResult.Healthy("No enabled SQL target profiles configured.");
        }

        var profileResults = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var unreachableProfiles = new List<string>();

        foreach (var (profileName, profile) in enabledProfiles)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(profile.ConnectionString)
                {
                    ApplicationIntent = ApplicationIntent.ReadOnly,
                    MultipleActiveResultSets = false
                };

                await using var connection = new SqlConnection(builder.ConnectionString);
                await connection.OpenAsync(cancellationToken);
                using var command = new SqlCommand("SELECT 1", connection)
                {
                    CommandType = CommandType.Text,
                    CommandTimeout = Math.Clamp(profile.CommandTimeoutSeconds, 1, 600)
                };
                await command.ExecuteScalarAsync(cancellationToken);

                profileResults[profileName] = "reachable";
            }
            catch (Exception ex)
            {
                unreachableProfiles.Add(profileName);
                profileResults[profileName] = $"unreachable: {ex.GetType().Name}";
            }
        }

        if (unreachableProfiles.Count == 0)
        {
            return HealthCheckResult.Healthy(
                "All enabled SQL target profiles are reachable.",
                data: profileResults);
        }

        return HealthCheckResult.Degraded(
            $"Some SQL target profiles are unreachable: {string.Join(", ", unreachableProfiles)}",
            data: profileResults);
    }
}
