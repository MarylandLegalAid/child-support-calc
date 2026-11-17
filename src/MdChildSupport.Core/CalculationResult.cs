namespace MdChildSupport.Core;

public enum WorksheetKind { A, B }

public enum LineFormat { Money, Percent, Count }

/// <summary>One numbered line of the worksheet, in the form's order. Null values print as blank.</summary>
public sealed class WorksheetLine
{
    public required string Number { get; init; }
    public required string Caption { get; init; }
    public decimal? Parent1 { get; init; }
    public decimal? Parent2 { get; init; }
    public decimal? Combined { get; init; }
    public LineFormat Format { get; init; } = LineFormat.Money;

    /// <summary>The form's operator mark for lines 1a–1d and 4a–4e ("−", "+", "+/−").</summary>
    public string? Operator { get; init; }

    /// <summary>True for main numbered lines (printed bold); false for lettered sub-lines.</summary>
    public bool IsMain { get; init; } = true;

    /// <summary>Optional marker printed after a value (e.g. "*" for a value explained in the notes).</summary>
    public string? Parent1Marker { get; init; }
    public string? Parent2Marker { get; init; }
    public string? CombinedMarker { get; init; }

    public decimal? Value(int column) => column switch { 0 => Parent1, 1 => Parent2, _ => Combined };

    public string? Marker(int column) => column switch { 0 => Parent1Marker, 1 => Parent2Marker, _ => CombinedMarker };

    public string FormatValue(int column) => Value(column) is decimal v ? FormatNumber(v, Format) : "";

    public static string FormatNumber(decimal v, LineFormat format) => format switch
    {
        LineFormat.Percent => Money.FormatPercent(v),
        LineFormat.Count => Money.FormatCount(v),
        _ => Money.Format(v),
    };
}

public sealed record Warning(string Code, string Text);

/// <summary>Everything the engine computes for one case.</summary>
public sealed class CalculationResult
{
    public WorksheetKind Kind { get; init; }
    public GuidelinesVersion Guidelines { get; init; }
    public CustodyType Custody { get; init; }
    public int ChildCount { get; init; }

    public string Parent1Name { get; init; } = "Mother";
    public string Parent2Name { get; init; } = "Father";

    public decimal[] ActualIncome { get; init; } = new decimal[2];
    public decimal[] AdjustedIncome { get; init; } = new decimal[2];
    public decimal[] CountedAdjustedIncome { get; init; } = new decimal[2];
    public decimal[] SharePercent { get; init; } = new decimal[2];
    public decimal[] CashMedicalDollars { get; init; } = new decimal[2];
    public decimal CombinedActualIncome { get; init; }
    public decimal CombinedAdjustedIncome { get; init; }
    public ScheduleLookup Basic { get; init; } = ScheduleLookup.None;

    /// <summary>Overnights actually used (Worksheet B only).</summary>
    public int Parent1Overnights { get; init; }
    public int Parent2Overnights { get; init; }
    /// <summary>Caution shown when the entered overnights were outside 92–273 (DECISIONS D7).</summary>
    public string? OvernightsNote { get; init; }

    /// <summary>Who owes support (or, for a Worksheet A reimbursement, who reimburses). Null if nobody.</summary>
    public Parent? Payer { get; init; }
    public decimal Amount { get; init; }
    public bool IsReimbursement { get; init; }
    public bool IsAdvisory => Basic.Extrapolated;
    public bool SharedLimitApplied { get; init; }
    /// <summary>When the FL §12-204(m)(5) limit applied: the line 16 amount before the limit (DECISIONS D10).</summary>
    public decimal? AmountBeforeLimit { get; init; }

    public IReadOnlyList<WorksheetLine> Lines { get; init; } = Array.Empty<WorksheetLine>();
    /// <summary>Worksheet C lines (Worksheet B only; empty when no expenses were entered).</summary>
    public IReadOnlyList<WorksheetLine> WorksheetC { get; init; } = Array.Empty<WorksheetLine>();

    /// <summary>Review warnings (shown on screen and printed).</summary>
    public IReadOnlyList<Warning> Warnings { get; init; } = Array.Empty<Warning>();
    /// <summary>Extra worksheet notes that are not Review warnings (overnights caution, limit details).</summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    public string ParentName(Parent p) => p == Parent.Parent1 ? Parent1Name : Parent2Name;

    /// <summary>Monthly amount owed by a parent (0 for the other parent or when nothing is owed).</summary>
    public decimal OwedBy(Parent p) => Payer == p ? Amount : 0m;

    public WorksheetLine? Line(string number) => Lines.FirstOrDefault(l => l.Number == number)
                                                 ?? WorksheetC.FirstOrDefault(l => l.Number == number);
}
