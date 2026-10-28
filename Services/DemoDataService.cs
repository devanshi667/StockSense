using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.Models;

namespace StockSense.Services;

public class DemoDataCounts
{
    public int Categories { get; set; }
    public int Products { get; set; }
    public int Suppliers { get; set; }
    public int Customers { get; set; }
    public int Purchases { get; set; }
    public int Sales { get; set; }
    public int Payments { get; set; }

    public int Total => Categories + Products + Suppliers + Customers + Purchases + Sales + Payments;
    public bool HasDemoData => Total > 0;
}

public interface IDemoDataService
{
    Task<DemoDataCounts> GetCountsAsync();
    Task<OperationResult> LoadAsync();
    Task<OperationResult> RemoveAsync();
}

/// <summary>
/// Optional sample data for demos. Every row it creates has IsDemo = true (shown as a "Demo" badge in the UI).
/// Sales, purchases and payments are created through the normal business services, so stock levels, cost of goods
/// sold and customer balances are exactly what the real rules produce - nothing is faked or hard-coded.
/// </summary>
public class DemoDataService : IDemoDataService
{
    private sealed record DemoProduct(
        string Name, string Sku, string Category, string Unit,
        decimal Cost, decimal Price, decimal MinStock, decimal OpeningPurchaseQty,
        int? ExpiryInDays, int SupplierIndex);

    private static readonly DemoProduct[] Catalog =
    {
        new("Basmati Rice 5 kg", "DEMO-GRN-001", "Grains & Pulses", "pack", 410, 450, 10, 40, null, 0),
        new("Toor Dal 1 kg", "DEMO-GRN-002", "Grains & Pulses", "kg", 128, 150, 15, 60, null, 0),
        new("Chana Dal 1 kg", "DEMO-GRN-003", "Grains & Pulses", "kg", 90, 110, 12, 16, null, 0),
        new("Wheat Flour (Atta) 5 kg", "DEMO-GRN-004", "Grains & Pulses", "pack", 215, 245, 12, 45, null, 0),
        new("Sugar 1 kg", "DEMO-GRN-005", "Grains & Pulses", "kg", 41, 48, 20, 80, null, 0),
        new("Milk 500 ml", "DEMO-DAI-001", "Dairy", "pouch", 26, 29, 20, 100, 3, 1),
        new("Butter 100 g", "DEMO-DAI-002", "Dairy", "pack", 52, 58, 10, 40, 40, 1),
        new("Curd 400 g", "DEMO-DAI-003", "Dairy", "cup", 28, 35, 10, 30, 2, 1),
        new("Paneer 200 g", "DEMO-DAI-004", "Dairy", "pack", 70, 85, 8, 20, -3, 1),
        new("Glucose Biscuits 100 g", "DEMO-SNK-001", "Snacks", "pack", 8, 10, 50, 200, 120, 2),
        new("Masala Potato Chips 52 g", "DEMO-SNK-002", "Snacks", "pack", 17, 20, 30, 38, 90, 2),
        new("Instant Noodles 70 g", "DEMO-SNK-003", "Snacks", "pack", 13, 15, 40, 150, 150, 2),
        new("Tea Powder 250 g", "DEMO-BEV-001", "Beverages", "pack", 118, 135, 10, 40, 300, 0),
        new("Cola 750 ml", "DEMO-BEV-002", "Beverages", "bottle", 36, 40, 24, 96, 60, 2),
        new("Mixed Fruit Juice 1 L", "DEMO-BEV-003", "Beverages", "pack", 95, 110, 10, 30, 22, 2),
        new("Sunflower Oil 1 L", "DEMO-OIL-001", "Oils & Spices", "bottle", 128, 145, 15, 50, 200, 0),
        new("Turmeric Powder 100 g", "DEMO-OIL-002", "Oils & Spices", "pack", 22, 30, 15, 40, 250, 0),
        new("Red Chilli Powder 100 g", "DEMO-OIL-003", "Oils & Spices", "pack", 25, 35, 15, 40, 250, 0),
        new("Iodised Salt 1 kg", "DEMO-OIL-004", "Oils & Spices", "pack", 20, 25, 20, 70, null, 0),
        new("Detergent Powder 1 kg", "DEMO-HSE-001", "Household", "pack", 118, 135, 8, 0, null, 0),   // never purchased -> out of stock
        new("Dishwash Bar", "DEMO-HSE-002", "Household", "pcs", 8, 10, 25, 80, null, 0),
        new("Toothpaste 100 g", "DEMO-PCR-001", "Personal Care", "pcs", 52, 62, 12, 40, null, 2),
        new("Bathing Soap 100 g", "DEMO-PCR-002", "Personal Care", "pcs", 48, 58, 15, 50, null, 2),
        new("Hand Wash 200 ml", "DEMO-PCR-003", "Personal Care", "bottle", 75, 89, 8, 25, null, 2)
    };

