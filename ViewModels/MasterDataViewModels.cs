using System.ComponentModel.DataAnnotations;
using StockSense.Models;

namespace StockSense.ViewModels;

public class CategoryRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public bool IsDemo { get; set; }
}

public class CategoryFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(80)]
    [Display(Name = "Category name")]
    public string Name { get; set; } = string.Empty;
}

public class CategoryListViewModel
{
    public List<CategoryRow> Categories { get; set; } = new();
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class SupplierRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int PurchaseCount { get; set; }
    public decimal TotalPurchased { get; set; }
    public bool IsDemo { get; set; }
}

public class SupplierListViewModel
{
    public string? Search { get; set; }
    public PagedResult<SupplierRow> Suppliers { get; set; } = new();
}

public class SupplierFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Supplier name is required.")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    [RegularExpression(@"^[0-9+\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number (digits, spaces, + and - only).")]
    public string? Phone { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public bool IsEdit => Id > 0;
}

public class SupplierDetailsViewModel
{
    public Supplier Supplier { get; set; } = new();
    public List<PurchaseRow> Purchases { get; set; } = new();
    public decimal TotalPurchased { get; set; }
}
