using System.ComponentModel.DataAnnotations.Schema;

namespace StockSense.Models;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDemo { get; set; }

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class Sale
{
    public int Id { get; set; }

    /// <summary>Human readable number, e.g. INV-00042. Assigned after the first save.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime SaleDate { get; set; }

    /// <summary>Optional for fully paid walk-in sales, required for credit (udhaar) sales.</summary>
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }

    /// <summary>Final bill amount = SubTotal - DiscountAmount. This is the sale's revenue.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Money received at the time of sale. The remaining amount is customer credit (udhaar).</summary>
    public decimal AmountPaid { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }

    public Guid ClientToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDemo { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();

    /// <summary>Amount put on credit by this sale (not stored; derived from saved values).</summary>
    [NotMapped]
    public decimal CreditAmount => TotalAmount - AmountPaid;
}

public class SaleItem
{
    public int Id { get; set; }

    public int SaleId { get; set; }
    public Sale? Sale { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Selling price per unit at the moment of sale (history-safe snapshot).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Purchase cost per unit at the moment of sale (history-safe snapshot).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Quantity x UnitPrice, before discount.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>This line's share of the bill discount (shares add up exactly to the sale discount).</summary>
    public decimal LineDiscount { get; set; }

    /// <summary>Quantity x UnitCost - cost of goods sold for this line.</summary>
    public decimal LineCost { get; set; }

    [NotMapped]
    public decimal NetRevenue => LineTotal - LineDiscount;

    [NotMapped]
    public decimal GrossProfit => LineTotal - LineDiscount - LineCost;
}

/// <summary>Money received from a customer later, against their udhaar (credit) balance.</summary>
public class Payment
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }

    public Guid ClientToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDemo { get; set; }
}
