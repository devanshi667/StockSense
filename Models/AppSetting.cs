namespace StockSense.Models;

/// <summary>Simple key/value store for shop settings (e.g. the expiry warning period).</summary>
public class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
