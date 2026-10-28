using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.ViewModels;

namespace StockSense.Services;

public enum ReportPeriod
{
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// Accounting definitions used on every report:
///   Revenue       = value of bills raised in the period (cash AND credit sales), after discounts.
///   COGS          = purchase cost saved on each sold line at the time of sale (not today's cost).
///   Gross profit  = Revenue - COGS. It is NOT net profit: shop expenses (rent, salaries, electricity...) are not tracked.
///   Cash received = money paid at the time of sale + udhaar payments collected in the period.
/// Credit given is shown separately because it is revenue that has not been collected yet.
/// </summary>
public class SalesSummary
{
    public decimal Revenue { get; set; }
    public decimal Cogs { get; set; }
    public decimal GrossProfit => Revenue - Cogs;
    public decimal MarginPercent => Revenue <= 0 ? 0 : GrossProfit / Revenue * 100m;

    public int SalesCount { get; set; }
    public decimal DiscountGiven { get; set; }

    public decimal PaidAtSale { get; set; }
    public decimal CreditGiven => Revenue - PaidAtSale;
    public decimal UdhaarCollected { get; set; }
    public decimal TotalCashReceived => PaidAtSale + UdhaarCollected;
}

public class SeriesPoint
{
    public string Label { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public decimal Sales { get; set; }
    public decimal Cogs { get; set; }
    public decimal GrossProfit => Sales - Cogs;
}

public class ProductProfitRow
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit => Revenue - Cost;
    public decimal MarginPercent => Revenue <= 0 ? 0 : GrossProfit / Revenue * 100m;
}

public interface IReportService
{
    Task<SalesSummary> GetSummaryAsync(DateTime from, DateTime toInclusive);
    Task<List<SeriesPoint>> GetSeriesAsync(ReportPeriod period, DateTime from, DateTime toInclusive);
    Task<ReportViewModel> GetReportAsync(ReportPeriod period, DateTime? from, DateTime? to);
}

public class ReportService : IReportService
{
    private const int MaxRangeDays = 366;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly StockSenseDbContext _db;

    public ReportService(StockSenseDbContext db)
    {
        _db = db;
    }

    private sealed class ItemRow
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
    }

    public async Task<SalesSummary> GetSummaryAsync(DateTime from, DateTime toInclusive)
    {
        var start = from.Date;
        var end = toInclusive.Date.AddDays(1);

        var sales = _db.Sales.AsNoTracking().Where(s => s.SaleDate >= start && s.SaleDate < end);

        var summary = new SalesSummary
        {
            Revenue = await sales.SumAsync(s => (decimal?)s.TotalAmount) ?? 0m,
            DiscountGiven = await sales.SumAsync(s => (decimal?)s.DiscountAmount) ?? 0m,
            PaidAtSale = await sales.SumAsync(s => (decimal?)s.AmountPaid) ?? 0m,
            SalesCount = await sales.CountAsync(),
            Cogs = await _db.SaleItems.AsNoTracking()
                .Where(i => i.Sale!.SaleDate >= start && i.Sale!.SaleDate < end)
                .SumAsync(i => (decimal?)i.LineCost) ?? 0m,
            UdhaarCollected = await _db.Payments.AsNoTracking()
                .Where(p => p.PaymentDate >= start && p.PaymentDate < end)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m
        };

        return summary;
    }

    public async Task<List<SeriesPoint>> GetSeriesAsync(ReportPeriod period, DateTime from, DateTime toInclusive)
    {
        var rows = await LoadRowsAsync(from, toInclusive);
        return BuildSeries(rows, period, from.Date, toInclusive.Date);
    }

