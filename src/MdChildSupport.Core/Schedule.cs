using System.Globalization;

namespace MdChildSupport.Core;

/// <summary>One row of the FL §12-204(e) schedule.</summary>
public sealed record ScheduleRow(int Income, int[] Amounts, bool[] SelfSupportReserve)
{
    /// <summary>True for the first row, "0–1200".</summary>
    public bool IsFirstRow => Income == ChildSupportSchedule.FirstRowTop;
}

/// <summary>Result of a schedule lookup (docs/calculation.md §2.1).</summary>
public sealed record ScheduleLookup(decimal Amount, int? RowIncome, bool SelfSupportReserve, bool Extrapolated)
{
    public static readonly ScheduleLookup None = new(0m, null, false, false);
}

/// <summary>The schedule of basic child support obligations, FL §12-204(e), embedded verbatim.</summary>
public static class ChildSupportSchedule
{
    public const int FirstRowTop = 1200;
    public const int Top = 30000;
    public const int ExtrapolationBase = 15000;
    public const int Step = 50;

    private static readonly Lazy<IReadOnlyList<ScheduleRow>> _rows = new(Load);

    public static IReadOnlyList<ScheduleRow> Rows => _rows.Value;

    /// <summary>Parse schedule text in the statute's layout: "income a1 a2 a3 a4 a5 a6", "*" marks the reserve.</summary>
    public static IReadOnlyList<ScheduleRow> Parse(IEnumerable<string> lines)
    {
        var rows = new List<ScheduleRow>();
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 7) throw new FormatException($"Schedule row must have 7 fields: '{line}'");
            string first = parts[0].Replace('–', '-');
            int income = first.Contains('-') ? int.Parse(first.Split('-')[1], CultureInfo.InvariantCulture)
                                             : int.Parse(first, CultureInfo.InvariantCulture);
            var amounts = new int[6];
            var reserve = new bool[6];
            for (int i = 0; i < 6; i++)
            {
                string cell = parts[i + 1];
                reserve[i] = cell.StartsWith('*');
                amounts[i] = int.Parse(cell.TrimStart('*'), CultureInfo.InvariantCulture);
            }
            rows.Add(new ScheduleRow(income, amounts, reserve));
        }
        return rows;
    }

    private static IReadOnlyList<ScheduleRow> Load()
    {
        using var s = typeof(ChildSupportSchedule).Assembly.GetManifestResourceStream("MdChildSupport.Core.Schedule.schedule.txt")
                      ?? throw new InvalidOperationException("Embedded schedule missing.");
        using var reader = new StreamReader(s);
        var lines = new List<string>();
        while (reader.ReadLine() is string l) lines.Add(l);
        return Parse(lines);
    }

    /// <summary>
    /// Basic obligation for <paramref name="children"/> (1–6; 6 means "6 or more") at a combined (or, for the
    /// multifamily allowance, single-parent) monthly income. Next higher row between rows (§12-204(c)); advisory
    /// extrapolation above $30,000 (§12-204(d), DECISIONS D4).
    /// </summary>
    public static ScheduleLookup Lookup(decimal income, int children)
    {
        if (children <= 0) return ScheduleLookup.None;
        int col = Math.Min(children, 6) - 1;
        var rows = Rows;

        if (income <= Top)
        {
            foreach (var row in rows)
            {
                if (row.Income >= income)
                    return new ScheduleLookup(row.Amounts[col], row.Income, row.SelfSupportReserve[col], false);
            }
        }

        var top = rows[^1];
        var baseRow = rows.First(r => r.Income == ExtrapolationBase);
        decimal roundedUp = Math.Ceiling(income / Step) * Step;
        decimal slope = (decimal)(top.Amounts[col] - baseRow.Amounts[col]) / (Top - ExtrapolationBase);
        decimal amount = Money.Dollars(top.Amounts[col] + (roundedUp - Top) * slope);
        return new ScheduleLookup(amount, null, false, true);
    }

    /// <summary>The slope used for extrapolation, in dollars of obligation per dollar of income (for notes).</summary>
    public static decimal ExtrapolationSlope(int children)
    {
        int col = Math.Min(children, 6) - 1;
        var top = Rows[^1];
        var baseRow = Rows.First(r => r.Income == ExtrapolationBase);
        return (decimal)(top.Amounts[col] - baseRow.Amounts[col]) / (Top - ExtrapolationBase);
    }
}
