namespace MdChildSupport.Core.Tests;

public class MultiFamilyTests
{
    [Theory]
    [InlineData(6100, 2, 1485, 1114)]   // 1485 × 0.75 = 1113.75
    [InlineData(4200, 1, 799, 599)]     // 599.25
    [InlineData(4201, 1, 808, 606)]     // next higher row 4250
    [InlineData(1000, 1, 50, 38)]       // first row; 37.5 rounds away from zero
    [InlineData(0, 3, 57, 43)]          // 42.75
    public void ScheduleColumnForTheAdditionalChildrenTimes75Percent(int income, int kids, int basic, int allowance)
    {
        var a = MultiFamily.Calculate(income, kids);
        Assert.Equal(basic, a.Schedule.Amount);
        Assert.Equal(allowance, a.Allowance);
    }

    [Fact]
    public void ZeroChildrenIsZero() => Assert.Equal(0, MultiFamily.Calculate(5000, 0).Allowance);

    [Fact]
    public void FootnoteShowsTheArithmetic()
    {
        string note = MultiFamily.Calculate(6100, 2).Footnote("Father");
        Assert.Contains("2 additional children", note);
        Assert.Contains("$6,100", note);
        Assert.Contains("$1,485", note);
        Assert.Contains("75% = $1,114", note);
        Assert.Contains("§12-201(c)", note);
    }

    [Fact]
    public void CalculatorFootnoteFollowsCurrentInputsAndNotesAnOverride()
    {
        var c = CaseData.CreateNew(DateTime.Today);
        string field = FieldIds.ForParent(Parent.Parent2, FieldIds.MultiFamily);
        c.Parent2.Income = 6100;
        c.Parent2.MultiFamily = 1114;
        c.SetFootnote(field, "stale text");
        var note = c.Footnotes.Single();
        note.MultiFamilyChildren = 2;

        Assert.Contains("Father's actual income of $6,100", Footnotes.Render(c, note));
        c.UseParentLabels = true;
        Assert.Contains("Parent 2's actual income", Footnotes.Render(c, note));
        c.Parent2.MultiFamily = 900;
        Assert.Contains("The amount used, $900, was entered by the preparer instead", Footnotes.Render(c, note));

        Footnotes.Refresh(c);
        var back = CaseFile.Deserialize(CaseFile.Serialize(c));
        Assert.Equal(2, back.Footnotes.Single().MultiFamilyChildren);
        Assert.Equal(note.Text, back.Footnotes.Single().Text);
    }

    [Fact]
    public void AllowanceReducesAdjustedIncomeOnlyInOctober2025()
    {
        var c = CaseData.CreateNew(DateTime.Today);
        c.Children[0].Name = "A";
        c.Parent1.Income = 4000;
        c.Parent2.Income = 6100;
        c.Parent2.MultiFamily = MultiFamily.Calculate(6100, 2).Allowance;
        Assert.Equal(6100 - 1114, Calculator.Calculate(c).AdjustedIncome[1]);
        c.Guidelines = GuidelinesVersion.July2022;
        var r = Calculator.Calculate(c);
        Assert.Equal(6100, r.AdjustedIncome[1]);
        Assert.Contains(r.Warnings, w => w.Code == "MultifamilyIgnored");
    }
}
