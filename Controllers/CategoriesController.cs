using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Models;
using StockSense.ViewModels;

namespace StockSense.Controllers;

public class CategoriesController : Controller
{
    private readonly StockSenseDbContext _db;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(StockSenseDbContext db, ILogger<CategoriesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        return View(await BuildListAsync(new CategoryFormViewModel()));
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "NewCategory")] CategoryFormViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;

        if (ModelState.IsValid && await _db.Categories.AnyAsync(c => c.Name == model.Name))
        {
            ModelState.AddModelError("NewCategory.Name", "A category with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildListAsync(model));
        }

        try
        {
            _db.Categories.Add(new Category { Name = model.Name });
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not save category {Name}", model.Name);
            ModelState.AddModelError("NewCategory.Name", "A category with this name already exists.");
            return View("Index", await BuildListAsync(model));
        }

        TempData["Success"] = $"Category \"{model.Name}\" was added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var category = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();

        return View(new CategoryFormViewModel { Id = category.Id, Name = category.Name });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model)
    {
        if (id != model.Id) return BadRequest();
        model.Name = model.Name?.Trim() ?? string.Empty;

        if (ModelState.IsValid && await _db.Categories.AnyAsync(c => c.Name == model.Name && c.Id != id))
        {
            ModelState.AddModelError(nameof(model.Name), "A category with this name already exists.");
        }

        if (!ModelState.IsValid) return View(model);

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();

        category.Name = model.Name;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not rename category {Id}", id);
            ModelState.AddModelError(nameof(model.Name), "A category with this name already exists.");
            return View(model);
        }

        TempData["Success"] = "Category was renamed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound();

        if (await _db.Products.AnyAsync(p => p.CategoryId == id))
        {
            TempData["Error"] = $"Category \"{category.Name}\" still has products. Move or delete those products first.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not delete category {Id}", id);
            TempData["Error"] = "This category could not be deleted because other records still use it.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"Category \"{category.Name}\" was deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<CategoryListViewModel> BuildListAsync(CategoryFormViewModel newCategory)
    {
        var rows = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryRow
            {
                Id = c.Id,
                Name = c.Name,
                ProductCount = c.Products.Count,
                IsDemo = c.IsDemo
            })
            .ToListAsync();

        return new CategoryListViewModel { Categories = rows, NewCategory = newCategory };
    }
}
