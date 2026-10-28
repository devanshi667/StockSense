using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.Models;
using StockSense.Services;
using StockSense.ViewModels;

namespace StockSense.Controllers;

public class ProductsController : Controller
{
    private const int PageSize = 15;

    private readonly StockSenseDbContext _db;
    private readonly ISettingsService _settings;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(StockSenseDbContext db, ISettingsService settings, ILogger<ProductsController> logger)
    {
        _db = db;
        _settings = settings;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? search, int? categoryId, string filter = "all", string sort = "name", int page = 1)
    {
        var today = AppTime.Today;
        var warningDays = await _settings.GetExpiryWarningDaysAsync();

        var query = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Sku.Contains(term) || p.Category!.Name.Contains(term));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        query = filter switch
        {
            "low" => query.LowStock(),
            "out" => query.OutOfStock(),
            "expiring" => query.ExpiringSoon(today, warningDays),
            "expired" => query.Expired(today),
            _ => query
        };

        IOrderedQueryable<Product> ordered = sort switch
        {
            "name_desc" => query.OrderByDescending(p => p.Name),
            "price" => query.OrderBy(p => p.SellingPrice).ThenBy(p => p.Name),
            "price_desc" => query.OrderByDescending(p => p.SellingPrice).ThenBy(p => p.Name),
            "stock" => query.OrderBy(p => p.CurrentStock).ThenBy(p => p.Name),
            "stock_desc" => query.OrderByDescending(p => p.CurrentStock).ThenBy(p => p.Name),
            "expiry" => query.OrderBy(p => p.ExpiryDate == null).ThenBy(p => p.ExpiryDate).ThenBy(p => p.Name),
            "newest" => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Name),
            _ => query.OrderBy(p => p.Name)
        };

        var rows = ordered.Select(p => new ProductRow
        {
            Id = p.Id,
            Name = p.Name,
            Sku = p.Sku,
            CategoryName = p.Category!.Name,
            Unit = p.Unit,
            PurchaseCost = p.PurchaseCost,
            SellingPrice = p.SellingPrice,
            CurrentStock = p.CurrentStock,
            MinStockLevel = p.MinStockLevel,
            ExpiryDate = p.ExpiryDate,
            IsDemo = p.IsDemo
        });

        var paged = await PagedResult<ProductRow>.CreateAsync(rows, page, PageSize);
        foreach (var row in paged.Items)
        {
            row.ExpiryWarningDays = warningDays;
        }

        var model = new ProductListViewModel
        {
            Search = search,
            CategoryId = categoryId,
            Filter = filter,
            Sort = sort,
            Products = paged,
            Categories = await GetCategoryItemsAsync(),
            ExpiryWarningDays = warningDays
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products.AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return NotFound();

        var warningDays = await _settings.GetExpiryWarningDaysAsync();
        var sales = _db.SaleItems.AsNoTracking().Where(i => i.ProductId == id);

        var hasHistory = await sales.AnyAsync() || await _db.PurchaseItems.AnyAsync(i => i.ProductId == id);

        var model = new ProductDetailsViewModel
        {
            Product = product,
            ExpiryWarningDays = warningDays,
            Alerts = StockRules.BuildAlerts(product, AppTime.Today, warningDays),
            TotalSoldQuantity = await sales.SumAsync(i => (decimal?)i.Quantity) ?? 0m,
            TotalRevenue = await sales.SumAsync(i => (decimal?)(i.LineTotal - i.LineDiscount)) ?? 0m,
            TotalGrossProfit = await sales.SumAsync(i => (decimal?)(i.LineTotal - i.LineDiscount - i.LineCost)) ?? 0m,
            RecentSales = await _db.SaleItems.AsNoTracking()
                .Include(i => i.Sale)
                .Where(i => i.ProductId == id)
                .OrderByDescending(i => i.Sale!.SaleDate).ThenByDescending(i => i.Id)
                .Take(8).ToListAsync(),
            RecentPurchases = await _db.PurchaseItems.AsNoTracking()
                .Include(i => i.Purchase).ThenInclude(p => p!.Supplier)
                .Where(i => i.ProductId == id)
                .OrderByDescending(i => i.Purchase!.PurchaseDate).ThenByDescending(i => i.Id)
                .Take(8).ToListAsync(),
            CanDelete = !hasHistory
        };

        return View(model);
    }

    public async Task<IActionResult> Create()
    {
        var model = new ProductFormViewModel { Categories = await GetCategoryItemsAsync() };
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProductFormViewModel model)
    {
        model.Sku = Lookups.NormalizeSku(model.Sku);
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Unit = model.Unit?.Trim() ?? string.Empty;

        await ValidateProductAsync(model, null);

        if (!ModelState.IsValid)
        {
            model.Categories = await GetCategoryItemsAsync();
            return View("Form", model);
        }

        var now = AppTime.Now;
        var product = new Product
        {
            Name = model.Name,
            Sku = model.Sku,
            CategoryId = model.CategoryId!.Value,
            Unit = model.Unit,
            PurchaseCost = Money.R2(model.PurchaseCost),
            SellingPrice = Money.R2(model.SellingPrice),
            CurrentStock = Money.R3(model.CurrentStock),
            MinStockLevel = Money.R3(model.MinStockLevel),
            ExpiryDate = model.ExpiryDate?.Date,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            _db.Products.Add(product);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not save product {Sku}", model.Sku);
            ModelState.AddModelError(nameof(model.Sku), "This SKU is already used by another product.");
            model.Categories = await GetCategoryItemsAsync();
            return View("Form", model);
        }

        TempData["Success"] = $"Product \"{product.Name}\" was added.";
        if (product.SellingPrice < product.PurchaseCost)
        {
            TempData["Warning"] = "The selling price is lower than the purchase cost - every sale of this product will make a loss.";
        }

        return RedirectToAction(nameof(Details), new { id = product.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        var model = new ProductFormViewModel
        {
            Id = product.Id,
            Name = product.Name,
            Sku = product.Sku,
            CategoryId = product.CategoryId,
            Unit = product.Unit,
            PurchaseCost = product.PurchaseCost,
            SellingPrice = product.SellingPrice,
            CurrentStock = product.CurrentStock,
            DisplayStock = product.CurrentStock,
            MinStockLevel = product.MinStockLevel,
            ExpiryDate = product.ExpiryDate,
            Categories = await GetCategoryItemsAsync()
        };

        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        // Stock is not edited here (it would overwrite sales / purchases made in the meantime).
        ModelState.Remove(nameof(ProductFormViewModel.CurrentStock));

        model.Sku = Lookups.NormalizeSku(model.Sku);
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Unit = model.Unit?.Trim() ?? string.Empty;

        await ValidateProductAsync(model, id);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.DisplayStock = product.CurrentStock;
            model.Categories = await GetCategoryItemsAsync();
            return View("Form", model);
        }

        product.Name = model.Name;
        product.Sku = model.Sku;
        product.CategoryId = model.CategoryId!.Value;
        product.Unit = model.Unit;
        product.PurchaseCost = Money.R2(model.PurchaseCost);
        product.SellingPrice = Money.R2(model.SellingPrice);
        product.MinStockLevel = Money.R3(model.MinStockLevel);
        product.ExpiryDate = model.ExpiryDate?.Date;
        product.UpdatedAt = AppTime.Now;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not update product {Id}", id);
            ModelState.AddModelError(nameof(model.Sku), "This SKU is already used by another product.");
            model.DisplayStock = product.CurrentStock;
            model.Categories = await GetCategoryItemsAsync();
            return View("Form", model);
        }

        TempData["Success"] = $"Product \"{product.Name}\" was updated.";
        if (product.SellingPrice < product.PurchaseCost)
        {
            TempData["Warning"] = "The selling price is lower than the purchase cost - every sale of this product will make a loss.";
        }

        return RedirectToAction(nameof(Details), new { id = product.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        var used = await _db.SaleItems.AnyAsync(i => i.ProductId == id) ||
                   await _db.PurchaseItems.AnyAsync(i => i.ProductId == id);

        if (used)
        {
            TempData["Error"] = $"\"{product.Name}\" cannot be deleted because it appears in sales or purchases. " +
                                "Deleting it would break your history and reports.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not delete product {Id}", id);
            TempData["Error"] = "This product could not be deleted because other records still use it.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = $"Product \"{product.Name}\" was deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Stock-taking correction (damage, theft, miscount). Normal restocking should use Purchases.</summary>
    [HttpPost]
    public async Task<IActionResult> AdjustStock(int id, decimal newStock)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (product == null) return NotFound();

        newStock = Money.R3(newStock);
        if (newStock < 0 || newStock > 999999999m)
        {
            TempData["Error"] = "Stock must be zero or a positive number.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = AppTime.Now;
        await _db.Products.Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.CurrentStock, newStock)
                .SetProperty(p => p.UpdatedAt, now));

        TempData["Success"] = $"Stock of \"{product.Name}\" was corrected from {Fmt.Qty(product.CurrentStock)} to {Fmt.Qty(newStock)} {product.Unit}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task ValidateProductAsync(ProductFormViewModel model, int? existingId)
    {
        if (!string.IsNullOrEmpty(model.Sku))
        {
            var sku = model.Sku;
            var duplicate = await _db.Products.AnyAsync(p => p.Sku == sku && (existingId == null || p.Id != existingId.Value));
            if (duplicate)
            {
                ModelState.AddModelError(nameof(model.Sku), "This SKU is already used by another product.");
            }
        }

        if (model.CategoryId.HasValue && !await _db.Categories.AnyAsync(c => c.Id == model.CategoryId.Value))
        {
            ModelState.AddModelError(nameof(model.CategoryId), "The selected category does not exist.");
        }
    }

    private async Task<List<SelectListItem>> GetCategoryItemsAsync()
    {
        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        return categories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
    }
}
