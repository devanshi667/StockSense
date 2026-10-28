namespace StockSense.Models;

/// <summary>How money was handed over (for the part paid immediately, or for a udhaar payment).</summary>
public enum PaymentMethod
{
    Cash = 1,
    Upi = 2,
    Card = 3,
    Other = 4
}
