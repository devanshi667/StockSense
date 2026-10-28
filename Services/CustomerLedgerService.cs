using Microsoft.EntityFrameworkCore;
using StockSense.Data;
using StockSense.Helpers;
using StockSense.Models;

namespace StockSense.Services;

/// <summary>
/// Udhaar balances are never typed in by hand. They are always calculated from saved data:
///   Total credit  = sum over sales of (bill total - amount paid at the time of sale)
///   Total paid    = sum of saved udhaar payments
///   Outstanding   = total credit - total paid
/// </summary>
public class CustomerBalance
{
    public decimal TotalCredit { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Outstanding => TotalCredit - TotalPaid;
}

public class LedgerEntry
{
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;      // "Credit sale" or "Payment"
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CreditGiven { get; set; }               // increases what the customer owes
    public decimal PaymentReceived { get; set; }           // decreases what the customer owes
    public decimal Balance { get; set; }                   // running balance after this entry
    public int? SaleId { get; set; }
    public int Order { get; set; }
}

public class RecordPaymentRequest
{
    public Guid ClientToken { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }
    public bool IsDemo { get; set; }
}

public interface ICustomerLedgerService
{
    Task<Dictionary<int, CustomerBalance>> GetBalancesAsync(IReadOnlyCollection<int>? customerIds = null);
    Task<CustomerBalance> GetBalanceAsync(int customerId);
    Task<decimal> GetTotalOutstandingAsync();
    Task<List<LedgerEntry>> GetLedgerAsync(int customerId);
    Task<OperationResult> RecordPaymentAsync(RecordPaymentRequest request);
}

public class CustomerLedgerService : ICustomerLedgerService
{
    private readonly StockSenseDbContext _db;