    private static readonly string[] SupplierNames =
    {
        "Shree Ganesh Traders", "Fresh Dairy Distributors", "Metro Wholesale Mart"
    };

    private static readonly string[] SupplierPhones = { "9000000101", "9000000102", "9000000103" };

    private static readonly (string Name, string Mobile, string Address)[] CustomerData =
    {
        ("Ramesh Patel", "9000000001", "Near Main Market"),
        ("Sunita Sharma", "9000000002", "Shivaji Chowk"),
        ("Mahesh Desai", "9000000003", "Station Road"),
        ("Kavita Joshi", "9000000004", "Gandhi Nagar"),
        ("Anil Mehta", "9000000005", "Temple Street")
    };

    private readonly StockSenseDbContext _db;
    private readonly IPurchaseService _purchases;
    private readonly ISaleService _sales;
    private readonly ICustomerLedgerService _ledger;

    public DemoDataService(
        StockSenseDbContext db,
        IPurchaseService purchases,
        ISaleService sales,
        ICustomerLedgerService ledger)
    {
        _db = db;
        _purchases = purchases;
        _sales = sales;
        _ledger = ledger;
    }

    public async Task<DemoDataCounts> GetCountsAsync()
    {
        return new DemoDataCounts
        {
            Categories = await _db.Categories.CountAsync(x => x.IsDemo),
            Products = await _db.Products.CountAsync(x => x.IsDemo),
            Suppliers = await _db.Suppliers.CountAsync(x => x.IsDemo),
            Customers = await _db.Customers.CountAsync(x => x.IsDemo),
            Purchases = await _db.Purchases.CountAsync(x => x.IsDemo),
            Sales = await _db.Sales.CountAsync(x => x.IsDemo),
            Payments = await _db.Payments.CountAsync(x => x.IsDemo)
        };
    }

