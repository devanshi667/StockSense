using System.ComponentModel.DataAnnotations;
using StockSense.Services;

namespace StockSense.ViewModels;

public class AlertsViewModel
{
    public int ExpiryWarningDays { get; set; }
    public string Tab { get; set; } = "alerts";
    public string Kind { get; set; } = "all";
    public StockCounts Counts { get; set; } = new();
    public List<AlertItem> Alerts { get; set; } = new();
    public List<ReorderSuggestion> Reorder { get; set; } = new();
    public decimal ReorderEstimate => Reorder.Sum(r => r.EstimatedCost);
}

public class SettingsViewModel
{
    [Range(1, 365, ErrorMessage = "Enter a number of days between 1 and 365.")]
    [Display(Name = "Expiry warning period (days)")]
    public int ExpiryWarningDays { get; set; } = 30;

    public DemoDataCounts DemoCounts { get; set; } = new();
}

public class ErrorViewModel
{
    public int? StatusCode { get; set; }
    public string Title { get; set; } = "Something went wrong";
    public string Message { get; set; } = "An unexpected error occurred. Please go back and try again.";
}
