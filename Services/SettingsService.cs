using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Models;

namespace StockSense.Services;

public interface ISettingsService
{
    Task EnsureDefaultsAsync();
    Task<int> GetExpiryWarningDaysAsync();
    Task SetExpiryWarningDaysAsync(int days);
}

public class SettingsService : ISettingsService
{
    public const string ExpiryWarningKey = "ExpiryWarningDays";
    public const int DefaultExpiryWarningDays = 30;
    public const int MinExpiryWarningDays = 1;
    public const int MaxExpiryWarningDays = 365;

    private readonly StockSenseDbContext _db;

    public SettingsService(StockSenseDbContext db)
    {
        _db = db;
    }

    public async Task EnsureDefaultsAsync()
    {
        var exists = await _db.AppSettings.AnyAsync(s => s.Key == ExpiryWarningKey);
        if (!exists)
        {
            _db.AppSettings.Add(new AppSetting
            {
                Key = ExpiryWarningKey,
                Value = DefaultExpiryWarningDays.ToString()
            });
            await _db.SaveChangesAsync();
        }
    }

    public async Task<int> GetExpiryWarningDaysAsync()
    {
        var setting = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == ExpiryWarningKey);
        if (setting != null && int.TryParse(setting.Value, out var days) && days >= MinExpiryWarningDays)
        {
            return days;
        }

        return DefaultExpiryWarningDays;
    }

    public async Task SetExpiryWarningDaysAsync(int days)
    {
        if (days < MinExpiryWarningDays || days > MaxExpiryWarningDays)
        {
            throw new ArgumentOutOfRangeException(nameof(days),
                $"Expiry warning period must be between {MinExpiryWarningDays} and {MaxExpiryWarningDays} days.");
        }

        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == ExpiryWarningKey);
        if (setting == null)
        {
            _db.AppSettings.Add(new AppSetting { Key = ExpiryWarningKey, Value = days.ToString() });
        }
        else
        {
            setting.Value = days.ToString();
        }

        await _db.SaveChangesAsync();
    }
}
