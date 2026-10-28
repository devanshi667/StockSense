using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.Models;
using StockSense.Services;
using StockSense.ViewModels;

namespace StockSense.Controllers;

public class SalesController : Controller
{
    private const int PageSize = 15;

    private readonly StockSenseDbContext _db;
    private readonly ISaleService _sales;

    public SalesController(StockSenseDbContext db, ISaleService sales)
    {
        _db = db;
        _sales = sales;
    }

    public async Task<IActionResult> Index(string? search, DateTime? from, DateTime? to, string status = "all", int page = 1)
    {
        var query = _db.Sales.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.InvoiceNumber.Contains(term) ||
                                     (s.Customer != null && (s.Customer.Name.Contains(term) || s.Customer.Mobile.Contains(term))) ||
                                     s.Items.Any(i => i.Product!.Name.Contains(term)));
        }

        if (from.HasValue) query = query.Where(s => s.SaleDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(s => s.SaleDate < to.Value.Date.AddDays(1));

        query = status switch
        {
            "credit" => query.Where(s => s.AmountPaid < s.TotalAmount),
            "paid" => query.Where(s => s.AmountPaid >= s.TotalAmount),
            _ => query
        };

        var rangeTotal = await query.SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;
        var rangeCredit = await query.SumAsync(s => (decimal?)(s.TotalAmount - s.AmountPaid)) ?? 0m;

        var rows = query
            .OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.Id)
            .Select(s => new SaleRow
            {
                Id = s.Id,
                InvoiceNumber = s.InvoiceNumber,
                SaleDate = s.SaleDate,
                CustomerName = s.Customer != null ? s.Customer.Name : null,
                ItemCount = s.Items.Count,
                TotalAmount = s.TotalAmount,
                AmountPaid = s.AmountPaid,
                PaymentMethod = s.PaymentMethod,
                IsDemo = s.IsDemo
            });

        var model = new SaleListViewModel
        {
            Search = search,
            From = from,
            To = to,
            Status = status,
            Sales = await PagedResult<SaleRow>.CreateAsync(rows, page, PageSize),
            RangeTotal = rangeTotal,
            RangeCredit = rangeCredit
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var sale = await _db.Sales.AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale == null) return NotFound();
        return View(sale);
    }

    public async Task<IActionResult> Create(int? productId, int? customerId)
    {
        var model = await BuildFormAsync(new SaleCreateViewModel { ClientToken = Guid.NewGuid(), CustomerId = customerId });

        if (productId.HasValue && model.Products.Any(p => p.Id == productId.Value))
        {
            model.Lines.Add(new SaleLineInput { ProductId = productId.Value, Quantity = 1 });
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaleCreateViewModel model)
    {
        model.Lines = (model.Lines ?? new()).Where(l => l.ProductId.HasValue).ToList();

        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Lines[")).ToList())
        {
            if (ParseIndex(key) >= model.Lines.Count) ModelState.Remove(key);
        }

        if (model.Lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one product to the sale.");
        }

        if (model.OnCredit && !model.CustomerId.HasValue)
        {
            ModelState.AddModelError(nameof(model.CustomerId), "Select a customer for a credit (udhaar) sale.");
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildFormAsync(model));
        }

        var request = new CreateSaleRequest
        {
            ClientToken = model.ClientToken,
            SaleDate = AppTime.Now,
            CustomerId = model.CustomerId,
            OnCredit = model.OnCredit,
            AmountPaidNow = model.OnCredit ? model.AmountPaidNow : 0m,
            PaymentMethod = model.PaymentMethod,
            Discount = model.Discount,
            Notes = model.Notes,
            Lines = model.Lines.Select(l => new SaleLineRequest { ProductId = l.ProductId!.Value, Quantity = l.Quantity }).ToList()
        };

        var result = await _sales.CreateAsync(request);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            return View(await BuildFormAsync(model));
        }

        TempData["Success"] = result.WasDuplicate
            ? "This sale was already saved - it was not recorded twice."
            : "Sale completed and stock updated.";

        return RedirectToAction(nameof(Details), new { id = result.EntityId });
    }

    private static int ParseIndex(string key)
    {
        var start = key.IndexOf('[') + 1;
        var end = key.IndexOf(']');
        return start > 0 && end > start && int.TryParse(key[start..end], out var i) ? i : 0;
    }

    private async Task<SaleCreateViewModel> BuildFormAsync(SaleCreateViewModel model)
    {
        var today = AppTime.Today;

        // Only products that can actually be sold right now (in stock and not expired) are offered.
        model.Products = await _db.Products.AsNoTracking()
            .Where(p => p.CurrentStock > 0 && (p.ExpiryDate == null || p.ExpiryDate >= today))
            .OrderBy(p => p.Name)
            .Select(p => new ProductOption
            {
                Id = p.Id, Name = p.Name, Sku = p.Sku, Unit = p.Unit,
                Price = p.SellingPrice, Cost = p.PurchaseCost, Stock = p.CurrentStock
            })
            .ToListAsync();

        var customers = await _db.Customers.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Mobile })
            .ToListAsync();

        model.Customers = customers.Select(c => new SelectListItem($"{c.Name} ({c.Mobile})", c.Id.ToString())).ToList();
        model.PaymentMethods = Lookups.PaymentMethods();
        return model;
    }
}
