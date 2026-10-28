using StockSense.Helpers;
using StockSense.Models;

namespace StockSense.Services;

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum AlertKind
{
    Expired,
    ExpiringSoon,
    OutOfStock,
    LowStock
}

public class AlertItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public AlertKind Kind { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinStock { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? DaysToExpiry { get; set; }
}

/// <summary>
/// The single definition of "low stock", "expired", "expiring soon" and reorder quantity.
/// Controllers, services and views all use these rules, so numbers never disagree.
/// </summary>
public static class StockRules
{
    /// <summary>Reorder up to this many times the minimum stock level.</summary>
    public const decimal ReorderTargetMultiplier = 2m;

    /// <summary>Items expiring within this many days are flagged Critical instead of Warning.</summary>
    public const int CriticalExpiryDays = 7;

    public static decimal SuggestedReorderQuantity(Product product)
    {
        var target = Math.Max(product.MinStockLevel * ReorderTargetMultiplier, 1m);
        var quantity = Math.Ceiling(target - Math.Max(product.CurrentStock, 0m));
        return Math.Max(quantity, 1m);
    }

    public static bool IsExpired(Product product, DateTime today) =>
        product.ExpiryDate.HasValue && product.ExpiryDate.Value.Date < today.Date;

    public static int? DaysToExpiry(Product product, DateTime today) =>
        product.ExpiryDate.HasValue ? (int)(product.ExpiryDate.Value.Date - today.Date).TotalDays : null;

    /// <summary>All alerts that apply to one product (zero, one or two).</summary>
    public static List<AlertItem> BuildAlerts(Product product, DateTime today, int warningDays)
    {
        var list = new List<AlertItem>();

        AlertItem Create(AlertKind kind, AlertSeverity severity, string title, string message, string action, int? days)
        {
            return new AlertItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                Unit = product.Unit,
                Kind = kind,
                Severity = severity,
                Title = title,
                Message = message,
                RecommendedAction = action,
                CurrentStock = product.CurrentStock,
                MinStock = product.MinStockLevel,
                ExpiryDate = product.ExpiryDate,
                DaysToExpiry = days
            };
        }

        if (product.CurrentStock > 0 && product.ExpiryDate.HasValue)
        {
            var days = (int)(product.ExpiryDate.Value.Date - today.Date).TotalDays;
            var stockText = $"{Fmt.Qty(product.CurrentStock)} {product.Unit} in stock.";

            if (days < 0)
            {
                list.Add(Create(AlertKind.Expired, AlertSeverity.Critical, "Expired",
                    $"Expired {Fmt.DaysText(days)} ({Fmt.Date(product.ExpiryDate.Value)}). {stockText}",
                    "Remove from the shelf now and return to the supplier or write it off. Billing is blocked for this product.",
                    days));
            }
            else if (days <= warningDays)
            {
                var severity = days <= CriticalExpiryDays ? AlertSeverity.Critical : AlertSeverity.Warning;
                var action = days <= CriticalExpiryDays
                    ? "Sell first: move to the front, offer a discount, or return to the supplier before it expires."
                    : "Move to the front of the shelf and plan a discount if it is not selling.";
                list.Add(Create(AlertKind.ExpiringSoon, severity, "Expiring soon",
                    $"Expires {Fmt.DaysText(days)} ({Fmt.Date(product.ExpiryDate.Value)}). {stockText}",
                    action, days));
            }
        }

        if (product.CurrentStock <= 0)
        {
            list.Add(Create(AlertKind.OutOfStock, AlertSeverity.Critical, "Out of stock",
                $"No stock left (minimum level is {Fmt.Qty(product.MinStockLevel)} {product.Unit}).",
                $"Reorder about {Fmt.Qty(SuggestedReorderQuantity(product))} {product.Unit} from your supplier.",
                null));
        }
        else if (product.CurrentStock <= product.MinStockLevel)
        {
            list.Add(Create(AlertKind.LowStock, AlertSeverity.Warning, "Low stock",
                $"Only {Fmt.Qty(product.CurrentStock)} {product.Unit} left (minimum level is {Fmt.Qty(product.MinStockLevel)}).",
                $"Reorder about {Fmt.Qty(SuggestedReorderQuantity(product))} {product.Unit} from your supplier.",
                null));
        }

        return list;
    }
}

/// <summary>Reusable query filters so every screen counts "low stock" / "expired" the same way.</summary>
public static class ProductQueries
{
    public static IQueryable<Product> LowStock(this IQueryable<Product> query) =>
        query.Where(p => p.CurrentStock <= p.MinStockLevel);

    public static IQueryable<Product> OutOfStock(this IQueryable<Product> query) =>
        query.Where(p => p.CurrentStock <= 0);

    public static IQueryable<Product> Expired(this IQueryable<Product> query, DateTime today) =>
        query.Where(p => p.CurrentStock > 0 && p.ExpiryDate != null && p.ExpiryDate < today);

    public static IQueryable<Product> ExpiringSoon(this IQueryable<Product> query, DateTime today, int warningDays)
    {
        var limit = today.AddDays(warningDays);
        return query.Where(p => p.CurrentStock > 0 && p.ExpiryDate != null && p.ExpiryDate >= today && p.ExpiryDate <= limit);
    }
}
