using Microsoft.EntityFrameworkCore;

namespace StockSense.Data;

/// <summary>Reads the MySQL / MariaDB server version from configuration (used by the app and by "dotnet ef").</summary>
public static class DbConfig
{
    public static ServerVersion GetServerVersion(IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "MySql";
        var versionText = configuration["Database:ServerVersion"] ?? "8.0.36";

        if (!Version.TryParse(versionText, out var version))
        {
            version = new Version(8, 0, 36);
        }

        return provider.Equals("MariaDb", StringComparison.OrdinalIgnoreCase)
            ? new MariaDbServerVersion(version)
            : new MySqlServerVersion(version);
    }
}
