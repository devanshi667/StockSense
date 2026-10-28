using System.Globalization;
using StockSense.Services;

namespace StockSense.Helpers;

/// <summary>Small presentation helpers (CSS class names, icons, data-attribute values) for Razor views.</summary>
public static class Ui
{
    public static string SeverityClass(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "soft-danger",
        AlertSeverity.Warning => "soft-warning",
        _ => "soft-info"
    };

    public static string SeverityLabel(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "Critical",
        AlertSeverity.Warning => "Warning",
        _ => "Notice"
    };

    public static string KindIcon(AlertKind kind) => kind switch
    {
        AlertKind.Expired => "bi-calendar-x",
        AlertKind.ExpiringSoon => "bi-hourglass-split",
        AlertKind.OutOfStock => "bi-box-seam",
        _ => "bi-arrow-down-circle"
    };

    /// <summary>Icon + colour for a row on the "recent transactions" list.</summary>
    public static string TransactionIcon(string type) => type switch
    {
        "Sale" => "bi-receipt",
        "Purchase" => "bi-truck",
        _ => "bi-cash-coin"
    };

    public static string TransactionClass(string type) => type switch
    {
        "Sale" => "soft-success",
        "Purchase" => "soft-info",
        _ => "soft-warning"
    };

    public static string Initial(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();

    /// <summary>Culture independent number text for HTML data-* attributes and JavaScript (always uses a dot).</summary>
    public static string Num(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>yyyy-MM-dd text for value="" of date inputs and for URLs.</summary>
    public static string IsoDate(DateTime? value) => value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : string.Empty;
}
