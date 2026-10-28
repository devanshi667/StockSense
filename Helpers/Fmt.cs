using System.Globalization;
using StockSense.Models;

namespace StockSense.Helpers;

/// <summary>Display formatting helpers used by Razor views and messages.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inr = CreateIndianCulture();
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static CultureInfo CreateIndianCulture()
    {
        try { return new CultureInfo("en-IN"); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }

    public static string Money(decimal value)
    {
        var text = Math.Abs(value).ToString("N2", Inr);
        return value < 0 ? "-₹" + text : "₹" + text;
    }

    public static string Qty(decimal value) => value.ToString("#,##0.###", Inr);

    public static string Date(DateTime value) => value.ToString("dd MMM yyyy", Inv);

    public static string Stamp(DateTime value) => value.ToString("dd MMM yyyy, hh:mm tt", Inv);

    public static string Pct(decimal value) => value.ToString("0.0", Inv) + "%";

    public static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Upi => "UPI",
        PaymentMethod.Card => "Card",
        _ => "Other"
    };

    /// <summary>Human friendly "in 5 days" / "3 days ago" text for an expiry date.</summary>
    public static string DaysText(int days)
    {
        if (days == 0) return "today";
        if (days == 1) return "tomorrow";
        if (days == -1) return "1 day ago";
        return days > 0 ? $"in {days} days" : $"{-days} days ago";
    }
}