    public async Task<ReportViewModel> GetReportAsync(ReportPeriod period, DateTime? from, DateTime? to)
    {
        var today = AppTime.Today;
        var (defaultFrom, defaultTo) = DefaultRange(period, today);

        var start = (from ?? defaultFrom).Date;
        var end = (to ?? defaultTo).Date;

        if (end < start)
        {
            (start, end) = (end, start);
        }

        string? notice = null;
        if ((end - start).TotalDays > MaxRangeDays)
        {
            start = end.AddDays(-MaxRangeDays);
            notice = $"The date range was limited to {MaxRangeDays} days ({Fmt.Date(start)} to {Fmt.Date(end)}).";
        }

        var rows = await LoadRowsAsync(start, end);

        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var productInfo = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Sku, p.Unit })
            .ToDictionaryAsync(p => p.Id);

        var productRows = rows
            .GroupBy(r => r.ProductId)
            .Select(g =>
            {
                productInfo.TryGetValue(g.Key, out var info);
                return new ProductProfitRow
                {
                    ProductId = g.Key,
                    Name = info?.Name ?? "(deleted product)",
                    Sku = info?.Sku ?? string.Empty,
                    Unit = info?.Unit ?? string.Empty,
                    Quantity = g.Sum(r => r.Quantity),
                    Revenue = g.Sum(r => r.Revenue),
                    Cost = g.Sum(r => r.Cost)
                };
            })
            .OrderByDescending(p => p.GrossProfit)
            .ThenBy(p => p.Name)
            .ToList();

        var rangeStart = start;
        var rangeEnd = end.AddDays(1);

        var salesInRange = _db.Sales.AsNoTracking().Where(s => s.SaleDate >= rangeStart && s.SaleDate < rangeEnd);
        var totalSales = await salesInRange.CountAsync();

        var recentSales = await salesInRange
            .OrderByDescending(s => s.SaleDate)
            .ThenByDescending(s => s.Id)
            .Take(100)
            .Select(s => new SaleRow
            {
                Id = s.Id,
                InvoiceNumber = s.InvoiceNumber,
                SaleDate = s.SaleDate,
                CustomerName = s.Customer != null ? s.Customer.Name : null,
                ItemCount = s.Items.Count,
                TotalAmount = s.TotalAmount,
                AmountPaid = s.AmountPaid,
                PaymentMethod = s.PaymentMethod,
                IsDemo = s.IsDemo
            })
            .ToListAsync();

        return new ReportViewModel
        {
            Period = period,
            From = start,
            To = end,
            RangeNotice = notice,
            Summary = await GetSummaryAsync(start, end),
            Series = BuildSeries(rows, period, start, end),
            Products = productRows,
            BestSellers = productRows.OrderByDescending(p => p.Quantity).ThenBy(p => p.Name).Take(10).ToList(),
            Sales = recentSales,
            TotalSalesInRange = totalSales,
            Presets = BuildPresets(today)
        };
    }

    private async Task<List<ItemRow>> LoadRowsAsync(DateTime from, DateTime toInclusive)
    {
        var start = from.Date;
        var end = toInclusive.Date.AddDays(1);

        return await _db.SaleItems.AsNoTracking()
            .Where(i => i.Sale!.SaleDate >= start && i.Sale!.SaleDate < end)
            .Select(i => new ItemRow
            {
                SaleId = i.SaleId,
                SaleDate = i.Sale!.SaleDate,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Revenue = i.LineTotal - i.LineDiscount,
                Cost = i.LineCost
            })
            .ToListAsync();
    }

    private static List<SeriesPoint> BuildSeries(List<ItemRow> rows, ReportPeriod period, DateTime from, DateTime to)
    {
        var points = new Dictionary<DateTime, SeriesPoint>();

        // Create every bucket first so days / weeks / months without sales still appear (as zero).
        for (var bucket = BucketStart(from, period); bucket <= to; bucket = NextBucket(bucket, period))
        {
            points[bucket] = new SeriesPoint { Start = bucket, Label = BucketLabel(bucket, period) };
        }

        foreach (var row in rows)
        {
            var key = BucketStart(row.SaleDate, period);
            if (points.TryGetValue(key, out var point))
            {
                point.Sales += row.Revenue;
                point.Cogs += row.Cost;
            }
        }

        return points.Values.OrderBy(p => p.Start).ToList();
    }

    private static DateTime BucketStart(DateTime date, ReportPeriod period) => period switch
    {
        ReportPeriod.Weekly => StartOfWeek(date),
        ReportPeriod.Monthly => new DateTime(date.Year, date.Month, 1),
        _ => date.Date
    };

    private static DateTime NextBucket(DateTime start, ReportPeriod period) => period switch
    {
        ReportPeriod.Weekly => start.AddDays(7),
        ReportPeriod.Monthly => start.AddMonths(1),
        _ => start.AddDays(1)
    };

    private static string BucketLabel(DateTime start, ReportPeriod period) => period switch
    {
        ReportPeriod.Weekly => "Wk " + start.ToString("dd MMM", Inv),
        ReportPeriod.Monthly => start.ToString("MMM yyyy", Inv),
        _ => start.ToString("dd MMM", Inv)
    };

    /// <summary>Weeks start on Monday.</summary>
    public static DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-diff);
    }

    private static (DateTime From, DateTime To) DefaultRange(ReportPeriod period, DateTime today) => period switch
    {
        ReportPeriod.Weekly => (StartOfWeek(today).AddDays(-7 * 7), today),
        ReportPeriod.Monthly => (new DateTime(today.Year, today.Month, 1).AddMonths(-5), today),
        _ => (today.AddDays(-6), today)
    };

    private static List<ReportPreset> BuildPresets(DateTime today)
    {
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = monthStart.AddMonths(-1);

        return new List<ReportPreset>
        {
            new() { Label = "Today", Period = ReportPeriod.Daily, From = today, To = today },
            new() { Label = "Last 7 days", Period = ReportPeriod.Daily, From = today.AddDays(-6), To = today },
            new() { Label = "This month", Period = ReportPeriod.Daily, From = monthStart, To = today },
            new() { Label = "Last month", Period = ReportPeriod.Daily, From = lastMonthStart, To = monthStart.AddDays(-1) },
            new() { Label = "Last 6 months", Period = ReportPeriod.Monthly, From = monthStart.AddMonths(-5), To = today }
        };
    }
}