    public async Task<OperationResult> LoadAsync()
    {
        if ((await GetCountsAsync()).HasDemoData)
        {
            return OperationResult.Fail("Demo data is already loaded. Remove it first if you want to load it again.");
        }

        try
        {
            var now = AppTime.Now;
            var today = AppTime.Today;
            var rnd = new Random(42);   // fixed seed: the same demo shop every time

            // ---- master data -------------------------------------------------------------------------------
            var categoryIds = new Dictionary<string, int>();
            foreach (var name in Catalog.Select(c => c.Category).Distinct())
            {
                var existing = await _db.Categories.FirstOrDefaultAsync(c => c.Name == name);
                if (existing == null)
                {
                    existing = new Category { Name = name, IsDemo = true };
                    _db.Categories.Add(existing);
                    await _db.SaveChangesAsync();
                }

                categoryIds[name] = existing.Id;
            }

            var supplierIds = new List<int>();
            for (var i = 0; i < SupplierNames.Length; i++)
            {
                var supplier = new Supplier
                {
                    Name = SupplierNames[i],
                    Phone = SupplierPhones[i],
                    Address = "Wholesale market",
                    CreatedAt = now,
                    IsDemo = true
                };
                _db.Suppliers.Add(supplier);
                await _db.SaveChangesAsync();
                supplierIds.Add(supplier.Id);
            }

            var customerIds = new List<int>();
            foreach (var (name, mobile, address) in CustomerData)
            {
                var customer = new Customer { Name = name, Mobile = mobile, Address = address, CreatedAt = now, IsDemo = true };
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync();
                customerIds.Add(customer.Id);
            }

            // Products start with ZERO stock; stock only arrives through recorded purchases below.
            var productIds = new Dictionary<string, int>();
            foreach (var p in Catalog)
            {
                var product = new Product
                {
                    Name = p.Name,
                    Sku = p.Sku,
                    CategoryId = categoryIds[p.Category],
                    Unit = p.Unit,
                    PurchaseCost = p.Cost,
                    SellingPrice = p.Price,
                    CurrentStock = 0,
                    MinStockLevel = p.MinStock,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDemo = true
                };
                _db.Products.Add(product);
                await _db.SaveChangesAsync();
                productIds[p.Sku] = product.Id;
            }

            _db.ChangeTracker.Clear();

            // ---- purchases (stock arrives here) --------------------------------------------------------------
            var stock = new Dictionary<string, decimal>();
            var expiry = new Dictionary<string, DateTime?>();

            for (var s = 0; s < supplierIds.Count; s++)
            {
                var lines = Catalog.Where(c => c.SupplierIndex == s && c.OpeningPurchaseQty > 0).ToList();
                if (lines.Count == 0) continue;

                var request = new CreatePurchaseRequest
                {
                    ClientToken = Guid.NewGuid(),
                    SupplierId = supplierIds[s],
                    PurchaseDate = today.AddDays(-50),
                    Notes = "Demo opening stock",
                    IsDemo = true,
                    Lines = lines.Select(l => new PurchaseLineRequest
                    {
                        ProductId = productIds[l.Sku],
                        Quantity = l.OpeningPurchaseQty,
                        UnitCost = l.Cost,
                        ExpiryDate = l.ExpiryInDays.HasValue ? today.AddDays(l.ExpiryInDays.Value) : null
                    }).ToList()
                };

                var result = await _purchases.CreateAsync(request);
                if (!result.Succeeded)
                {
                    return OperationResult.Fail("Demo purchase failed: " + string.Join(" ", result.Errors));
                }

                foreach (var l in lines)
                {
                    stock[l.Sku] = l.OpeningPurchaseQty;
                    expiry[l.Sku] = l.ExpiryInDays.HasValue ? today.AddDays(l.ExpiryInDays.Value) : null;
                }
            }

            // A restock of a few fast movers about 2.5 weeks ago.
            var restock = new (string Sku, decimal Qty)[]
            {
                ("DEMO-DAI-001", 60), ("DEMO-DAI-003", 20), ("DEMO-SNK-001", 100), ("DEMO-GRN-005", 40)
            };

            var restockResult = await _purchases.CreateAsync(new CreatePurchaseRequest
            {
                ClientToken = Guid.NewGuid(),
                SupplierId = supplierIds[1],
                PurchaseDate = today.AddDays(-18),
                Notes = "Demo restock",
                IsDemo = true,
                Lines = restock.Select(r =>
                {
                    var def = Catalog.First(c => c.Sku == r.Sku);
                    return new PurchaseLineRequest
                    {
                        ProductId = productIds[r.Sku],
                        Quantity = r.Qty,
                        UnitCost = def.Cost,
                        ExpiryDate = def.ExpiryInDays.HasValue ? today.AddDays(def.ExpiryInDays.Value) : null
                    };
                }).ToList()
            });

            if (!restockResult.Succeeded)
            {
                return OperationResult.Fail("Demo restock failed: " + string.Join(" ", restockResult.Errors));
            }

            foreach (var r in restock)
            {
                stock[r.Sku] += r.Qty;
            }

            // ---- sales ---------------------------------------------------------------------------------------
            var salesCreated = 0;
            for (var dayOffset = -44; dayOffset <= 0; dayOffset++)
            {
                var day = today.AddDays(dayOffset);
                var salesToday = rnd.Next(2, 6);

                for (var n = 0; n < salesToday; n++)
                {
                    var saleDate = day.AddHours(9).AddMinutes(rnd.Next(0, 660));
                    if (saleDate > now)
                    {
                        saleDate = now.AddMinutes(-rnd.Next(1, 90));
                    }

                    var candidates = Catalog
                        .Where(c => stock.TryGetValue(c.Sku, out var available) && available >= 1 &&
                                    (!expiry[c.Sku].HasValue || expiry[c.Sku]!.Value.Date >= saleDate.Date))
                        .OrderBy(_ => rnd.Next())
                        .Take(rnd.Next(1, 5))
                        .ToList();

                    if (candidates.Count == 0) continue;

                    var lines = new List<SaleLineRequest>();
                    decimal subTotal = 0;
                    foreach (var c in candidates)
                    {
                        var qty = Math.Min(stock[c.Sku], rnd.Next(1, 4));
                        lines.Add(new SaleLineRequest { ProductId = productIds[c.Sku], Quantity = qty });
                        subTotal += qty * c.Price;
                    }

                    var onCredit = rnd.Next(0, 100) < 25;
                    var request = new CreateSaleRequest
                    {
                        ClientToken = Guid.NewGuid(),
                        SaleDate = saleDate,
                        IsDemo = true,
                        Lines = lines,
                        PaymentMethod = rnd.Next(0, 100) < 55 ? PaymentMethod.Cash : PaymentMethod.Upi
                    };

                    if (onCredit)
                    {
                        request.OnCredit = true;
                        request.CustomerId = customerIds[rnd.Next(customerIds.Count)];
                        request.AmountPaidNow = rnd.Next(0, 2) == 0 ? 0 : Math.Floor(subTotal * 0.4m);
                    }
                    else if (subTotal > 100 && rnd.Next(0, 100) < 10)
                    {
                        request.Discount = Math.Min(10m, Math.Floor(subTotal * 0.05m));
                    }

                    var result = await _sales.CreateAsync(request);
                    if (result.Succeeded)
                    {
                        salesCreated++;
                        foreach (var line in lines)
                        {
                            var sku = Catalog.First(c => productIds[c.Sku] == line.ProductId).Sku;
                            stock[sku] -= line.Quantity;
                        }
                    }
                }
            }

            // ---- udhaar payments -----------------------------------------------------------------------------
            var paidCustomers = 0;
            for (var i = 0; i < customerIds.Count && paidCustomers < 3; i++)
            {
                var balance = await _ledger.GetBalanceAsync(customerIds[i]);
                if (balance.Outstanding <= 0) continue;

                var amount = paidCustomers == 0
                    ? balance.Outstanding                       // one customer settles fully
                    : Money.R2(balance.Outstanding * 0.5m);     // others pay part

                if (amount <= 0) continue;

                var paymentResult = await _ledger.RecordPaymentAsync(new RecordPaymentRequest
                {
                    ClientToken = Guid.NewGuid(),
                    CustomerId = customerIds[i],
                    Amount = amount,
                    Method = paidCustomers == 1 ? PaymentMethod.Cash : PaymentMethod.Upi,
                    PaymentDate = today.AddDays(-rnd.Next(1, 8)),
                    Notes = "Demo payment",
                    IsDemo = true
                });

                if (paymentResult.Succeeded) paidCustomers++;
            }

            var counts = await GetCountsAsync();
            var ok = OperationResult.Ok();
            ok.Message = $"Demo data loaded: {counts.Products} products, {counts.Suppliers} suppliers, {counts.Customers} customers, " +
                         $"{counts.Purchases} purchases, {counts.Sales} sales and {counts.Payments} payments.";
            return ok;
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(
                "Demo data could not be fully loaded (" + ex.GetBaseException().Message + "). " +
                "Use \"Remove demo data\" to clean up any partial data, then try again.");
        }
    }

