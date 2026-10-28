namespace StockSense.Models;

public class Purchase
{
    public int Id { get; set; }

    /// <summary>Human readable number, e.g. PUR-00012. Assigned after the first save.</summary>
    public string PurchaseNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateTime PurchaseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    /// <summary>Random token created when the form is opened. Unique, so a double submit cannot save twice.</summary>
    public Guid ClientToken { get; set; }

    public DateTime CreatedAt { get; set; }
    public bool IsDemo { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}

public class PurchaseItem
{
    public int Id { get; set; }

    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>Optional expiry date of this batch. If given it becomes the product's expiry date.</summary>
    public DateTime? ExpiryDate { get; set; }
}
