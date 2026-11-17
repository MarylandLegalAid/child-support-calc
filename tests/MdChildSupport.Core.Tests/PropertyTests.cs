namespace MdChildSupport.Core.Tests;

/// <summary>Properties that must hold for any inputs.</summary>
public class PropertyTests
{
    private static decimal Pick(Random r, int max, int zeroChancePercent = 40) =>
        r.Next(100) < zeroChancePercent ? 0 : r.Next(max + 1);

    internal static CaseData RandomCase(Random r)
    {
        var c = CaseData.CreateNew(new DateTime(2026, 11, 17));
        c.Guidelines = r.Next(2) == 0 ? GuidelinesVersion.October2025 : GuidelinesVersion.July2022;
        c.Custody = (CustodyType)r.Next(3);
        int kids = r.Next(0, 7);
        for (int i = 0; i < kids; i++) c.Children[i].Name = "Child " + i;
        foreach (var p in new[] { c.Parent1, c.Parent2 })
        {
            p.Income = r.Next(100) < 5 ? 0 : r.Next(100) < 10 ? r.Next(25000, 40000) : r.Next(0, 15000);
            p.MultiFamily = Pick(r, 900, 70);
            p.ExistingSupport = Pick(r, 800, 70);
            p.AlimonyPaid = Pick(r, 800, 80);
            p.ChildCare = Pick(r, 1200, 60);
            p.HealthInsurance = Pick(r, 600, 60);
            p.ExtraordinaryMedical = Pick(r, 300, 70);
            p.CashMedicalPercent = r.Next(100) < 70 ? 0 : r.Next(0, 80) / 10m;
            p.AdditionalExpenses = Pick(r, 500, 70);
        }
        c.AlimonyToParent1 = r.Next(100) < 70 ? 0 : r.Next(-1500, 1500);
        c.Parent1Overnights = r.Next(100) < 10 ? r.Next(0, 366) : r.Next(92, 274);
        return c;
    }

    internal static CaseData Swap(CaseData c)
    {
        var s = c.Clone();
        (s.Parent1, s.Parent2) = (s.Parent2, s.Parent1);
        s.AlimonyToParent1 = -c.AlimonyToParent1;
        s.Custody = c.Custody switch
        {
            CustodyType.SoleWithParent1 => CustodyType.SoleWithParent2,
            CustodyType.SoleWithParent2 => CustodyType.SoleWithParent1,
            _ => CustodyType.Shared,
        };
        s.Parent1Overnights = 365 - Calculator.EffectiveOvernights(c.Parent1Overnights);
        return s;
    }

    [Fact]
    public void SwappingParentsSwapsEveryResult()
    {
        var rnd = new Random(20261117);
        for (int n = 0; n < 3000; n++)
        {
            var c = RandomCase(rnd);
            var a = Calculator.Calculate(c);
            var b = Calculator.Calculate(Swap(c));

            Assert.Equal(a.Amount, b.Amount);
            Assert.Equal(a.Payer is null ? null : (Parent?)(1 - (int)a.Payer.Value), b.Payer);
            Assert.Equal(a.IsReimbursement, b.IsReimbursement);
            Assert.Equal(a.Basic, b.Basic);
            var la = a.Lines.Concat(a.WorksheetC).ToList();
            var lb = b.Lines.Concat(b.WorksheetC).ToList();
            Assert.Equal(la.Select(l => l.Number), lb.Select(l => l.Number));
            for (int i = 0; i < la.Count; i++)
            {
                var x = la[i];
                var y = lb[i];
                Assert.True(x.Parent1 == y.Parent2 && x.Parent2 == y.Parent1 && x.Combined == y.Combined,
                    $"case {n}, line {x.Number}: ({x.Parent1}, {x.Parent2}, {x.Combined}) vs swapped ({y.Parent1}, {y.Parent2}, {y.Combined})");
            }
            Assert.Equal(a.Warnings.Select(w => w.Code).Order(), b.Warnings.Select(w => w.Code).Order());
        }
    }

    [Fact]
    public void IncomeSharesAddUpWithinDisplayRounding()
    {
        var rnd = new Random(7);
        for (int n = 0; n < 5000; n++)
        {
            var r = Calculator.Calculate(RandomCase(rnd));
            if (r.CombinedAdjustedIncome == 0) { Assert.Equal(0m, r.SharePercent.Sum()); continue; }
            decimal sum = r.SharePercent.Sum();
            Assert.InRange(sum, 99.9m, 100.1m);
        }
    }

