namespace MdChildSupport.Core.Tests;

/// <summary>The engine reproduces every hand-worked case in tests/cases/.</summary>
public class WorkedCaseTests
{
    public static TheoryData<string> Cases() => new(WorkedCase.Names());

    [Fact]
    public void ThereAreEnoughCases() => Assert.True(WorkedCase.Names().Count() >= 12);

    [Theory]
    [MemberData(nameof(Cases))]
    public void EngineMatchesWorkedCase(string name)
    {
        var wc = WorkedCase.Load(name);
        var r = Calculator.Calculate(wc.Case);
        var actual = r.Lines.Concat(r.WorksheetC).ToList();

        Assert.Equal(wc.Expected.Select(e => e.Line), actual.Select(a => a.Number));
        foreach (var (line, values) in wc.Expected)
        {
            var a = actual.Single(x => x.Number == line);
            for (int c = 0; c < 3; c++)
                Assert.True(values[c] == a.FormatValue(c),
                    $"{name} line {line} column {c}: expected '{values[c]}', engine '{a.FormatValue(c)}'");
        }
        Assert.Equal(wc.Result, WorkedCase.Describe(r));
        Assert.Equal(wc.Warnings.Order(), r.Warnings.Select(w => w.Code).Distinct().Order());
    }

    [Fact]
    public void OvernightsCoverage()
    {
        var nights = WorkedCase.Names().Select(WorkedCase.Load)
            .Where(c => c.Case.Custody == CustodyType.Shared).Select(c => c.Case.Parent1Overnights).ToHashSet();
        foreach (int n in new[] { 92, 94, 95, 98, 99, 102, 103, 105, 106, 109, 110, 183 })
            Assert.Contains(n, nights);
    }

    [Theory]
    [InlineData("S092", 2146, 1920)]
    [InlineData("B01", 1612, 1392)]
    public void SharedLimitKeepsThePreLimitAmount(string name, int before, int after)
    {
        var r = Calculator.Calculate(WorkedCase.Load(name).Case);
        Assert.True(r.SharedLimitApplied);
        Assert.Equal(before, r.AmountBeforeLimit);
        Assert.Equal(after, r.Amount);
        Assert.Null(Calculator.Calculate(WorkedCase.Load("S183").Case).AmountBeforeLimit);
    }

    [Fact]
    public void SoleExampleGives1251()
    {
        // A01's inputs under July 2022 with Parent 1/2 labels: samples/sole-jul2022-parent-labels.mdcase.
        var c = WorkedCase.Load("A01").Case;
        c.Guidelines = GuidelinesVersion.July2022;
        c.UseParentLabels = true;
        var r = Calculator.Calculate(c);
        Assert.Equal(Parent.Parent2, r.Payer);
        Assert.Equal(1251m, r.Amount);
        Assert.Equal(new[] { 45.2m, 54.8m }, r.SharePercent);
    }
}
