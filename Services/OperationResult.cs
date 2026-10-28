namespace StockSense.Services;

/// <summary>Outcome of a business operation (create sale, record payment...). Carries user-friendly errors.</summary>
public class OperationResult
{
    public bool Succeeded { get; set; }

    /// <summary>True when the same form token had already been saved (double click / refresh / retry).</summary>
    public bool WasDuplicate { get; set; }

    public int EntityId { get; set; }

    /// <summary>Optional success message (used by bulk operations such as demo data load / remove).</summary>
    public string? Message { get; set; }

    public List<string> Errors { get; } = new();

    public static OperationResult Ok(int entityId = 0, bool duplicate = false) =>
        new() { Succeeded = true, EntityId = entityId, WasDuplicate = duplicate };

    public static OperationResult Fail(params string[] errors)
    {
        var result = new OperationResult();
        result.Errors.AddRange(errors);
        return result;
    }

    public static OperationResult Fail(IEnumerable<string> errors)
    {
        var result = new OperationResult();
        result.Errors.AddRange(errors);
        return result;
    }
}