    [Fact]
    public void ExactMidpointSharesAreSymmetricAndMayTotal100Point1()
    {
        // 4,225 / 10,000 = 42.25% and 57.75%: both round away from zero.
        var c = CaseData.CreateNew(DateTime.Today);
        c.Children[0].Name = "A";
        c.Parent1.Income = 4225;
        c.Parent2.Income = 5775;
        var r = Calculator.Calculate(c);
        Assert.Equal(new[] { 42.3m, 57.8m }, r.SharePercent);
    }

    [Fact]
    public void SupportNeverRisesWhenPayersDeductionsRise()
    {
        var rnd = new Random(99);
        for (int n = 0; n < 3000; n++)
        {
            var c = RandomCase(rnd);
            var before = Calculator.Calculate(c);
            // A Worksheet A reimbursement (Comment (2)) is not child support owed by the payer: the custodial
            // parent's deductions shrink the total, which can raise it (DECISIONS D12).
            if (before.Payer is not Parent payer || before.IsReimbursement) continue;
            foreach (string field in new[] { "support", "alimony", "multifamily" })
            {
                var more = c.Clone();
                var p = more.Inputs(payer);
                int delta = rnd.Next(1, 2000);
                switch (field)
                {
                    case "support": p.ExistingSupport += delta; break;
                    case "alimony": p.AlimonyPaid += delta; break;
                    default: p.MultiFamily += delta; break;
                }
                var after = Calculator.Calculate(more);
                Assert.True(after.OwedBy(payer) <= before.Amount,
                    $"case {n}: raising {payer}'s {field} by {delta} raised support from {before.Amount} to {after.OwedBy(payer)}");
            }
        }
    }

    [Fact]
    public void July2022AndOctober2025AgreeWhenNoMultifamilyAmount()
    {
        var rnd = new Random(2025);
        for (int n = 0; n < 2000; n++)
        {
            var c = RandomCase(rnd);
            c.Parent1.MultiFamily = 0;
            c.Parent2.MultiFamily = 0;
            c.Guidelines = GuidelinesVersion.October2025;
            var oct = Calculator.Calculate(c);
            c.Guidelines = GuidelinesVersion.July2022;
            var jul = Calculator.Calculate(c);
            Assert.Equal(oct.Payer, jul.Payer);
            Assert.Equal(oct.Amount, jul.Amount);
            var lo = oct.Lines.Concat(oct.WorksheetC).ToList();
            var lj = jul.Lines.Concat(jul.WorksheetC).ToList();
            Assert.Equal(lo.Count, lj.Count);
            for (int i = 0; i < lo.Count; i++)
                Assert.Equal((lo[i].Number, lo[i].Parent1, lo[i].Parent2, lo[i].Combined), (lj[i].Number, lj[i].Parent1, lj[i].Parent2, lj[i].Combined));
            Assert.Equal(oct.Warnings.Select(w => w.Code), jul.Warnings.Select(w => w.Code));
        }
    }

    [Fact]
    public void EveryShownLineIsHandCheckable()
    {
        // Worksheet A: line 5 = line 4 + 4a..4e; line 8 = 6 − 7. Worksheet B: line 11 = 9 + 10.
        var rnd = new Random(5);
        for (int n = 0; n < 2000; n++)
        {
            var r = Calculator.Calculate(RandomCase(rnd));
            if (r.Line("5")?.Combined is null) continue;
            if (r.Kind == WorksheetKind.A)
            {
                decimal sum = r.Line("4")!.Combined!.Value + "abcde".Sum(k => r.Line("4" + k)!.Combined!.Value);
                Assert.Equal(sum, r.Line("5")!.Combined);
                Assert.Equal(r.Line("6")!.Parent1 - r.Line("7")!.Parent1, r.Line("8")!.Parent1);
            }
            else
            {
                Assert.Equal(Money.Dollars(1.5m * r.Line("4")!.Combined!.Value), r.Line("5")!.Combined);
                for (int c = 0; c < 2; c++)
                    if (r.Line("10")!.Value(c) is decimal adj)
                        Assert.Equal(r.Line("9")!.Value(c) + adj, r.Line("11")!.Value(c));
            }
        }
    }
}
