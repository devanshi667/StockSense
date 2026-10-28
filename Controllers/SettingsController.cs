using Microsoft.AspNetCore.Mvc;
using StockSense.Services;
using StockSense.ViewModels;

namespace StockSense.Controllers;

public class SettingsController : Controller
{
    private readonly ISettingsService _settings;
    private readonly IDemoDataService _demo;

    public SettingsController(ISettingsService settings, IDemoDataService demo)
    {
        _settings = settings;
        _demo = demo;
    }

    public async Task<IActionResult> Index()
    {
        var model = new SettingsViewModel
        {
            ExpiryWarningDays = await _settings.GetExpiryWarningDaysAsync(),
            DemoCounts = await _demo.GetCountsAsync()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SaveExpiry(SettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.DemoCounts = await _demo.GetCountsAsync();
            return View("Index", model);
        }

        await _settings.SetExpiryWarningDaysAsync(model.ExpiryWarningDays);
        TempData["Success"] = $"Expiry warning period saved: products are flagged {model.ExpiryWarningDays} day(s) before they expire.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> LoadDemo()
    {
        var result = await _demo.LoadAsync();
        if (result.Succeeded)
        {
            TempData["Success"] = result.Message ?? "Demo data loaded.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveDemo()
    {
        var result = await _demo.RemoveAsync();
        if (result.Succeeded)
        {
            TempData["Success"] = result.Message ?? "Demo data removed.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors);
        }

        return RedirectToAction(nameof(Index));
    }
}
