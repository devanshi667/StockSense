using Microsoft.EntityFrameworkCore;

namespace StockSense.ViewModels;

public interface IPagedInfo
{
    int Page { get; }
    int PageSize { get; }
    int TotalCount { get; }
    int TotalPages { get; }
}

public class PagedResult<T> : IPagedInfo
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public int FirstItemNumber => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int LastItemNumber => Math.Min(TotalCount, Page * PageSize);

    /// <summary>The query must already be ordered, otherwise pages are not stable.</summary>
    public static async Task<PagedResult<T>> CreateAsync(IQueryable<T> orderedQuery, int page, int pageSize)
    {
        pageSize = Math.Max(1, pageSize);
        var total = await orderedQuery.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(Math.Max(1, page), totalPages);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}

/// <summary>Product data sent to the browser to power the sale / purchase entry forms.</summary>
public class ProductOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public decimal Stock { get; set; }
    public string Label => $"{Sku} · {Name}";
}
