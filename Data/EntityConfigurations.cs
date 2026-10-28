using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockSense.Models;

namespace StockSense.Data;

// Deletion rules in this model:
//  * Anything that has transaction history is RESTRICTED from deletion (products, customers, suppliers, categories).
//  * Child lines (sale items, purchase items) are CASCADE-deleted with their parent document.

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.Property(x => x.Name).IsRequired().HasMaxLength(80);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.Property(x => x.Sku).IsRequired().HasMaxLength(50);
        b.Property(x => x.Unit).IsRequired().HasMaxLength(20);
        b.Property(x => x.PurchaseCost).HasPrecision(12, 2);
        b.Property(x => x.SellingPrice).HasPrecision(12, 2);
        b.Property(x => x.CurrentStock).HasPrecision(12, 3);
        b.Property(x => x.MinStockLevel).HasPrecision(12, 3);
        b.Property(x => x.ExpiryDate).HasColumnType("date");

        b.HasIndex(x => x.Sku).IsUnique();
        b.HasIndex(x => x.Name);
        b.HasIndex(x => x.ExpiryDate);

        b.HasOne(x => x.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("Suppliers");
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Address).HasMaxLength(250);
        b.Property(x => x.Notes).HasMaxLength(300);
        b.HasIndex(x => x.Name);
    }
}

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> b)
    {
        b.ToTable("Purchases");
        b.Property(x => x.PurchaseNumber).IsRequired().HasMaxLength(40);
        b.Property(x => x.TotalAmount).HasPrecision(14, 2);
        b.Property(x => x.Notes).HasMaxLength(300);
        b.Property(x => x.PurchaseDate).HasColumnType("date");

        b.HasIndex(x => x.PurchaseNumber).IsUnique();
        b.HasIndex(x => x.ClientToken).IsUnique();
        b.HasIndex(x => x.PurchaseDate);

        b.HasOne(x => x.Supplier)
            .WithMany(s => s.Purchases)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> b)
    {
        b.ToTable("PurchaseItems");
        b.Property(x => x.Quantity).HasPrecision(12, 3);
        b.Property(x => x.UnitCost).HasPrecision(12, 2);
        b.Property(x => x.LineTotal).HasPrecision(14, 2);
        b.Property(x => x.ExpiryDate).HasColumnType("date");

        b.HasOne(x => x.Purchase)
            .WithMany(p => p.Items)
            .HasForeignKey(x => x.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Product)
            .WithMany(p => p.PurchaseItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.Property(x => x.Mobile).IsRequired().HasMaxLength(15);
        b.Property(x => x.Address).HasMaxLength(250);
        b.HasIndex(x => x.Mobile).IsUnique();
        b.HasIndex(x => x.Name);
    }
}

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("Sales");
        b.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(40);
        b.Property(x => x.SubTotal).HasPrecision(14, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(14, 2);
        b.Property(x => x.TotalAmount).HasPrecision(14, 2);
        b.Property(x => x.AmountPaid).HasPrecision(14, 2);
        b.Property(x => x.Notes).HasMaxLength(300);

        b.HasIndex(x => x.InvoiceNumber).IsUnique();
        b.HasIndex(x => x.ClientToken).IsUnique();
        b.HasIndex(x => x.SaleDate);
        b.HasIndex(x => x.CustomerId);

        b.HasOne(x => x.Customer)
            .WithMany(c => c.Sales)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> b)
    {
        b.ToTable("SaleItems");
        b.Property(x => x.Quantity).HasPrecision(12, 3);
        b.Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Property(x => x.UnitCost).HasPrecision(12, 2);
        b.Property(x => x.LineTotal).HasPrecision(14, 2);
        b.Property(x => x.LineDiscount).HasPrecision(14, 2);
        b.Property(x => x.LineCost).HasPrecision(14, 2);

        b.HasOne(x => x.Sale)
            .WithMany(s => s.Items)
            .HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Product)
            .WithMany(p => p.SaleItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments");
        b.Property(x => x.Amount).HasPrecision(14, 2);
        b.Property(x => x.Notes).HasMaxLength(200);

        b.HasIndex(x => x.ClientToken).IsUnique();
        b.HasIndex(x => new { x.CustomerId, x.PaymentDate });

        b.HasOne(x => x.Customer)
            .WithMany(c => c.Payments)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.ToTable("AppSettings");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(60);
        b.Property(x => x.Value).IsRequired().HasMaxLength(200);
    }
}
