using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.Models;
using StockSense.Services;
using StockSense.ViewModels;

namespace StockSense.Controllers;

public class PurchasesController : Controller
{
    private const int PageSize = 15;

    private readonly StockSenseDbContext _db;
    private readonly IPurchaseService _purchases;

    public PurchasesController(StockSenseDbContext db, IPurchaseService purchases)
    {
        _db = db;
        _purchases = purchases;
    }

    public async Task<IActionResult> Index(string? search, int? supplierId, DateTime? from, DateTime? to, int page = 1)
    {
        var query = _db.Purchases.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.PurchaseNumber.Contains(term) || p.Supplier!.Name.Contains(term) ||
                                     p.Items.Any(i => i.Product!.Name.Contains(term)));
        }

        if (supplierId.HasValue) query = query.Where(p => p.SupplierId == supplierId.Value);
        if (from.HasValue) query = query.Where(p => p.PurchaseDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(p => p.PurchaseDate <= to.Value.Date);

        var rangeTotal = await query.SumAsync(p => (decimal?)p.TotalAmount) ?? 0m;

        var rows = query
            .OrderByDescending(p => p.PurchaseDate).ThenByDescending(p => p.Id)
            .Select(p => new PurchaseRow
            {
                Id = p.Id,
                PurchaseNumber = p.PurchaseNumber,
                PurchaseDate = p.PurchaseDate,
                SupplierName = p.Supplier!.Name,
                ItemCount = p.Items.Count,
                TotalAmount = p.TotalAmount,
                IsDemo = p.IsDemo
            });

        var paged = await PagedResult<PurchaseRow>.CreateAsync(rows, page, PageSize);

        var model = new PurchaseListViewModel
        {
            Search = search,
            SupplierId = supplierId,
            From = from,
            To = to,
            Purchases = paged,
            Suppliers = await GetSupplierItemsAsync(),
            PageTotal = paged.Items.Sum(p => p.TotalAmount),
            RangeTotal = rangeTotal
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var purchase = await _db.Purchases.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (purchase == null) return NotFound();
        return View(purchase);
    }

    public async Task<IActionResult> Create(int? productId)
    {
        var model = await BuildFormAsync(new PurchaseCreateViewModel
        {
            ClientToken = Guid.NewGuid(),
            PurchaseDate = AppTime.Today
        });

        if (productId.HasValue && model.Products.Any(p => p.Id == productId.Value))
        {
            var product = model.Products.First(p => p.Id == productId.Value);
            model.Lines.Add(new PurchaseLineInput { ProductId = product.Id, Quantity = 1, UnitCost = product.Cost });
        }

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(PurchaseCreateViewModel model)
    {
        // Rows the user left empty are ignored; anything else is validated.
        model.Lines = (model.Lines ?? new()).Where(l => l.ProductId.HasValue).ToList();

        // Lines are validated by the service; clear per-field errors on removed/blank rows.
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Lines[")).ToList())
        {
            var index = ParseIndex(key);
            if (index >= model.Lines.Count) ModelState.Remove(key);
        }

        if (model.Lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one product to the purchase.");
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildFormAsync(model));
        }

        var request = new CreatePurchaseRequest
        {
            ClientToken = model.ClientToken,
            SupplierId = model.SupplierId!.Value,
            PurchaseDate = model.PurchaseDate,
            Notes = model.Notes,
            Lines = model.Lines.Select(l => new PurchaseLineRequest
            {
                ProductId = l.ProductId!.Value,
                Quantity = l.Quantity,
                UnitCost = l.UnitCost,
                ExpiryDate = l.ExpiryDate
            }).ToList()
        };

        var result = await _purchases.CreateAsync(request);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            return View(await BuildFormAsync(model));
        }

        TempData["Success"] = result.WasDuplicate
            ? "This purchase was already saved - it was not recorded twice."
            : "Purchase saved and stock updated.";

        return RedirectToAction(nameof(Details), new { id = result.EntityId });
    }

    private static int ParseIndex(string key)
    {
        var start = key.IndexOf('[') + 1;
        var end = key.IndexOf(']');
        return start > 0 && end > start && int.TryParse(key[start..end], out var i) ? i : 0;
    }

    private async Task<PurchaseCreateViewModel> BuildFormAsync(PurchaseCreateViewModel model)
    {
        model.Suppliers = await GetSupplierItemsAsync();
        model.Products = await _db.Products.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProductOption
            {
                Id = p.Id, Name = p.Name, Sku = p.Sku, Unit = p.Unit,
                Price = p.SellingPrice, Cost = p.PurchaseCost, Stock = p.CurrentStock
            })
            .ToListAsync();
        return model;
    }

    private async Task<List<SelectListItem>> GetSupplierItemsAsync()
    {
        var suppliers = await _db.Suppliers.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();

        return suppliers.Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToList();
    }
}
