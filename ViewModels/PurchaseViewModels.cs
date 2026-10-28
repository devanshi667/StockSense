using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace StockSense.ViewModels;

public class PurchaseRow
{
    public int Id { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsDemo { get; set; }
}

public class PurchaseListViewModel
{
    public string? Search { get; set; }
    public int? SupplierId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public PagedResult<PurchaseRow> Purchases { get; set; } = new();
    public List<SelectListItem> Suppliers { get; set; } = new();
    public decimal PageTotal { get; set; }
    public decimal RangeTotal { get; set; }
}

public class PurchaseLineInput
{
    public int? ProductId { get; set; }

    [Range(0.001d, 999999d, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; } = 1;

    [Range(0.01, 9999999.99, ErrorMessage = "Cost must be greater than zero.")]
    public decimal UnitCost { get; set; }

    [DataType(DataType.Date)]
    public DateTime? ExpiryDate { get; set; }
}

public class PurchaseCreateViewModel
{
    public Guid ClientToken { get; set; }

    [Required(ErrorMessage = "Please select a supplier.")]
    [Display(Name = "Supplier")]
    public int? SupplierId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Purchase date")]
    public DateTime PurchaseDate { get; set; } = DateTime.Today;

    [StringLength(300)]
    public string? Notes { get; set; }

    public List<PurchaseLineInput> Lines { get; set; } = new();

    public List<SelectListItem> Suppliers { get; set; } = new();
    public List<ProductOption> Products { get; set; } = new();
}
