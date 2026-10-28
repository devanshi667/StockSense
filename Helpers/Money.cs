namespace StockSense.Helpers;

/// <summary>Rounding rules used by every money / quantity calculation (one place = no drift).</summary>
public static class Money
{
    /// <summary>Rupees and paise: 2 decimal places.</summary>
    public static decimal R2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Quantities (kg, litres...) keep 3 decimal places.</summary>
    public static decimal R3(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
