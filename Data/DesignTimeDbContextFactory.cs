using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockSense.Data;

/// <summary>
/// Used only by "dotnet ef migrations ..." so migrations can be created without a running database.
/// It reads the same appsettings.json / appsettings.Local.json as the app.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StockSenseDbContext>
{
    public StockSenseDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=stocksense;User=root;Password=unused;";

        var options = new DbContextOptionsBuilder<StockSenseDbContext>()
            .UseMySql(connectionString, DbConfig.GetServerVersion(configuration))
            .Options;

        return new StockSenseDbContext(options);
    }
}
