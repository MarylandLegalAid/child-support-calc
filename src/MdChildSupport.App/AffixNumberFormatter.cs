using System.Globalization;
using Windows.Globalization.NumberFormatting;

namespace MdChildSupport.App;

/// <summary>
/// NumberBox formatter that shows a grouped, fixed-precision number with an optional prefix/suffix
/// ("$1,111", "5.0%") and parses the same text back, with or without the affixes and group separators.
/// Parsed values are rounded to <see cref="FractionDigits"/> so what is shown is what is stored.
/// </summary>
public sealed partial class AffixNumberFormatter : INumberFormatter2, INumberParser
{
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public int FractionDigits { get; set; }

    private string Pattern => FractionDigits > 0 ? "#,0." + new string('0', FractionDigits) : "#,0";

    private string Format(double value)
    {
        double rounded = Math.Round(value, FractionDigits, MidpointRounding.AwayFromZero);
        string digits = Math.Abs(rounded).ToString(Pattern, CultureInfo.InvariantCulture);
        return (rounded < 0 ? "−" : "") + Prefix + digits + Suffix; // typographic minus, matches the result text
    }

    public string FormatDouble(double value) => Format(value);
    public string FormatInt(long value) => Format(value);
    public string FormatUInt(ulong value) => Format(value);

    public double? ParseDouble(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string s = text.Trim().Replace('−', '-'); // accept a typographic minus
        if (Prefix.Length > 0) s = s.Replace(Prefix, "");
        if (Suffix.Length > 0) s = s.Replace(Suffix, "");
        s = s.Replace(",", "").Replace(" ", "");
        if (!double.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double v))
            return null;
        return Math.Round(v, FractionDigits, MidpointRounding.AwayFromZero);
    }

    public long? ParseInt(string text) => ParseDouble(text) is double v ? (long)Math.Round(v, MidpointRounding.AwayFromZero) : null;

    public ulong? ParseUInt(string text) => ParseDouble(text) is double v && v >= 0 ? (ulong)Math.Round(v, MidpointRounding.AwayFromZero) : null;
}
