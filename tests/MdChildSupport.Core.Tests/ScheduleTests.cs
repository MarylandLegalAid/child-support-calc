namespace MdChildSupport.Core.Tests;

public class ScheduleTests
{
    /// <summary>The embedded schedule matches §12-204(e) in the statute text on every row and column.</summary>
    [Fact]
    public void EmbeddedScheduleMatchesStatuteText()
    {
        string[] statute = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Data", "gfl-12-204.txt"));
        int start = Array.FindIndex(statute, l => l.StartsWith("(e) Schedule of basic child support obligations"));
        int end = Array.FindIndex(statute, l => l.StartsWith("(f) "));
        Assert.True(start > 0 && end > start);
        var parsed = ChildSupportSchedule.Parse(statute[(start + 1)..end].Where(l => char.IsDigit(l.TrimStart().FirstOrDefault())));

        var embedded = ChildSupportSchedule.Rows;
        Assert.Equal(577, embedded.Count);
        Assert.Equal(parsed.Count, embedded.Count);
        for (int r = 0; r < parsed.Count; r++)
        {
            Assert.Equal(parsed[r].Income, embedded[r].Income);
            Assert.Equal(parsed[r].Amounts, embedded[r].Amounts);
            Assert.Equal(parsed[r].SelfSupportReserve, embedded[r].SelfSupportReserve);
        }
        Assert.Equal(1200, embedded[0].Income);
        Assert.Equal(new[] { 50, 55, 57, 58, 59, 60 }, embedded[0].Amounts);
        Assert.All(embedded[0].SelfSupportReserve, Assert.True);
        Assert.Equal(30000, embedded[^1].Income);
        Assert.Equal(new[] { 3163, 4596, 5256, 5871, 6458, 7020 }, embedded[^1].Amounts);
        // Rows are $50 apart after the first.
        for (int r = 1; r < embedded.Count; r++) Assert.Equal(1200 + 50 * r, embedded[r].Income);
    }

    [Theory]
    [InlineData(0, 1, 50, 1200)]
    [InlineData(1200, 2, 55, 1200)]
    [InlineData(1201, 1, 74, 1250)]
    [InlineData(1250, 1, 74, 1250)]
    [InlineData(1251, 1, 109, 1300)]
    [InlineData(5000, 2, 1356, 5000)]
    [InlineData(5001, 2, 1368, 5050)]
    [InlineData(9950, 2, 1878, 9950)]
    [InlineData(30000, 6, 7020, 30000)]
    [InlineData(29999, 1, 3163, 30000)]
    public void LookupUsesTheNextHigherRow(int income, int children, int expected, int row)
    {
        var l = ChildSupportSchedule.Lookup(income, children);
        Assert.Equal(expected, l.Amount);
        Assert.Equal(row, l.RowIncome);
        Assert.False(l.Extrapolated);
    }

    [Fact]
    public void SixOrMoreColumnAndZeroChildren()
    {
        Assert.Equal(ChildSupportSchedule.Lookup(4000, 6).Amount, ChildSupportSchedule.Lookup(4000, 9).Amount);
        Assert.Equal(0, ChildSupportSchedule.Lookup(4000, 0).Amount);
    }

    [Fact]
    public void SelfSupportReserveFlagIsKept()
    {
        Assert.True(ChildSupportSchedule.Lookup(1650, 2).SelfSupportReserve);   // *379
        Assert.False(ChildSupportSchedule.Lookup(1650, 1).SelfSupportReserve);  // 339
    }

    [Fact]
    public void AboveScheduleIsAdvisoryExtrapolation()
    {
        // One child: slope (3163 − 2160) / 15000; $31,234 rounds up to $31,250.
        var l = ChildSupportSchedule.Lookup(31234, 1);
        Assert.True(l.Extrapolated);
        Assert.Null(l.RowIncome);
        Assert.Equal(Money.Dollars(3163 + 1250m * 1003m / 15000m), l.Amount); // 3246.58 → 3247
        Assert.Equal(3247, l.Amount);
        // Monotone and continuous at the top.
        Assert.True(ChildSupportSchedule.Lookup(30001, 3).Amount >= 5256);
    }
}
