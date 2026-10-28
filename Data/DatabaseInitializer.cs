using Microsoft.EntityFrameworkCore;
using StockSense.Services;

namespace StockSense.Data;

/// <summary>Holds a startup database problem (if any) so the app can show a helpful page instead of crashing.</summary>
public static class DatabaseStatus
{
    public static string? Error { get; set; }
}

public static class DatabaseInitializer
{
    /// <summary>
    /// Creates / updates the schema and default settings.
    /// * If EF Core migrations exist in the project they are applied (recommended path).
    /// * If none exist, the schema is created directly from the model so the app still runs out of the box.
    /// </summary>
    public static async Task InitializeAsync(WebApplication app)
    {
        var connectionString = app.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(connectionString) ||
            connectionString.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("PUT_YOUR_MYSQL_PASSWORD_HERE", StringComparison.OrdinalIgnoreCase))
        {
            DatabaseStatus.Error =
                "The MySQL password is not configured yet. Copy appsettings.Example.json to appsettings.Local.json, " +
                "put your real MySQL password in it, then restart the app (see README.md, step 3).";
            return;
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<StockSenseDbContext>();

            if (db.Database.GetMigrations().Any())
            {
                await db.Database.MigrateAsync();
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }

            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            await settings.EnsureDefaultsAsync();

            if (app.Configuration.GetValue<bool>("Demo:AutoLoadOnEmptyDatabase") && !await db.Products.AnyAsync())
            {
                var demo = scope.ServiceProvider.GetRequiredService<IDemoDataService>();
                var result = await demo.LoadAsync();
                if (!result.Succeeded)
                {
                    logger.LogWarning("Demo data was not loaded: {Errors}", string.Join("; ", result.Errors));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialisation failed.");
            DatabaseStatus.Error = "Could not connect to / prepare the MySQL database: " + ex.GetBaseException().Message;
        }
    }
}
