using System.Globalization;

namespace MdChildSupport.Core;

/// <summary>Rounding and display rules (DECISIONS.md P3; docs/calculation.md §0).</summary>
public static class Money
{
    /// <summary>Typographic minus, as used by the UI's number formatter.</summary>
    public const string Minus = "−";

    /// <summary>R$: whole dollars, half away from zero.</summary>
    public static decimal Dollars(decimal x) => Math.Round(x, 0, MidpointRounding.AwayFromZero);

    /// <summary>R%: one decimal place of a percent, half away from zero.</summary>
    public static decimal Percent(decimal x) => Math.Round(x, 1, MidpointRounding.AwayFromZero);

    /// <summary>"$1,234", "−$300".</summary>
    public static string Format(decimal amount)
    {
        decimal r = Dollars(amount);
        return (r < 0 ? Minus : "") + "$" + Math.Abs(r).ToString("#,0", CultureInfo.InvariantCulture);
    }

    /// <summary>"42.2%".</summary>
    public static string FormatPercent(decimal percent) =>
        (percent < 0 ? Minus : "") + Math.Abs(Percent(percent)).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public static string FormatCount(decimal n) => n.ToString("#,0", CultureInfo.InvariantCulture);
}
