using Microsoft.AspNetCore.Mvc;
using StockSense.Services;
using StockSense.ViewModels;

namespace StockSense.Controllers;

public class HomeController : Controller
{
    private readonly IDashboardService _dashboard;

    public HomeController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public async Task<IActionResult> Index()
    {
        var model = await _dashboard.GetAsync();
        return View(model);
    }

    [IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View("Error", new ErrorViewModel
        {
            StatusCode = 500,
            Title = "Something went wrong",
            Message = "An unexpected error occurred. Your data was not changed by this failed action. Please go back and try again."
        });
    }

    [IgnoreAntiforgeryToken]
    public IActionResult HttpStatus(int code)
    {
        var model = code switch
        {
            404 => new ErrorViewModel
            {
                StatusCode = 404,
                Title = "Page not found",
                Message = "The page or record you are looking for does not exist. It may have been deleted or the link is wrong."
            },
            400 => new ErrorViewModel
            {
                StatusCode = 400,
                Title = "Request could not be processed",
                Message = "The form expired or was invalid. Please go back, reload the page and try again."
            },
            _ => new ErrorViewModel { StatusCode = code }
        };

        return View("Error", model);
    }
}
