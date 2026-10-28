using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockSense.Models;
using StockSense.Services;

namespace StockSense.ViewModels;

public class CustomerRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsDemo { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Outstanding => TotalCredit - TotalPaid;
}

public class CustomerListViewModel
{
    public string? Search { get; set; }
    public bool OnlyOutstanding { get; set; }
    public PagedResult<CustomerRow> Customers { get; set; } = new();
    public decimal TotalOutstanding { get; set; }
    public int CustomersWithDues { get; set; }
}

public class CustomerFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Customer name is required.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    [RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a 10-digit mobile number (digits only).")]
    [Display(Name = "Mobile number")]
    public string Mobile { get; set; } = string.Empty;

    [StringLength(250)]
    [Display(Name = "Address (optional)")]
    public string? Address { get; set; }

    public bool IsEdit => Id > 0;
}

public class PaymentInput
{
    public Guid ClientToken { get; set; }

    [Range(0.01, 9999999.99, ErrorMessage = "Payment must be greater than zero.")]
    public decimal Amount { get; set; }

    [Display(Name = "Payment method")]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Payment date")]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [StringLength(200)]
    public string? Notes { get; set; }
}

public class CustomerLedgerViewModel
{
    public Customer Customer { get; set; } = new();
    public CustomerBalance Balance { get; set; } = new();
    public List<LedgerEntry> Entries { get; set; } = new();
    public PaymentInput Payment { get; set; } = new();
    public List<SelectListItem> PaymentMethods { get; set; } = new();
}
