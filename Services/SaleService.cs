using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.Models;

namespace StockSense.Services;

public class SaleLineRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}

public class CreateSaleRequest
{
    /// <summary>Token generated when the sale form was opened. Used to ignore duplicate submissions.</summary>
    public Guid ClientToken { get; set; }

    public DateTime SaleDate { get; set; }
    public int? CustomerId { get; set; }

    /// <summary>False = fully paid now. True = credit (udhaar) sale; AmountPaidNow may be 0 or a part payment.</summary>
    public bool OnCredit { get; set; }
    public decimal AmountPaidNow { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal Discount { get; set; }
    public string? Notes { get; set; }
    public List<SaleLineRequest> Lines { get; set; } = new();
    public bool IsDemo { get; set; }
}

public interface ISaleService
{
    Task<OperationResult> CreateAsync(CreateSaleRequest request);
}

public class SaleService : ISaleService
{
    private readonly StockSenseDbContext _db;

    public SaleService(StockSenseDbContext db)
    {
        _db = db;
    }

    public async Task<OperationResult> CreateAsync(CreateSaleRequest request)
    {
        if (request.ClientToken == Guid.Empty)
        {
            return OperationResult.Fail("This form is no longer valid. Please reload the page and try again.");
        }

        // Duplicate submission (double click, refresh, back button): return the sale that was already saved.
        var existingId = await FindExistingAsync(request.ClientToken);
        if (existingId.HasValue)
        {
            return OperationResult.Ok(existingId.Value, duplicate: true);
        }

        if (request.Lines == null || request.Lines.Count == 0)
        {
            return OperationResult.Fail("Add at least one product to the sale.");
        }

        // Same product entered twice? Merge into one line.
        var merged = request.Lines
            .GroupBy(l => l.ProductId)
            .Select(g => new SaleLineRequest { ProductId = g.Key, Quantity = Money.R3(g.Sum(x => x.Quantity)) })
            .ToList();

        if (merged.Any(l => l.Quantity <= 0))
        {
            return OperationResult.Fail("Quantity must be greater than zero for every item.");
        }

        var ids = merged.Select(m => m.ProductId).ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var saleDay = request.SaleDate.Date;
        var errors = new List<string>();
        var items = new List<SaleItem>();

        foreach (var line in merged)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                errors.Add("One of the selected products no longer exists.");
                continue;
            }

            if (product.ExpiryDate.HasValue && product.ExpiryDate.Value.Date < saleDay)
            {
                errors.Add($"{product.Name} expired on {Fmt.Date(product.ExpiryDate.Value)} and cannot be sold.");
                continue;
            }

            if (product.CurrentStock < line.Quantity)
            {
                errors.Add($"Insufficient stock for {product.Name}: {Fmt.Qty(product.CurrentStock)} {product.Unit} available, " +
                           $"{Fmt.Qty(line.Quantity)} requested.");
                continue;
            }

            items.Add(new SaleItem
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitPrice = product.SellingPrice,   // price and cost are frozen on the sale line
                UnitCost = product.PurchaseCost,
                LineTotal = Money.R2(line.Quantity * product.SellingPrice),
                LineCost = Money.R2(line.Quantity * product.PurchaseCost)
            });
        }

        if (errors.Count > 0)
        {
            return OperationResult.Fail(errors);
        }

        var subTotal = items.Sum(i => i.LineTotal);
        if (subTotal <= 0)
        {
            return OperationResult.Fail("The sale total must be greater than zero.");
        }

        var discount = Money.R2(request.Discount);
        if (discount < 0)
        {
            return OperationResult.Fail("Discount cannot be negative.");
        }

        if (discount >= subTotal)
        {
            return OperationResult.Fail("Discount must be less than the subtotal.");
        }

        AllocateDiscount(items, discount, subTotal);
        var total = subTotal - discount;

        decimal amountPaid;
        if (request.OnCredit)
        {
            if (!request.CustomerId.HasValue)
            {
                return OperationResult.Fail("Select a customer for a credit (udhaar) sale.");
            }

            amountPaid = Money.R2(request.AmountPaidNow);
            if (amountPaid < 0)
            {
                return OperationResult.Fail("Amount paid now cannot be negative.");
            }

            if (amountPaid >= total)
            {
                return OperationResult.Fail("For a credit sale the amount paid now must be less than the bill total. " +
                                            "Choose \"Paid in full\" if the customer pays everything now.");
            }
        }
        else
        {
            amountPaid = total;
        }

        if (request.CustomerId.HasValue &&
            !await _db.Customers.AnyAsync(c => c.Id == request.CustomerId.Value))
        {
            return OperationResult.Fail("The selected customer no longer exists.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var now = AppTime.Now;

            // Stock is reduced with one atomic UPDATE per product. The WHERE clause re-checks stock and expiry,
            // so two cashiers selling the last item at the same time cannot push stock below zero.
            foreach (var item in items)
            {
                var productId = item.ProductId;
                var quantity = item.Quantity;

                var affected = await _db.Products
                    .Where(p => p.Id == productId &&
                                p.CurrentStock >= quantity &&
                                (p.ExpiryDate == null || p.ExpiryDate >= saleDay))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.CurrentStock, p => p.CurrentStock - quantity)
                        .SetProperty(p => p.UpdatedAt, now));

                if (affected == 0)
                {
                    await transaction.RollbackAsync();
                    return OperationResult.Fail(
                        "Stock changed while the sale was being saved. Nothing was recorded - please review the items and try again.");
                }
            }

            var sale = new Sale
            {
                InvoiceNumber = "TMP-" + request.ClientToken.ToString("N"),
                SaleDate = request.SaleDate,
                CustomerId = request.CustomerId,
                SubTotal = subTotal,
                DiscountAmount = discount,
                TotalAmount = total,
                AmountPaid = amountPaid,
                PaymentMethod = amountPaid > 0 ? request.PaymentMethod : PaymentMethod.Cash,
                Notes = Lookups.CleanText(request.Notes),
                ClientToken = request.ClientToken,
                CreatedAt = now,
                IsDemo = request.IsDemo,
                Items = items
            };

            _db.Sales.Add(sale);
            await _db.SaveChangesAsync();

            sale.InvoiceNumber = $"INV-{sale.Id:D5}";
            await _db.SaveChangesAsync();

            await transaction.CommitAsync();
            return OperationResult.Ok(sale.Id);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();

            // A parallel request with the same token may have won the race.
            var raced = await FindExistingAsync(request.ClientToken);
            if (raced.HasValue)
            {
                return OperationResult.Ok(raced.Value, duplicate: true);
            }

            return OperationResult.Fail("The sale could not be saved. Nothing was recorded - please try again.");
        }
    }

    private Task<int?> FindExistingAsync(Guid token) =>
        _db.Sales.AsNoTracking()
            .Where(s => s.ClientToken == token)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Splits the bill discount across lines in proportion to line value. The last line takes the rounding
    /// remainder so the shares add up to exactly the discount (product-wise profit stays consistent with revenue).
    /// </summary>
    private static void AllocateDiscount(List<SaleItem> items, decimal discount, decimal subTotal)
    {
        decimal allocated = 0m;
        for (var i = 0; i < items.Count; i++)
        {
            decimal share;
            if (i == items.Count - 1)
            {
                share = discount - allocated;
            }
            else
            {
                share = Money.R2(discount * items[i].LineTotal / subTotal);
            }

            items[i].LineDiscount = share;
            allocated += share;
        }
    }
}
