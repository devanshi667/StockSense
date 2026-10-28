using StockSense.Services;

namespace StockSense.ViewModels;

public class ReportPreset
{
    public string Label { get; set; } = string.Empty;
    public ReportPeriod Period { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public class ReportViewModel
{
    public ReportPeriod Period { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? RangeNotice { get; set; }

    public SalesSummary Summary { get; set; } = new();
    public List<SeriesPoint> Series { get; set; } = new();
    public List<ProductProfitRow> Products { get; set; } = new();
    public List<ProductProfitRow> BestSellers { get; set; } = new();

    public List<SaleRow> Sales { get; set; } = new();
    public int TotalSalesInRange { get; set; }

    public List<ReportPreset> Presets { get; set; } = new();

    public string PeriodLabel => Period switch
    {
        ReportPeriod.Weekly => "Weekly",
        ReportPeriod.Monthly => "Monthly",
        _ => "Daily"
    };

    public bool HasData => Summary.SalesCount > 0;
}

public class RecentTransaction
{
    public DateTime Date { get; set; }

    /// <summary>Sale, Purchase or Payment.</summary>
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Controller { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int RouteId { get; set; }
}

public class DashboardViewModel
{
    public StockCounts Stock { get; set; } = new();
    public int ExpiryWarningDays { get; set; }

    public decimal TodaySales { get; set; }
    public int TodaySalesCount { get; set; }

    public decimal OutstandingCredit { get; set; }
    public int CustomersWithDues { get; set; }

    public SalesSummary Month { get; set; } = new();
    public string MonthLabel { get; set; } = string.Empty;

    public List<RecentTransaction> Recent { get; set; } = new();
    public List<AlertItem> Alerts { get; set; } = new();
    public int TotalAlertCount { get; set; }

    public List<SeriesPoint> MonthlySeries { get; set; } = new();
    public List<SeriesPoint> DailySeries { get; set; } = new();

    public bool IsEmpty => Stock.TotalProducts == 0;
    public bool HasSalesHistory => MonthlySeries.Any(p => p.Sales > 0) || DailySeries.Any(p => p.Sales > 0);
}
