using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;

namespace StockSense.Services;

public class StockCounts
{
    public int TotalProducts { get; set; }

    /// <summary>Products at or below their minimum level (includes out-of-stock products).</summary>
    public int LowStock { get; set; }
    public int OutOfStock { get; set; }

    /// <summary>In-stock products that expire within the configured warning period.</summary>
    public int ExpiringSoon { get; set; }

    /// <summary>In-stock products that are already past their expiry date.</summary>
    public int Expired { get; set; }

    public int AttentionCount => LowStock + ExpiringSoon + Expired;
}

public class ReorderSuggestion
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinStock { get; set; }
    public decimal SuggestedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal EstimatedCost => Money.R2(SuggestedQuantity * UnitCost);
    public string? LastSupplierName { get; set; }
}

public interface IAlertService
{
    Task<StockCounts> GetCountsAsync();
    Task<List<AlertItem>> GetAlertsAsync();
    Task<List<ReorderSuggestion>> GetReorderSuggestionsAsync();
}

public class AlertService : IAlertService
{
    private readonly StockSenseDbContext _db;
    private readonly ISettingsService _settings;

    public AlertService(StockSenseDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<StockCounts> GetCountsAsync()
    {
        var today = AppTime.Today;
        var warningDays = await _settings.GetExpiryWarningDaysAsync();
        var products = _db.Products.AsNoTracking();

        return new StockCounts
        {
            TotalProducts = await products.CountAsync(),
            LowStock = await products.LowStock().CountAsync(),
            OutOfStock = await products.OutOfStock().CountAsync(),
            ExpiringSoon = await products.ExpiringSoon(today, warningDays).CountAsync(),
            Expired = await products.Expired(today).CountAsync()
        };
    }

    public async Task<List<AlertItem>> GetAlertsAsync()
    {
        var today = AppTime.Today;
        var warningDays = await _settings.GetExpiryWarningDaysAsync();
        var limit = today.AddDays(warningDays);

        var candidates = await _db.Products.AsNoTracking()
            .Where(p => p.CurrentStock <= p.MinStockLevel ||
                        (p.CurrentStock > 0 && p.ExpiryDate != null && p.ExpiryDate <= limit))
            .ToListAsync();

        return candidates
            .SelectMany(p => StockRules.BuildAlerts(p, today, warningDays))
            .OrderByDescending(a => a.Severity)
            .ThenBy(a => a.DaysToExpiry ?? int.MaxValue)
            .ThenBy(a => a.ProductName)
            .ToList();
    }

    public async Task<List<ReorderSuggestion>> GetReorderSuggestionsAsync()
    {
        var lowStock = await _db.Products.AsNoTracking()
            .Include(p => p.Category)
            .LowStock()
            .OrderBy(p => p.CurrentStock - p.MinStockLevel)
            .ThenBy(p => p.Name)
            .ToListAsync();

        if (lowStock.Count == 0)
        {
            return new List<ReorderSuggestion>();
        }

        var ids = lowStock.Select(p => p.Id).ToList();

        // Most recent supplier for each product, taken from saved purchase history.
        var history = await _db.PurchaseItems.AsNoTracking()
            .Where(i => ids.Contains(i.ProductId))
            .OrderByDescending(i => i.Purchase!.PurchaseDate)
            .ThenByDescending(i => i.Id)
            .Select(i => new { i.ProductId, SupplierName = i.Purchase!.Supplier!.Name })
            .ToListAsync();

        var lastSupplier = history
            .GroupBy(h => h.ProductId)
            .ToDictionary(g => g.Key, g => g.First().SupplierName);

        return lowStock.Select(p => new ReorderSuggestion
        {
            ProductId = p.Id,
            ProductName = p.Name,
            Sku = p.Sku,
            Unit = p.Unit,
            CategoryName = p.Category?.Name ?? string.Empty,
            CurrentStock = p.CurrentStock,
            MinStock = p.MinStockLevel,
            SuggestedQuantity = StockRules.SuggestedReorderQuantity(p),
            UnitCost = p.PurchaseCost,
            LastSupplierName = lastSupplier.TryGetValue(p.Id, out var supplier) ? supplier : null
        }).ToList();
    }
}
