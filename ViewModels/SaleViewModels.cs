using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockSense.Models;

namespace StockSense.ViewModels;

public class SaleRow
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string? CustomerName { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsDemo { get; set; }

    public decimal CreditAmount => TotalAmount - AmountPaid;
}

public class SaleListViewModel
{
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>all | paid | credit</summary>
    public string Status { get; set; } = "all";

    public PagedResult<SaleRow> Sales { get; set; } = new();
    public decimal RangeTotal { get; set; }
    public decimal RangeCredit { get; set; }
}

public class SaleLineInput
{
    public int? ProductId { get; set; }

    [Range(0.001d, 999999d, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; } = 1;
}

public class SaleCreateViewModel
{
    public Guid ClientToken { get; set; }

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    /// <summary>false = paid in full now; true = credit (udhaar) sale.</summary>
    public bool OnCredit { get; set; }

    [Range(0d, 9999999.99d, ErrorMessage = "Amount paid now cannot be negative.")]
    [Display(Name = "Amount paid now")]
    public decimal AmountPaidNow { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Range(0d, 9999999.99d, ErrorMessage = "Discount cannot be negative.")]
    public decimal Discount { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    public List<SaleLineInput> Lines { get; set; } = new();

    public List<SelectListItem> Customers { get; set; } = new();
    public List<SelectListItem> PaymentMethods { get; set; } = new();
    public List<ProductOption> Products { get; set; } = new();
}
