namespace StockSense.Helpers;

/// <summary>
/// Single place for "what time is it". The shop's local time is used everywhere
/// (sales, payments, reports) so "today" matches what the shopkeeper sees on the wall clock.
/// </summary>
public static class AppTime
{
    public static DateTime Now => DateTime.Now;
    public static DateTime Today => DateTime.Today;
}
