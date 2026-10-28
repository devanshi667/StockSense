using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.ViewModels;

namespace StockSense.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetAsync();
}

public class DashboardService : IDashboardService
{
    private readonly StockSenseDbContext _db;
    private readonly IAlertService _alerts;
    private readonly IReportService _reports;
    private readonly ICustomerLedgerService _ledger;
    private readonly ISettingsService _settings;

    public DashboardService(
        StockSenseDbContext db,
        IAlertService alerts,
        IReportService reports,
        ICustomerLedgerService ledger,
        ISettingsService settings)
    {
        _db = db;
        _alerts = alerts;
        _reports = reports;
        _ledger = ledger;
        _settings = settings;
    }

    public async Task<DashboardViewModel> GetAsync()
    {
        var today = AppTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var allAlerts = await _alerts.GetAlertsAsync();
        var balances = await _ledger.GetBalancesAsync();

        var todaySummary = await _reports.GetSummaryAsync(today, today);

        return new DashboardViewModel
        {
            Stock = await _alerts.GetCountsAsync(),
            ExpiryWarningDays = await _settings.GetExpiryWarningDaysAsync(),
            TodaySales = todaySummary.Revenue,
            TodaySalesCount = todaySummary.SalesCount,
            OutstandingCredit = balances.Values.Sum(b => b.Outstanding),
            CustomersWithDues = balances.Values.Count(b => b.Outstanding > 0),
            Month = await _reports.GetSummaryAsync(monthStart, today),
            MonthLabel = monthStart.ToString("MMMM yyyy"),
            Alerts = allAlerts.Take(6).ToList(),
            TotalAlertCount = allAlerts.Count,
            Recent = await GetRecentAsync(),
            MonthlySeries = await _reports.GetSeriesAsync(ReportPeriod.Monthly, monthStart.AddMonths(-5), today),
            DailySeries = await _reports.GetSeriesAsync(ReportPeriod.Daily, today.AddDays(-13), today)
        };
    }

    private async Task<List<RecentTransaction>> GetRecentAsync()
    {
        var sales = await _db.Sales.AsNoTracking()
            .OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id)
            .Take(6)
            .Select(s => new RecentTransaction
            {
                Date = s.SaleDate,
                Type = "Sale",
                Title = s.InvoiceNumber,
                Subtitle = s.Customer != null ? s.Customer.Name : "Walk-in customer",
                Amount = s.TotalAmount,
                Controller = "Sales",
                Action = "Details",
                RouteId = s.Id
            })
            .ToListAsync();

        var payments = await _db.Payments.AsNoTracking()
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .Take(4)
            .Select(p => new RecentTransaction
            {
                Date = p.PaymentDate,
                Type = "Payment",
                Title = "Udhaar payment",
                Subtitle = p.Customer!.Name,
                Amount = p.Amount,
                Controller = "Customers",
                Action = "Ledger",
                RouteId = p.CustomerId
            })
            .ToListAsync();

        var purchases = await _db.Purchases.AsNoTracking()
            .OrderByDescending(p => p.PurchaseDate).ThenByDescending(p => p.Id)
            .Take(4)
            .Select(p => new RecentTransaction
            {
                Date = p.PurchaseDate,
                Type = "Purchase",
                Title = p.PurchaseNumber,
                Subtitle = p.Supplier!.Name,
                Amount = p.TotalAmount,
                Controller = "Purchases",
                Action = "Details",
                RouteId = p.Id
            })
            .ToListAsync();

        return sales.Concat(payments).Concat(purchases)
            .OrderByDescending(t => t.Date)
            .Take(8)
            .ToList();
    }
}
