using Microsoft.AspNetCore.Mvc.Rendering;
using StockSense.Models;

namespace StockSense.Helpers;

public static class Lookups
{
    /// <summary>Payment methods as select items. Values are enum names so model binding and asp-for agree.</summary>
    public static List<SelectListItem> PaymentMethods()
    {
        return Enum.GetValues<PaymentMethod>()
            .Select(m => new SelectListItem(Fmt.Method(m), m.ToString()))
            .ToList();
    }

    public static readonly string[] CommonUnits =
    {
        "pcs", "kg", "g", "litre", "ml", "pack", "packet", "pouch", "bottle", "box", "dozen", "bar", "can"
    };

    /// <summary>Normalises a SKU: trimmed and upper-case so "abc-1" and "ABC-1" are the same code.</summary>
    public static string NormalizeSku(string? sku) => (sku ?? string.Empty).Trim().ToUpperInvariant();

    public static string? CleanText(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }
}