    public async Task<OperationResult> RemoveAsync()
    {
        try
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            // Order matters: documents first, then the records they pointed to.
            var payments = await _db.Payments.Where(x => x.IsDemo).ExecuteDeleteAsync();
            var sales = await _db.Sales.Where(x => x.IsDemo).ExecuteDeleteAsync();          // sale items cascade
            var purchases = await _db.Purchases.Where(x => x.IsDemo).ExecuteDeleteAsync();  // purchase items cascade

            // Demo master data is kept if real (non-demo) records still reference it.
            var products = await _db.Products
                .Where(x => x.IsDemo && !x.SaleItems.Any() && !x.PurchaseItems.Any())
                .ExecuteDeleteAsync();
            var customers = await _db.Customers
                .Where(x => x.IsDemo && !x.Sales.Any() && !x.Payments.Any())
                .ExecuteDeleteAsync();
            var suppliers = await _db.Suppliers
                .Where(x => x.IsDemo && !x.Purchases.Any())
                .ExecuteDeleteAsync();
            await _db.Categories
                .Where(x => x.IsDemo && !x.Products.Any())
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();

            var ok = OperationResult.Ok();
            ok.Message = $"Demo data removed: {products} products, {suppliers} suppliers, {customers} customers, " +
                         $"{purchases} purchases, {sales} sales and {payments} payments.";
            return ok;
        }
        catch (Exception ex)
        {
            return OperationResult.Fail("Demo data could not be removed: " + ex.GetBaseException().Message);
        }
    }
}
