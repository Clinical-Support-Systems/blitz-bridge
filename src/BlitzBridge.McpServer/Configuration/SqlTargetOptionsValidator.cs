using Microsoft.Data.SqlClient;

namespace BlitzBridge.McpServer.Configuration;

internal static class SqlTargetOptionsValidator
{
    private static readonly HashSet<string> KnownProcedures = new(StringComparer.OrdinalIgnoreCase)
    {
        "sp_Blitz",
        "sp_BlitzAnalysis",
        "sp_BlitzCache",
        "sp_BlitzFirst",
        "sp_BlitzIndex",
        "sp_BlitzLock",
        "sp_BlitzWho"
    };

    public static List<string> Validate(SqlTargetOptions options)
    {
        var errors = new List<string>();

        foreach (var (profileName, profile) in options.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                errors.Add("SqlTargets contains an empty profile key.");
                continue;
            }

            if (!profile.Enabled)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(profile.ConnectionString))
            {
                errors.Add($"SqlTargets profile '{profileName}' is enabled but missing ConnectionString.");
                continue;
            }

            SqlConnectionStringBuilder connectionStringBuilder;
            try
            {
                connectionStringBuilder = new SqlConnectionStringBuilder(profile.ConnectionString);
            }
            catch (Exception)
            {
                errors.Add($"SqlTargets profile '{profileName}' has an invalid ConnectionString format.");
                continue;
            }

            if (connectionStringBuilder.ApplicationIntent != ApplicationIntent.ReadOnly)
            {
                errors.Add($"SqlTargets profile '{profileName}' must set ApplicationIntent=ReadOnly.");
            }

            if (profile.CommandTimeoutSeconds is < 1 or > 600)
            {
                errors.Add($"SqlTargets profile '{profileName}' CommandTimeoutSeconds must be between 1 and 600.");
            }

            if (profile.AiMode is < 0 or > 2)
            {
                errors.Add($"SqlTargets profile '{profileName}' AiMode must be 0, 1, or 2.");
            }

            if (profile.AllowedDatabases.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add($"SqlTargets profile '{profileName}' AllowedDatabases cannot contain blank values.");
            }

            if (profile.AllowedProcedures.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add($"SqlTargets profile '{profileName}' AllowedProcedures cannot contain blank values.");
            }

            foreach (var procedure in profile.AllowedProcedures)
            {
                if (!KnownProcedures.Contains(procedure))
                {
                    errors.Add($"SqlTargets profile '{profileName}' contains unsupported procedure '{procedure}'.");
                }
            }
        }

        return errors;
    }
}
