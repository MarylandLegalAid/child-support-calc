namespace MdChildSupport.Core;

/// <summary>Result of the multifamily allowance calculator (docs/calculation.md §6, DECISIONS D13).</summary>
public sealed record MultiFamilyAllowance(decimal ActualIncome, int AdditionalChildren, ScheduleLookup Schedule, decimal Allowance)
{
    /// <summary>Explanatory footnote printed on the worksheet for the parent's line 1d.</summary>
    public string Footnote(string parentName)
    {
        if (AdditionalChildren <= 0) return "";
        string kids = AdditionalChildren == 1 ? "1 additional child" : $"{AdditionalChildren} additional children";
        string row = Schedule.Extrapolated
            ? "an advisory extrapolation above the schedule's $30,000 top row (FL §12-204(d))"
            : Schedule.RowIncome == ChildSupportSchedule.FirstRowTop ? "schedule row $0–$1,200"
            : $"schedule row {Money.Format(Schedule.RowIncome ?? 0)}";
        return $"{parentName}'s multifamily adjustment (FL §12-201(c)(1)(iii), (c)(2)): basic obligation for {kids} in " +
               $"{parentName}'s home at {parentName}'s actual income of {Money.Format(ActualIncome)} ({row}) is " +
               $"{Money.Format(Schedule.Amount)}; × 75% = {Money.Format(Allowance)}.";
    }
}

public static class MultiFamily
{
    public const int MaxAdditionalChildren = 6;

    /// <summary>FL §12-201(c)(2): schedule obligation for the additional children at the parent's actual income × 75%.</summary>
    public static MultiFamilyAllowance Calculate(decimal actualIncome, int additionalChildren)
    {
        int k = Math.Clamp(additionalChildren, 0, MaxAdditionalChildren);
        if (k == 0) return new MultiFamilyAllowance(actualIncome, 0, ScheduleLookup.None, 0m);
        var lookup = ChildSupportSchedule.Lookup(Math.Max(0m, actualIncome), k);
        return new MultiFamilyAllowance(actualIncome, k, lookup, Money.Dollars(lookup.Amount * 0.75m));
    }
}
