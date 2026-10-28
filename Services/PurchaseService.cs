using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.Models;

namespace StockSense.Services;

public class PurchaseLineRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class CreatePurchaseRequest
{
    public Guid ClientToken { get; set; }
    public int SupplierId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseLineRequest> Lines { get; set; } = new();
    public bool IsDemo { get; set; }
}

public interface IPurchaseService
{
    Task<OperationResult> CreateAsync(CreatePurchaseRequest request);
}

public class PurchaseService : IPurchaseService
{
    private readonly StockSenseDbContext _db;

    public PurchaseService(StockSenseDbContext db)
    {
        _db = db;
    }

    public async Task<OperationResult> CreateAsync(CreatePurchaseRequest request)
    {
        if (request.ClientToken == Guid.Empty)
        {
            return OperationResult.Fail("This form is no longer valid. Please reload the page and try again.");
        }

        var existingId = await FindExistingAsync(request.ClientToken);
        if (existingId.HasValue)
        {
            return OperationResult.Ok(existingId.Value, duplicate: true);
        }

        if (request.Lines == null || request.Lines.Count == 0)
        {
            return OperationResult.Fail("Add at least one product to the purchase.");
        }

        var purchaseDate = request.PurchaseDate.Date;
        if (purchaseDate > AppTime.Today)
        {
            return OperationResult.Fail("Purchase date cannot be in the future.");
        }

        if (!await _db.Suppliers.AnyAsync(s => s.Id == request.SupplierId))
        {
            return OperationResult.Fail("Please select a valid supplier.");
        }

        var ids = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var existingProducts = await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        var errors = new List<string>();
        var items = new List<PurchaseItem>();

        foreach (var line in request.Lines)
        {
            if (!existingProducts.Contains(line.ProductId))
            {
                errors.Add("One of the selected products no longer exists.");
                continue;
            }

            var quantity = Money.R3(line.Quantity);
            var unitCost = Money.R2(line.UnitCost);

            if (quantity <= 0)
            {
                errors.Add("Quantity must be greater than zero for every item.");
                continue;
            }

            if (unitCost <= 0)
            {
                errors.Add("Purchase cost must be greater than zero for every item.");
                continue;
            }

            if (line.ExpiryDate.HasValue && line.ExpiryDate.Value.Date < purchaseDate)
            {
                errors.Add("A batch expiry date cannot be earlier than the purchase date.");
                continue;
            }

            items.Add(new PurchaseItem
            {
                ProductId = line.ProductId,
                Quantity = quantity,
                UnitCost = unitCost,
                LineTotal = Money.R2(quantity * unitCost),
                ExpiryDate = line.ExpiryDate?.Date
            });
        }

        if (errors.Count > 0)
        {
            return OperationResult.Fail(errors.Distinct());
        }

        var total = items.Sum(i => i.LineTotal);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var now = AppTime.Now;

            // The purchase and the stock increase are saved together. If anything fails, neither is kept.
            foreach (var item in items)
            {
                var productId = item.ProductId;
                var quantity = item.Quantity;
                var unitCost = item.UnitCost;
                DateTime? expiry = item.ExpiryDate;

                if (expiry.HasValue)
                {
                    await _db.Products
                        .Where(p => p.Id == productId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.CurrentStock, p => p.CurrentStock + quantity)
                            .SetProperty(p => p.PurchaseCost, unitCost)
                            .SetProperty(p => p.ExpiryDate, expiry)
                            .SetProperty(p => p.UpdatedAt, now));
                }
                else
                {
                    await _db.Products
                        .Where(p => p.Id == productId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(p => p.CurrentStock, p => p.CurrentStock + quantity)
                            .SetProperty(p => p.PurchaseCost, unitCost)
                            .SetProperty(p => p.UpdatedAt, now));
                }
            }

            var purchase = new Purchase
            {
                PurchaseNumber = "TMP-" + request.ClientToken.ToString("N"),
                SupplierId = request.SupplierId,
                PurchaseDate = purchaseDate,
                TotalAmount = total,
                Notes = Lookups.CleanText(request.Notes),
                ClientToken = request.ClientToken,
                CreatedAt = now,
                IsDemo = request.IsDemo,
                Items = items
            };

            _db.Purchases.Add(purchase);
            await _db.SaveChangesAsync();

            purchase.PurchaseNumber = $"PUR-{purchase.Id:D5}";
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
            return OperationResult.Ok(purchase.Id);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();

            var raced = await FindExistingAsync(request.ClientToken);
            if (raced.HasValue)
            {
                return OperationResult.Ok(raced.Value, duplicate: true);
            }

            return OperationResult.Fail("The purchase could not be saved. Stock was not changed - please try again.");
        }
    }

    private Task<int?> FindExistingAsync(Guid token) =>
        _db.Purchases.AsNoTracking()
            .Where(p => p.ClientToken == token)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync();
}
