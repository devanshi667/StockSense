using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.Models;
using StockSense.ViewModels;

namespace StockSense.Controllers;

public class SuppliersController : Controller
{
    private const int PageSize = 15;

    private readonly StockSenseDbContext _db;
    private readonly ILogger<SuppliersController> _logger;

    public SuppliersController(StockSenseDbContext db, ILogger<SuppliersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = _db.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.Name.Contains(term) || (s.Phone != null && s.Phone.Contains(term)));
        }

        var rows = query
            .OrderBy(s => s.Name)
            .Select(s => new SupplierRow
            {
                Id = s.Id,
                Name = s.Name,
                Phone = s.Phone,
                Address = s.Address,
                IsDemo = s.IsDemo,
                PurchaseCount = s.Purchases.Count,
                TotalPurchased = s.Purchases.Sum(p => (decimal?)p.TotalAmount) ?? 0m
            });

        var model = new SupplierListViewModel
        {
            Search = search,
            Suppliers = await PagedResult<SupplierRow>.CreateAsync(rows, page, PageSize)
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (supplier == null) return NotFound();

        var purchases = await _db.Purchases.AsNoTracking()
            .Where(p => p.SupplierId == id)
            .OrderByDescending(p => p.PurchaseDate).ThenByDescending(p => p.Id)
            .Take(50)
            .Select(p => new PurchaseRow
            {
                Id = p.Id,
                PurchaseNumber = p.PurchaseNumber,
                PurchaseDate = p.PurchaseDate,
                SupplierName = supplier.Name,
                ItemCount = p.Items.Count,
                TotalAmount = p.TotalAmount,
                IsDemo = p.IsDemo
            })
            .ToListAsync();

        var total = await _db.Purchases.AsNoTracking()
            .Where(p => p.SupplierId == id)
            .SumAsync(p => (decimal?)p.TotalAmount) ?? 0m;

        return View(new SupplierDetailsViewModel { Supplier = supplier, Purchases = purchases, TotalPurchased = total });
    }

    public IActionResult Create()
    {
        return View("Form", new SupplierFormViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(SupplierFormViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        if (!ModelState.IsValid) return View("Form", model);

        var supplier = new Supplier
        {
            Name = model.Name,
            Phone = Lookups.CleanText(model.Phone),
            Address = Lookups.CleanText(model.Address),
            Notes = Lookups.CleanText(model.Notes),
            CreatedAt = AppTime.Now
        };

        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Supplier \"{supplier.Name}\" was added.";
        return RedirectToAction(nameof(Details), new { id = supplier.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (supplier == null) return NotFound();

        return View("Form", new SupplierFormViewModel
        {
            Id = supplier.Id,
            Name = supplier.Name,
            Phone = supplier.Phone,
            Address = supplier.Address,
            Notes = supplier.Notes
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, SupplierFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        model.Name = model.Name?.Trim() ?? string.Empty;
        if (!ModelState.IsValid) return View("Form", model);

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (supplier == null) return NotFound();

        supplier.Name = model.Name;
        supplier.Phone = Lookups.CleanText(model.Phone);
        supplier.Address = Lookups.CleanText(model.Address);
        supplier.Notes = Lookups.CleanText(model.Notes);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Supplier \"{supplier.Name}\" was updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
        if (supplier == null) return NotFound();

        if (await _db.Purchases.AnyAsync(p => p.SupplierId == id))
        {
            TempData["Error"] = $"\"{supplier.Name}\" has recorded purchases and cannot be deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            _db.Suppliers.Remove(supplier);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not delete supplier {Id}", id);
            TempData["Error"] = "This supplier could not be deleted because other records still use it.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = $"Supplier \"{supplier.Name}\" was deleted.";
        return RedirectToAction(nameof(Index));
    }
}
