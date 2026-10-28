using Microsoft.AspNetCore.Mvc;
using StockSense.Services;
using StockSense.ViewModels;

namespace StockSense.Controllers;

/// <summary>Low-stock, expiry and reorder screens ("Stock alerts" in the sidebar).</summary>
public class StockController : Controller
{
    private readonly IAlertService _alerts;
    private readonly ISettingsService _settings;

    public StockController(IAlertService alerts, ISettingsService settings)
    {
        _alerts = alerts;
        _settings = settings;
    }

    public async Task<IActionResult> Index(string tab = "alerts", string kind = "all")
    {
        var all = await _alerts.GetAlertsAsync();

        IEnumerable<AlertItem> filtered = kind switch
        {
            "expired" => all.Where(a => a.Kind == AlertKind.Expired),
            "expiring" => all.Where(a => a.Kind == AlertKind.ExpiringSoon),
            "low" => all.Where(a => a.Kind == AlertKind.LowStock),
            "out" => all.Where(a => a.Kind == AlertKind.OutOfStock),
            _ => all
        };

        var model = new AlertsViewModel
        {
            Tab = tab == "reorder" ? "reorder" : "alerts",
            Kind = kind,
            ExpiryWarningDays = await _settings.GetExpiryWarningDaysAsync(),
            Counts = await _alerts.GetCountsAsync(),
            Alerts = filtered.ToList(),
            Reorder = await _alerts.GetReorderSuggestionsAsync()
        };

        return View(model);
    }
}
