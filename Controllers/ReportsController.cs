using Microsoft.AspNetCore.Mvc;
using StockSense.Services;

namespace StockSense.Controllers;

public class ReportsController : Controller
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    /// <param name="period">daily | weekly | monthly - how the range is grouped in the chart and table.</param>
    public async Task<IActionResult> Index(string period = "daily", DateTime? from = null, DateTime? to = null)
    {
        var reportPeriod = (period ?? string.Empty).ToLowerInvariant() switch
        {
            "weekly" => ReportPeriod.Weekly,
            "monthly" => ReportPeriod.Monthly,
            _ => ReportPeriod.Daily
        };

        var model = await _reports.GetReportAsync(reportPeriod, from, to);
        return View(model);
    }
}
