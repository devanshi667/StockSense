namespace StockSense.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>True for rows created by the optional demo-data loader.</summary>
    public bool IsDemo { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Unit { get; set; } = "pcs";

    /// <summary>Latest purchase cost per unit (updated whenever a purchase is recorded).</summary>
    public decimal PurchaseCost { get; set; }
    public decimal SellingPrice { get; set; }

    public decimal CurrentStock { get; set; }
    public decimal MinStockLevel { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsDemo { get; set; }

    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDemo { get; set; }

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
