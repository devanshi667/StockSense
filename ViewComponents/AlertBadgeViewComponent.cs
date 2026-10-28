using Microsoft.AspNetCore.Mvc;
using StockSense.Services;

namespace StockSense.ViewComponents;

/// <summary>Number shown next to "Stock alerts" in the sidebar (low stock + expiring + expired products).</summary>
public class AlertBadgeViewComponent : ViewComponent
{
    private readonly IAlertService _alerts;

    public AlertBadgeViewComponent(IAlertService alerts)
    {
        _alerts = alerts;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = 0;
        try
        {
            count = (await _alerts.GetCountsAsync()).AttentionCount;
        }
        catch (Exception)
        {
            // The sidebar must never break a page; just hide the badge.
            count = 0;
        }

        return View(count);
    }
}
