namespace MdChildSupport.Core;

/// <summary>Which version of the guidelines applies (DECISIONS D15).</summary>
public enum GuidelinesVersion
{
    /// <summary>Current law from October 1, 2025, including the multifamily allowance (FL §12-201(c)(1)(iii)).</summary>
    October2025,
    /// <summary>The same law without the multifamily allowance, for periods before October 1, 2025.</summary>
    July2022,
}

public enum CustodyType
{
    /// <summary>Shared physical custody, Worksheet B (FL §12-204(m)).</summary>
    Shared,
    /// <summary>Sole/primary physical custody with Parent 1 (Mother), Worksheet A (FL §12-204(l)).</summary>
    SoleWithParent1,
    /// <summary>Sole/primary physical custody with Parent 2 (Father), Worksheet A.</summary>
    SoleWithParent2,
}

public enum CourtType { Circuit, District }

public enum Parent { Parent1, Parent2 }

/// <summary>One parent's monthly inputs. Money is whole dollars; <see cref="CashMedicalPercent"/> has one decimal.</summary>
public sealed class ParentInputs
{
    public decimal Income { get; set; }
    public decimal MultiFamily { get; set; }
    public decimal ExistingSupport { get; set; }
    public decimal AlimonyPaid { get; set; }
    public decimal ChildCare { get; set; }
    public decimal HealthInsurance { get; set; }
    public decimal ExtraordinaryMedical { get; set; }
    public decimal CashMedicalPercent { get; set; }
    public decimal AdditionalExpenses { get; set; }

    public ParentInputs Clone() => (ParentInputs)MemberwiseClone();
}

public sealed class ChildInfo
{
    public string Name { get; set; } = "";
    public string BirthDate { get; set; } = "";

    public bool IsCounted => !string.IsNullOrWhiteSpace(Name);
}

/// <summary>A note attached to one input field; printed as a footnote on the worksheet.</summary>
public sealed class FieldNote
{
    public string Field { get; set; } = "";
    public string Text { get; set; } = "";

    /// <summary>
    /// Set when the note was created by the multifamily calculator: the number of additional children used. The
    /// printed text is then regenerated from the current inputs (see <see cref="Footnotes.Render"/>).
    /// </summary>
    public int? MultiFamilyChildren { get; set; }
}

public static class Footnotes
{
    /// <summary>The text to print for a note, kept in step with the current inputs.</summary>
    public static string Render(CaseData c, FieldNote note)
    {
        if (note.MultiFamilyChildren is not int k) return note.Text;
        Parent? p = note.Field == FieldIds.ForParent(Parent.Parent1, FieldIds.MultiFamily) ? Parent.Parent1
                  : note.Field == FieldIds.ForParent(Parent.Parent2, FieldIds.MultiFamily) ? Parent.Parent2 : null;
        if (p is not Parent parent) return note.Text;
        var inputs = c.Inputs(parent);
        var a = MultiFamily.Calculate(inputs.Income, k);
        string text = a.Footnote(Calculator.ParentName(c, parent));
        if (inputs.MultiFamily != a.Allowance)
            text += $" The amount used, {Money.Format(inputs.MultiFamily)}, was entered by the preparer instead (FL §12-201(c)(3)).";
        return text;
    }

    /// <summary>Bring every stored note's text up to date (before saving).</summary>
    public static void Refresh(CaseData c)
    {
        foreach (var n in c.Footnotes) n.Text = Render(c, n);
    }
}

/// <summary>Stable identifiers for input fields, used by footnotes and the case file.</summary>
public static class FieldIds
{
    public const string Income = "income";
    public const string MultiFamily = "multiFamily";
    public const string ExistingSupport = "existingSupport";
    public const string AlimonyPaid = "alimonyPaid";
    public const string ChildCare = "childCare";
    public const string HealthInsurance = "healthInsurance";
    public const string ExtraordinaryMedical = "extraordinaryMedical";
    public const string CashMedicalPercent = "cashMedicalPercent";
    public const string AdditionalExpenses = "additionalExpenses";
    public const string AlimonyToParent1 = "alimonyToParent1";
    public const string Parent1Overnights = "parent1Overnights";

    public static string ForParent(Parent p, string field) => (p == Parent.Parent1 ? "parent1." : "parent2.") + field;
}

/// <summary>Everything the user enters for one case. Computed results are never stored here.</summary>
public sealed class CaseData
{
    public const int ChildRows = 6;
    public const int DefaultOvernights = 183;

    public string CaseNumber { get; set; } = "";
    public string County { get; set; } = Counties.All[0];
    public CourtType Court { get; set; } = CourtType.Circuit;
    public string Plaintiff { get; set; } = "";
    public string Defendant { get; set; } = "";
    public string Preparer { get; set; } = "";
    public string Date { get; set; } = "";
    public bool UseParentLabels { get; set; }
    public GuidelinesVersion Guidelines { get; set; } = GuidelinesVersion.October2025;
    public CustodyType Custody { get; set; } = CustodyType.Shared;
    public List<ChildInfo> Children { get; set; } = Enumerable.Range(0, ChildRows).Select(_ => new ChildInfo()).ToList();
    public ParentInputs Parent1 { get; set; } = new();
    public ParentInputs Parent2 { get; set; } = new();

    /// <summary>Alimony awarded in this case; positive means Parent 1 receives it (FL §12-204(a)(2)).</summary>
    public decimal AlimonyToParent1 { get; set; }

    /// <summary>Parent 1's overnights per year as entered. The engine limits it to 92–273 (DECISIONS D7).</summary>
    public int Parent1Overnights { get; set; } = DefaultOvernights;

    public string Comments { get; set; } = "";
    public List<FieldNote> Footnotes { get; set; } = new();

    public ParentInputs Inputs(Parent p) => p == Parent.Parent1 ? Parent1 : Parent2;

    public int ChildCount => Math.Min(ChildRows, Children.Count(c => c.IsCounted));

    public static CaseData CreateNew(DateTime today) => new() { Date = today.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture) };

    public CaseData Clone()
    {
        var c = (CaseData)MemberwiseClone();
        c.Children = Children.Select(x => new ChildInfo { Name = x.Name, BirthDate = x.BirthDate }).ToList();
        c.Parent1 = Parent1.Clone();
        c.Parent2 = Parent2.Clone();
        c.Footnotes = Footnotes.Select(f => new FieldNote { Field = f.Field, Text = f.Text, MultiFamilyChildren = f.MultiFamilyChildren }).ToList();
        return c;
    }

    /// <summary>Replace (or remove, when <paramref name="text"/> is null) the footnote on a field.</summary>
    public void SetFootnote(string field, string? text)
    {
        Footnotes.RemoveAll(f => f.Field == field);
        if (!string.IsNullOrWhiteSpace(text)) Footnotes.Add(new FieldNote { Field = field, Text = text });
    }
}

/// <summary>Maryland's 23 counties and Baltimore City, in alphabetical (ordinal) order.</summary>
public static class Counties
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Allegany County", "Anne Arundel County", "Baltimore City", "Baltimore County", "Calvert County",
        "Caroline County", "Carroll County", "Cecil County", "Charles County", "Dorchester County",
        "Frederick County", "Garrett County", "Harford County", "Howard County", "Kent County",
        "Montgomery County", "Prince George's County", "Queen Anne's County", "Somerset County", "St. Mary's County",
        "Talbot County", "Washington County", "Wicomico County", "Worcester County",
    };
}