    public CustomerLedgerService(StockSenseDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<int, CustomerBalance>> GetBalancesAsync(IReadOnlyCollection<int>? customerIds = null)
    {
        var salesQuery = _db.Sales.AsNoTracking().Where(s => s.CustomerId != null);
        var paymentsQuery = _db.Payments.AsNoTracking().AsQueryable();

        if (customerIds != null)
        {
            var ids = customerIds.ToList();
            salesQuery = salesQuery.Where(s => ids.Contains(s.CustomerId!.Value));
            paymentsQuery = paymentsQuery.Where(p => ids.Contains(p.CustomerId));
        }

        var credits = await salesQuery
            .GroupBy(s => s.CustomerId)
            .Select(g => new { CustomerId = g.Key, Amount = g.Sum(s => s.TotalAmount - s.AmountPaid) })
            .ToListAsync();

        var payments = await paymentsQuery
            .GroupBy(p => p.CustomerId)
            .Select(g => new { CustomerId = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync();

        var result = new Dictionary<int, CustomerBalance>();

        foreach (var credit in credits)
        {
            if (credit.CustomerId.HasValue)
            {
                result[credit.CustomerId.Value] = new CustomerBalance { TotalCredit = credit.Amount };
            }
        }

        foreach (var payment in payments)
        {
            if (!result.TryGetValue(payment.CustomerId, out var balance))
            {
                balance = new CustomerBalance();
                result[payment.CustomerId] = balance;
            }

            balance.TotalPaid = payment.Amount;
        }

        return result;
    }

    public async Task<CustomerBalance> GetBalanceAsync(int customerId)
    {
        var balances = await GetBalancesAsync(new[] { customerId });
        return balances.TryGetValue(customerId, out var balance) ? balance : new CustomerBalance();
    }

    public async Task<decimal> GetTotalOutstandingAsync()
    {
        var credit = await _db.Sales.AsNoTracking()
            .Where(s => s.CustomerId != null)
            .SumAsync(s => (decimal?)(s.TotalAmount - s.AmountPaid)) ?? 0m;

        var paid = await _db.Payments.AsNoTracking()
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        return credit - paid;
    }

    public async Task<List<LedgerEntry>> GetLedgerAsync(int customerId)
    {
        var sales = await _db.Sales.AsNoTracking()
            .Where(s => s.CustomerId == customerId && s.TotalAmount > s.AmountPaid)
            .Select(s => new { s.Id, s.InvoiceNumber, s.SaleDate, s.TotalAmount, s.AmountPaid })
            .ToListAsync();

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .Select(p => new { p.Id, p.PaymentDate, p.Amount, p.PaymentMethod, p.Notes })
            .ToListAsync();

        var entries = new List<LedgerEntry>();

        foreach (var s in sales)
        {
            var description = s.AmountPaid > 0
                ? $"Bill {Fmt.Money(s.TotalAmount)}, {Fmt.Money(s.AmountPaid)} paid at sale"
                : $"Bill {Fmt.Money(s.TotalAmount)} on credit";

            entries.Add(new LedgerEntry
            {
                Date = s.SaleDate,
                Type = "Credit sale",
                Reference = s.InvoiceNumber,
                Description = description,
                CreditGiven = s.TotalAmount - s.AmountPaid,
                SaleId = s.Id,
                Order = s.Id
            });
        }

        foreach (var p in payments)
        {
            var description = "Paid by " + Fmt.Method(p.PaymentMethod);
            if (!string.IsNullOrWhiteSpace(p.Notes))
            {
                description += " - " + p.Notes;
            }

            entries.Add(new LedgerEntry
            {
                Date = p.PaymentDate,
                Type = "Payment",
                Reference = "PAY-" + p.Id.ToString("D5"),
                Description = description,
                PaymentReceived = p.Amount,
                Order = p.Id
            });
        }

        var ordered = entries
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Type)
            .ThenBy(e => e.Order)
            .ToList();

        decimal running = 0m;
        foreach (var entry in ordered)
        {
            running += entry.CreditGiven - entry.PaymentReceived;
            entry.Balance = running;
        }

        return ordered;
    }

    public async Task<OperationResult> RecordPaymentAsync(RecordPaymentRequest request)
    {
        if (request.ClientToken == Guid.Empty)
        {
            return OperationResult.Fail("This form is no longer valid. Please reload the page and try again.");
        }

        var existingId = await FindExistingPaymentAsync(request.ClientToken);
        if (existingId.HasValue)
        {
            return OperationResult.Ok(existingId.Value, duplicate: true);
        }

        var amount = Money.R2(request.Amount);
        if (amount <= 0)
        {
            return OperationResult.Fail("Payment amount must be greater than zero.");
        }

        if (request.PaymentDate.Date > AppTime.Today)
        {
            return OperationResult.Fail("Payment date cannot be in the future.");
        }

        if (!await _db.Customers.AnyAsync(c => c.Id == request.CustomerId))
        {
            return OperationResult.Fail("Customer not found.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Lock this customer's row first. Two payments for the same customer are then processed one after
            // the other, so the "cannot pay more than outstanding" check below is always accurate.
            var customerId = request.CustomerId;
            await _db.Customers
                .Where(c => c.Id == customerId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Name, c => c.Name));

            var balance = await GetBalanceAsync(customerId);

            if (balance.Outstanding <= 0)
            {
                await transaction.RollbackAsync();
                return OperationResult.Fail("This customer has no outstanding balance.");
            }

            if (amount > balance.Outstanding)
            {
                await transaction.RollbackAsync();
                return OperationResult.Fail(
                    $"Payment of {Fmt.Money(amount)} is more than the outstanding balance of {Fmt.Money(balance.Outstanding)}.");
            }

            var payment = new Payment
            {
                CustomerId = customerId,
                Amount = amount,
                PaymentDate = request.PaymentDate,
                PaymentMethod = request.Method,
                Notes = Lookups.CleanText(request.Notes),
                ClientToken = request.ClientToken,
                CreatedAt = AppTime.Now,
                IsDemo = request.IsDemo
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return OperationResult.Ok(payment.Id);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();

            var raced = await FindExistingPaymentAsync(request.ClientToken);
            if (raced.HasValue)
            {
                return OperationResult.Ok(raced.Value, duplicate: true);
            }

            return OperationResult.Fail("The payment could not be saved. Please try again.");
        }
    }

    private Task<int?> FindExistingPaymentAsync(Guid token) =>
        _db.Payments.AsNoTracking()
            .Where(p => p.ClientToken == token)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync();
}
