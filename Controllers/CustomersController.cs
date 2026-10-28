using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.Models;
using StockSense.Services;
using StockSense.ViewModels;

namespace StockSense.Controllers;

public class CustomersController : Controller
{
    private const int PageSize = 15;

    private readonly StockSenseDbContext _db;
    private readonly ICustomerLedgerService _ledger;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(StockSenseDbContext db, ICustomerLedgerService ledger, ILogger<CustomersController> logger)
    {
        _db = db;
        _ledger = ledger;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? search, bool onlyOutstanding = false, int page = 1)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.Mobile.Contains(term));
        }

        var customers = await query.OrderBy(c => c.Name).ToListAsync();

        // Balances are always calculated from saved sales and payments (never typed in).
        var balances = await _ledger.GetBalancesAsync();

        var rows = customers.Select(c =>
        {
            balances.TryGetValue(c.Id, out var balance);
            return new CustomerRow
            {
                Id = c.Id,
                Name = c.Name,
                Mobile = c.Mobile,
                Address = c.Address,
                IsDemo = c.IsDemo,
                TotalCredit = balance?.TotalCredit ?? 0m,
                TotalPaid = balance?.TotalPaid ?? 0m
            };
        }).ToList();

        if (onlyOutstanding)
        {
            rows = rows.Where(r => r.Outstanding > 0)
                       .OrderByDescending(r => r.Outstanding)
                       .ThenBy(r => r.Name)
                       .ToList();
        }

        var total = rows.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Min(Math.Max(1, page), totalPages);

        var paged = new PagedResult<CustomerRow>
        {
            Items = rows.Skip((page - 1) * PageSize).Take(PageSize).ToList(),
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        };

        var model = new CustomerListViewModel
        {
            Search = search,
            OnlyOutstanding = onlyOutstanding,
            Customers = paged,
            TotalOutstanding = balances.Values.Sum(b => b.Outstanding),
            CustomersWithDues = balances.Values.Count(b => b.Outstanding > 0)
        };

        return View(model);
    }

    public async Task<IActionResult> Ledger(int id)
    {
        var model = await BuildLedgerAsync(id, new PaymentInput { ClientToken = Guid.NewGuid() });
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> AddPayment(int id, [Bind(Prefix = "Payment")] PaymentInput input)
    {
        var exists = await _db.Customers.AnyAsync(c => c.Id == id);
        if (!exists) return NotFound();

        if (ModelState.IsValid)
        {
            var result = await _ledger.RecordPaymentAsync(new RecordPaymentRequest
            {
                ClientToken = input.ClientToken,
                CustomerId = id,
                Amount = input.Amount,
                Method = input.Method,
                PaymentDate = input.PaymentDate,
                Notes = input.Notes
            });

            if (result.Succeeded)
            {
                TempData["Success"] = result.WasDuplicate
                    ? "This payment was already recorded - it was not saved twice."
                    : $"Payment of {Fmt.Money(Money.R2(input.Amount))} recorded.";
                return RedirectToAction(nameof(Ledger), new { id });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("Payment.Amount", error);
            }
        }

        var model = await BuildLedgerAsync(id, input);
        if (model == null) return NotFound();

        ViewData["OpenPayment"] = true;
        return View("Ledger", model);
    }

    public IActionResult Create()
    {
        return View("Form", new CustomerFormViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        Normalize(model);
        await ValidateMobileAsync(model, null);
        if (!ModelState.IsValid) return View("Form", model);

        var customer = new Customer
        {
            Name = model.Name,
            Mobile = model.Mobile,
            Address = Lookups.CleanText(model.Address),
            CreatedAt = AppTime.Now
        };

        try
        {
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not save customer {Mobile}", model.Mobile);
            ModelState.AddModelError(nameof(model.Mobile), "A customer with this mobile number already exists.");
            return View("Form", model);
        }

        TempData["Success"] = $"Customer \"{customer.Name}\" was added.";
        return RedirectToAction(nameof(Ledger), new { id = customer.Id });
    }

    /// <summary>Used by the "New customer" pop-up on the sale form. Returns JSON.</summary>
    [HttpPost]
    public async Task<IActionResult> QuickCreate(CustomerFormViewModel model)
    {
        Normalize(model);
        await ValidateMobileAsync(model, null);

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Distinct()
                .ToList();
            return BadRequest(new { errors });
        }

        var customer = new Customer
        {
            Name = model.Name,
            Mobile = model.Mobile,
            Address = Lookups.CleanText(model.Address),
            CreatedAt = AppTime.Now
        };

        try
        {
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not quick-save customer {Mobile}", model.Mobile);
            return BadRequest(new { errors = new[] { "A customer with this mobile number already exists." } });
        }

        return Json(new { id = customer.Id, label = $"{customer.Name} ({customer.Mobile})" });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();

        return View("Form", new CustomerFormViewModel
        {
            Id = customer.Id,
            Name = customer.Name,
            Mobile = customer.Mobile,
            Address = customer.Address
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model)
    {
        if (id != model.Id) return BadRequest();

        Normalize(model);
        await ValidateMobileAsync(model, id);
        if (!ModelState.IsValid) return View("Form", model);

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();

        customer.Name = model.Name;
        customer.Mobile = model.Mobile;
        customer.Address = Lookups.CleanText(model.Address);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not update customer {Id}", id);
            ModelState.AddModelError(nameof(model.Mobile), "A customer with this mobile number already exists.");
            return View("Form", model);
        }

        TempData["Success"] = $"Customer \"{customer.Name}\" was updated.";
        return RedirectToAction(nameof(Ledger), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();

        var hasHistory = await _db.Sales.AnyAsync(s => s.CustomerId == id) ||
                         await _db.Payments.AnyAsync(p => p.CustomerId == id);

        if (hasHistory)
        {
            TempData["Error"] = $"\"{customer.Name}\" has sales or payments on record and cannot be deleted " +
                                "(it would break the ledger and reports).";
            return RedirectToAction(nameof(Ledger), new { id });
        }

        try
        {
            _db.Customers.Remove(customer);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Could not delete customer {Id}", id);
            TempData["Error"] = "This customer could not be deleted because other records still use it.";
            return RedirectToAction(nameof(Ledger), new { id });
        }

        TempData["Success"] = $"Customer \"{customer.Name}\" was deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static void Normalize(CustomerFormViewModel model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Mobile = model.Mobile?.Trim() ?? string.Empty;
    }

    private async Task ValidateMobileAsync(CustomerFormViewModel model, int? existingId)
    {
        if (string.IsNullOrEmpty(model.Mobile)) return;

        var mobile = model.Mobile;
        var duplicate = await _db.Customers.AnyAsync(c => c.Mobile == mobile && (existingId == null || c.Id != existingId.Value));
        if (duplicate)
        {
            ModelState.AddModelError(nameof(model.Mobile), "A customer with this mobile number already exists.");
        }
    }

    private async Task<CustomerLedgerViewModel?> BuildLedgerAsync(int id, PaymentInput payment)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return null;

        return new CustomerLedgerViewModel
        {
            Customer = customer,
            Balance = await _ledger.GetBalanceAsync(id),
            Entries = await _ledger.GetLedgerAsync(id),
            Payment = payment,
            PaymentMethods = Lookups.PaymentMethods()
        };
    }
}
