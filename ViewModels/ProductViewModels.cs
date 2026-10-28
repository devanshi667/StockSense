using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockSense.Helpers;
using StockSense.Models;
using StockSense.Services;

namespace StockSense.ViewModels;

public class ProductRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal PurchaseCost { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal MinStockLevel { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsDemo { get; set; }

    /// <summary>Set by the controller from the saved setting.</summary>
    public int ExpiryWarningDays { get; set; } = 30;

    public bool IsOutOfStock => CurrentStock <= 0;
    public bool IsLowStock => CurrentStock <= MinStockLevel;
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < AppTime.Today;
    public int? DaysToExpiry => ExpiryDate.HasValue ? (int)(ExpiryDate.Value.Date - AppTime.Today).TotalDays : null;
    public bool IsExpiringSoon => DaysToExpiry.HasValue && DaysToExpiry.Value >= 0 && DaysToExpiry.Value <= ExpiryWarningDays;
}

public class ProductListViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string Filter { get; set; } = "all";
    public string Sort { get; set; } = "name";
    public PagedResult<ProductRow> Products { get; set; } = new();
    public List<SelectListItem> Categories { get; set; } = new();
    public int ExpiryWarningDays { get; set; }
    public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || CategoryId.HasValue || Filter != "all";
}

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(150)]
    [Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "SKU / product code is required.")]
    [StringLength(50)]
    [RegularExpression(@"^[A-Za-z0-9\-_\./]+$", ErrorMessage = "Use only letters, numbers and - _ . / in the SKU.")]
    [Display(Name = "SKU / code")]
    public string Sku { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a category.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Unit is required (for example kg, pcs, litre).")]
    [StringLength(20)]
    public string Unit { get; set; } = "pcs";

    [Range(0.01, 9999999.99, ErrorMessage = "Purchase cost must be greater than zero.")]
    [Display(Name = "Purchase cost (per unit)")]
    public decimal PurchaseCost { get; set; }

    [Range(0.01, 9999999.99, ErrorMessage = "Selling price must be greater than zero.")]
    [Display(Name = "Selling price (per unit)")]
    public decimal SellingPrice { get; set; }

    [Range(0d, 999999999d, ErrorMessage = "Stock cannot be negative.")]
    [Display(Name = "Opening stock")]
    public decimal CurrentStock { get; set; }

    [Range(0d, 999999999d, ErrorMessage = "Minimum stock cannot be negative.")]
    [Display(Name = "Minimum stock level")]
    public decimal MinStockLevel { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Expiry date (optional)")]
    public DateTime? ExpiryDate { get; set; }

    public bool IsEdit => Id > 0;

    /// <summary>Shown read-only on the edit page. Stock changes go through purchases, sales or a stock correction.</summary>
    public decimal DisplayStock { get; set; }

    public List<SelectListItem> Categories { get; set; } = new();
}

public class ProductDetailsViewModel
{
    public Product Product { get; set; } = new();
    public int ExpiryWarningDays { get; set; }
    public List<AlertItem> Alerts { get; set; } = new();

    public decimal TotalSoldQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalGrossProfit { get; set; }

    public List<SaleItem> RecentSales { get; set; } = new();
    public List<PurchaseItem> RecentPurchases { get; set; } = new();

    public bool CanDelete { get; set; }

    public decimal MarginPercent => Product.SellingPrice <= 0
        ? 0
        : (Product.SellingPrice - Product.PurchaseCost) / Product.SellingPrice * 100m;
}
