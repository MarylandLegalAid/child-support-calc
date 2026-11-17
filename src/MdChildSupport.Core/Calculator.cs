namespace MdChildSupport.Core;

/// <summary>
/// The guideline calculation. Implements docs/calculation.md line by line; see that document for citations.
/// </summary>
public static class Calculator
{
    public const int MinOvernights = 92;
    public const int MaxOvernights = 365 - MinOvernights; // 273
    public const decimal CashMedicalLimitPercent = 5m;

    private static readonly string[] ExpenseKeys = { "a", "b", "c", "d", "e" };

    public static string ParentName(CaseData c, Parent p) =>
        c.UseParentLabels ? (p == Parent.Parent1 ? "Parent 1" : "Parent 2") : (p == Parent.Parent1 ? "Mother" : "Father");

    public static int EffectiveOvernights(int entered) => Math.Clamp(entered, MinOvernights, MaxOvernights);

    /// <summary>Shared-custody adjustment factor for a parent's overnights, FL §12-204(m)(2)(ii)2.</summary>
    public static decimal SharedAdjustmentFactor(int overnights) => overnights switch
    {
        >= 92 and <= 94 => 0.10m,
        >= 95 and <= 98 => 0.08m,
        >= 99 and <= 102 => 0.06m,
        >= 103 and <= 105 => 0.04m,
        >= 106 and <= 109 => 0.02m,
        _ => 0m,
    };

    public static decimal CashMedicalDollars(ParentInputs p) => Money.Dollars(p.Income * p.CashMedicalPercent / 100m);

    public static CalculationResult Calculate(CaseData c)
    {
        var ctx = new Context(c);
        return c.Custody == CustodyType.Shared ? WorksheetB(ctx) : WorksheetA(ctx);
    }

    // ------------------------------------------------------------------ common: lines 1–4

    private sealed class Context
    {
        public readonly CaseData Case;
        public readonly string[] Names;
        public readonly ParentInputs[] P;
        public readonly bool MultiFamilyApplies;
        public readonly int Children;
        public readonly decimal[] CashMedical = new decimal[2];
        public readonly decimal[] Line1c = new decimal[2];
        public readonly decimal[] Line1d = new decimal[2];
        public readonly decimal[] Adjusted = new decimal[2];
        public readonly decimal[] Counted = new decimal[2];
        public readonly decimal[] Share = new decimal[2];
        public readonly decimal CombinedActual;
        public readonly decimal Combined;
        public readonly ScheduleLookup Basic;
        public readonly List<Warning> Warnings = new();
        public readonly List<string> Notes = new();

        public Context(CaseData c)
        {
            Case = c;
            Names = new[] { ParentName(c, Parent.Parent1), ParentName(c, Parent.Parent2) };
            P = new[] { c.Parent1, c.Parent2 };
            MultiFamilyApplies = c.Guidelines == GuidelinesVersion.October2025;
            Children = c.ChildCount;
            decimal alimony = Money.Dollars(c.AlimonyToParent1);
            Line1c[0] = alimony;
            Line1c[1] = -alimony;
            for (int i = 0; i < 2; i++)
            {
                CashMedical[i] = CashMedicalDollars(P[i]);
                Line1d[i] = MultiFamilyApplies ? P[i].MultiFamily : 0m;
                Adjusted[i] = P[i].Income - P[i].ExistingSupport - P[i].AlimonyPaid + Line1c[i] - Line1d[i];
                Counted[i] = Math.Max(0m, Adjusted[i]);
            }
            CombinedActual = P[0].Income + P[1].Income;
            Combined = Counted[0] + Counted[1];
            for (int i = 0; i < 2; i++)
                Share[i] = Combined > 0 ? Money.Percent(100m * Counted[i] / Combined) : 0m;
            Basic = Children > 0 ? ChildSupportSchedule.Lookup(Combined, Children) : ScheduleLookup.None;

            CommonWarnings();
        }

        public bool Computable => Children > 0 && Combined > 0;

        public decimal Paid(int i, string key) => key switch
        {
            "a" => P[i].ChildCare,
            "b" => P[i].HealthInsurance,
            "c" => P[i].ExtraordinaryMedical,
            "d" => CashMedical[i],
            "e" => P[i].AdditionalExpenses,
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };

        public decimal DirectPay(int i) => ExpenseKeys.Sum(k => Paid(i, k));

        private void CommonWarnings()
        {
            if (Children == 0)
                Warnings.Add(new("NoChildren",
                    "Enter the name of at least one child in this case. Nothing is calculated until a child is entered."));
            else if (Combined == 0)
                Warnings.Add(new("ZeroIncome",
                    "Combined adjusted actual income is $0, so the parents' shares of income cannot be determined " +
                    "(FL §12-204(a)(1)). No amount is calculated; the court must decide."));

            for (int i = 0; i < 2; i++)
                if (Adjusted[i] < 0)
                    Warnings.Add(new("NegativeAdjusted",
                        $"{Names[i]}'s deductions exceed actual income: adjusted actual income is {Money.Format(Adjusted[i])}. " +
                        "It is counted as $0 for the income shares and the schedule."));

            if (Children > 0 && Basic.Extrapolated)
                Warnings.Add(new("AboveSchedule",
                    $"Combined adjusted actual income of {Money.Format(Combined)} is above the schedule's highest level " +
                    $"({Money.Format(ChildSupportSchedule.Top)}). Under FL §12-204(d) the court may use its discretion. " +
                    $"The basic obligation of {Money.Format(Basic.Amount)} is an advisory extrapolation of the schedule, " +
                    "not a guideline amount."));

            for (int i = 0; i < 2; i++)
                if (P[i].CashMedicalPercent > CashMedicalLimitPercent)
                    Warnings.Add(new("CashMedicalOver5",
                        $"{Names[i]}'s cash medical support ({Money.FormatPercent(P[i].CashMedicalPercent)} of actual income) " +
                        "is more than 5%. FL §12-102(c)(3)(ii) and (c)(4) limit an order to 5% of the ordered parent's actual income."));

            if (!MultiFamilyApplies && (P[0].MultiFamily > 0 || P[1].MultiFamily > 0))
                Warnings.Add(new("MultifamilyIgnored",
                    "A multifamily adjustment is entered, but the July 2022 guidelines do not include it (it took effect " +
                    "October 1, 2025, Ch. 532 of 2025). It is not applied."));
        }

        public List<WorksheetLine> Lines1To4()
        {
            string mfCaption = MultiFamilyApplies
                ? "Minus multifamily adjustment"
                : "Minus multifamily adjustment (not applicable: July 2022 guidelines)";
            var lines = new List<WorksheetLine>
            {
                new() { Number = "1", Caption = "MONTHLY ACTUAL INCOME (Before taxes) (Code, Family Law Article, § 12-201(b) and (c))",
                        Parent1 = P[0].Income, Parent2 = P[1].Income, Combined = CombinedActual },
                new() { Number = "1a", Caption = "Minus preexisting child support payment actually paid", Operator = Money.Minus, IsMain = false,
                        Parent1 = P[0].ExistingSupport, Parent2 = P[1].ExistingSupport },
                new() { Number = "1b", Caption = "Minus alimony actually paid", Operator = Money.Minus, IsMain = false,
                        Parent1 = P[0].AlimonyPaid, Parent2 = P[1].AlimonyPaid },
                new() { Number = "1c", Caption = "Plus/minus alimony awarded in this case", Operator = "+/" + Money.Minus, IsMain = false,
                        Parent1 = Line1c[0], Parent2 = Line1c[1] },
                new() { Number = "1d", Caption = mfCaption, Operator = Money.Minus, IsMain = false,
                        Parent1 = Line1d[0], Parent2 = Line1d[1] },
                new() { Number = "2", Caption = "MONTHLY ADJUSTED ACTUAL INCOME",
                        Parent1 = Adjusted[0], Parent2 = Adjusted[1], Combined = Combined,
                        Parent1Marker = Adjusted[0] < 0 ? "*" : null, Parent2Marker = Adjusted[1] < 0 ? "*" : null },
                new() { Number = "3", Caption = "PERCENTAGE SHARE OF INCOME (Divide each parent's income on line 2 by the combined income on line 2.)",
                        Format = LineFormat.Percent, Parent1 = Share[0], Parent2 = Share[1] },
                new() { Number = "4", Caption = "BASIC CHILD SUPPORT OBLIGATION (Apply line 2 Combined Income to Child Support Schedule.)",
                        Combined = Basic.Amount, CombinedMarker = Basic.Extrapolated ? "*" : null },
            };
            if (Adjusted[0] < 0 || Adjusted[1] < 0)
                Notes.Add("* Line 2: a negative adjusted actual income is counted as $0 for the combined income and line 3 (DECISIONS D11).");
            if (Basic.Extrapolated)
                Notes.Add($"* Line 4: combined income is above {Money.Format(ChildSupportSchedule.Top)}. Advisory amount = " +
                          $"$30,000 row + (income rounded up to $50 − $30,000) × slope of the $15,000–$30,000 rows " +
                          $"({ChildSupportSchedule.ExtrapolationSlope(Children):0.######} per dollar). Not a guideline amount (FL §12-204(d)).");
            return lines;
        }

        public IEnumerable<WorksheetLine> ExpenseLines(string prefix, Func<string, string> number)
        {
            string[] captions =
            {
                "Work-Related Child Care Expenses (Code, Family Law Article, § 12-204(g))",
                "Health Insurance Expenses (Code, Family Law Article, § 12-204(h)(1))",
                "Extraordinary Medical Expenses (Code, Family Law Article, § 12-204(h)(2))",
                "Cash Medical Support (Code, Family Law Article, § 12-102(c) - applies only to a child support order under Title IV, Part D of the Social Security Act)",
                "Additional Expenses (Code, Family Law Article, § 12-204(i))",
            };
            for (int k = 0; k < 5; k++)
            {
                string key = ExpenseKeys[k];
                decimal p1 = Paid(0, key), p2 = Paid(1, key);
                yield return new WorksheetLine
                {
                    Number = number(key), Caption = captions[k], Operator = "+", IsMain = false,
                    Parent1 = p1, Parent2 = p2, Combined = p1 + p2,
                };
            }
        }
    }

    // ------------------------------------------------------------------ Worksheet A

    private sealed record SoleSheet(decimal Total, decimal[] Obligation, decimal[] DirectPay, decimal[] Net, int Ncp);

    private static SoleSheet ComputeSole(Context x, int custodial)
    {
        decimal total = x.Basic.Amount + ExpenseKeys.Sum(k => x.Paid(0, k) + x.Paid(1, k));
        var obligation = new decimal[2];
        var direct = new decimal[2];
        var net = new decimal[2];
        for (int i = 0; i < 2; i++)
        {
            obligation[i] = Money.Dollars(total * x.Share[i] / 100m);
            direct[i] = x.DirectPay(i);
            net[i] = obligation[i] - direct[i];
        }
        return new SoleSheet(total, obligation, direct, net, 1 - custodial);
    }

    private static CalculationResult WorksheetA(Context x)
    {
        int custodial = x.Case.Custody == CustodyType.SoleWithParent1 ? 0 : 1;
        var lines = x.Lines1To4();
        Parent? payer = null;
        decimal amount = 0;
        bool reimbursement = false;

        lines.AddRange(x.ExpenseLines("4", k => "4" + k));
        if (x.Computable)
        {
            var s = ComputeSole(x, custodial);
            int ncp = s.Ncp;
            lines.Add(new() { Number = "5", Caption = "TOTAL CHILD SUPPORT OBLIGATION (Add lines 4, 4a, 4b, 4c, 4d, and 4e.)", Combined = s.Total });
            lines.Add(new() { Number = "6", Caption = "EACH PARENT'S CHILD SUPPORT OBLIGATION (Multiply line 5 by line 3 for each parent.)",
                              Parent1 = s.Obligation[0], Parent2 = s.Obligation[1] });
            lines.Add(new() { Number = "7", Caption = "TOTAL DIRECT PAY BY EACH PARENT (Add the expenses shown on lines 4a, 4b, 4c, 4d, and 4e paid by each parent.)",
                              Parent1 = s.DirectPay[0], Parent2 = s.DirectPay[1] });
            lines.Add(new() { Number = "8", Caption = "RECOMMENDED CHILD SUPPORT AMOUNT (Subtract line 7 from line 6 for each parent.)",
                              Parent1 = s.Net[0], Parent2 = s.Net[1] });
            decimal line9 = s.Net[ncp];
            lines.Add(new() { Number = "9", Caption = "RECOMMENDED CHILD SUPPORT ORDER (Bring down amount from line 8 for the non-custodial parent only.)",
                              Parent1 = ncp == 0 ? line9 : null, Parent2 = ncp == 1 ? line9 : null });

            if (line9 > 0) { payer = (Parent)ncp; amount = line9; }
            else if (line9 < 0)
            {
                payer = (Parent)custodial;
                amount = -line9;
                reimbursement = true;
                x.Warnings.Add(new("Reimbursement",
                    $"Line 9 is negative ({Money.Format(line9)}): the non-custodial parent's direct payments exceed that parent's " +
                    $"share. Per the form's Comment (2), this recommends that {x.Names[custodial]} (custodial parent) reimburse " +
                    $"{x.Names[ncp]} {Money.Format(amount)} a month for \"direct pay\" expenses (FL §12-204(l)(3))."));
            }
        }
        else
        {
            AddBlank(lines, ("5", "TOTAL CHILD SUPPORT OBLIGATION (Add lines 4, 4a, 4b, 4c, 4d, and 4e.)"),
                            ("6", "EACH PARENT'S CHILD SUPPORT OBLIGATION (Multiply line 5 by line 3 for each parent.)"),
                            ("7", "TOTAL DIRECT PAY BY EACH PARENT (Add the expenses shown on lines 4a, 4b, 4c, 4d, and 4e paid by each parent.)"),
                            ("8", "RECOMMENDED CHILD SUPPORT AMOUNT (Subtract line 7 from line 6 for each parent.)"),
                            ("9", "RECOMMENDED CHILD SUPPORT ORDER (Bring down amount from line 8 for the non-custodial parent only.)"));
        }

        return Result(x, WorksheetKind.A, lines, Array.Empty<WorksheetLine>(), payer, amount, reimbursement, false, 0, 0, null);
    }

    private static void AddBlank(List<WorksheetLine> lines, params (string Number, string Caption)[] items)
    {
        foreach (var (n, cap) in items) lines.Add(new WorksheetLine { Number = n, Caption = cap });
    }

    // ------------------------------------------------------------------ Worksheet B

    private static CalculationResult WorksheetB(Context x)
    {
        var lines = x.Lines1To4();
        var wsC = new List<WorksheetLine>();
        int entered = x.Case.Parent1Overnights;
        int n1 = EffectiveOvernights(entered);
        int[] nights = { n1, 365 - n1 };
        string? overnightsNote = null;
        if (entered != n1)
        {
            overnightsNote = $"{x.Names[0]}'s overnights were entered as {entered}. Shared physical custody requires each parent to " +
                             $"have at least {MinOvernights} overnights a year (more than 25%, FL §12-201(o)); otherwise use Worksheet A. " +
                             $"The calculation uses {n1} for {x.Names[0]} and {365 - n1} for {x.Names[1]}.";
            x.Notes.Add("Overnights: " + overnightsNote);
        }

        Parent? payer = null;
        decimal amount = 0;
        bool limited = false;

        if (!x.Computable)
        {
            AddBlank(lines, ("5", "ADJUSTED BASIC CHILD SUPPORT OBLIGATION (Multiply line 4 by 1.5)"));
            lines.Add(new() { Number = "6", Caption = "OVERNIGHTS with each parent (must total 365)", Format = LineFormat.Count,
                              Parent1 = nights[0], Parent2 = nights[1], Combined = 365, Parent1Marker = overnightsNote != null ? "*" : null });
            AddBlank(lines, ("7", "PERCENTAGE WITH EACH PARENT (Divide line 6 by 365)"),
                            ("8", "EACH PARENT'S SHARE OF ADJUSTED BASIC CHILD SUPPORT OBLIGATION (Multiply line 5 by line 3 for each parent.)"),
                            ("9", "THEORETICAL BASIC CHILD SUPPORT OBLIGATION FOR TIME WITH OTHER PARENT"),
                            ("10", "SHARED PHYSICAL CUSTODY ADJUSTMENT"),
                            ("11", "ADJUSTED THEORETICAL BASIC CHILD SUPPORT OBLIGATION"),
                            ("12", "NET BASIC CHILD SUPPORT OBLIGATION"));
            lines.AddRange(x.ExpenseLines("13", k => "13" + k));
            AddBlank(lines, ("14", "NET ADJUSTMENT FROM WORKSHEET C"),
                            ("15", "NET BASIC CHILD SUPPORT OBLIGATION (From line 12, WORKSHEET B)"),
                            ("16", "RECOMMENDED CHILD SUPPORT ORDER"));
            return Result(x, WorksheetKind.B, lines, wsC, null, 0, false, false, nights[0], nights[1], overnightsNote);
        }

        decimal adjustedBasic = Money.Dollars(1.5m * x.Basic.Amount);
        var pct = new decimal[2];
        var share = new decimal[2];
        for (int i = 0; i < 2; i++)
        {
            pct[i] = Money.Percent(100m * nights[i] / 365m);
            share[i] = Money.Dollars(adjustedBasic * x.Share[i] / 100m);
        }
        var theoretical = new[] { Money.Dollars(share[0] * pct[1] / 100m), Money.Dollars(share[1] * pct[0] / 100m) };
        var adj = new decimal?[2];
        var adjTheoretical = new decimal?[2];
        var effective = new decimal[2];
        for (int i = 0; i < 2; i++)
        {
            decimal f = SharedAdjustmentFactor(nights[i]);
            if (f > 0)
            {
                adj[i] = Money.Dollars(theoretical[i] * f);
                adjTheoretical[i] = theoretical[i] + adj[i];
            }
            effective[i] = adjTheoretical[i] ?? theoretical[i];
        }
        decimal net = Math.Abs(effective[0] - effective[1]);
        int? greater = effective[0] > effective[1] ? 0 : effective[1] > effective[0] ? 1 : null;

        lines.Add(new() { Number = "5", Caption = "ADJUSTED BASIC CHILD SUPPORT OBLIGATION (Multiply line 4 by 1.5)", Combined = adjustedBasic });
        lines.Add(new() { Number = "6", Caption = "OVERNIGHTS with each parent (must total 365)", Format = LineFormat.Count,
                          Parent1 = nights[0], Parent2 = nights[1], Combined = 365, Parent1Marker = overnightsNote != null ? "*" : null });
        lines.Add(new() { Number = "7", Caption = "PERCENTAGE WITH EACH PARENT (Divide line 6 by 365)", Format = LineFormat.Percent,
                          Parent1 = pct[0], Parent2 = pct[1] });
        lines.Add(new() { Number = "8", Caption = "EACH PARENT'S SHARE OF ADJUSTED BASIC CHILD SUPPORT OBLIGATION (Multiply line 5 by line 3 for each parent.)",
                          Parent1 = share[0], Parent2 = share[1] });
        lines.Add(new() { Number = "9", Caption = "THEORETICAL BASIC CHILD SUPPORT OBLIGATION FOR TIME WITH OTHER PARENT (Multiply line 8A by line 7B; put answer on line 9A. Multiply line 8B by line 7A; put answer on line 9B.)",
                          Parent1 = theoretical[0], Parent2 = theoretical[1] });
        lines.Add(new() { Number = "10", Caption = "SHARED PHYSICAL CUSTODY ADJUSTMENT (If line 6 for a parent is 92–109 overnights, multiply that parent's line 9 by: 92–94 .10; 95–98 .08; 99–102 .06; 103–105 .04; 106–109 .02)",
                          Parent1 = adj[0], Parent2 = adj[1] });
        lines.Add(new() { Number = "11", Caption = "ADJUSTED THEORETICAL BASIC CHILD SUPPORT OBLIGATION (If the adjustment applies, add line 10 to that parent's line 9.)",
                          Parent1 = adjTheoretical[0], Parent2 = adjTheoretical[1] });
        lines.Add(new() { Number = "12", Caption = "NET BASIC CHILD SUPPORT OBLIGATION (Subtract the lesser of line 9 (or line 11 where it applies) from the greater; place under the parent with the greater amount.)",
                          Parent1 = greater == 0 ? net : null, Parent2 = greater == 1 ? net : null, Combined = greater is null ? 0m : null });
        lines.AddRange(x.ExpenseLines("13", k => "13" + k));

        // Worksheet C (§12-204(m)(4)).
        var excessTotal = new decimal[2];
        bool anyExpense = false;
        string[] cLetters = { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };
        string[] cCaptions =
        {
            "Payments made for line 13a expenses multiplied by each parent's percentage of income (line 3) (Proportionate share)",
            "Excess payments made by the parent who pays more than the amount in line a (Subtract proportionate share from amount paid)",
            "Total amount of direct payments made for line 13b expenses multiplied by each parent's percentage of income (line 3)",
            "The excess amount of direct payments made by the parent who pays more than the amount calculated in line c",
            "Total amount of direct payments made for line 13c expenses multiplied by each parent's percentage of income (line 3)",
            "The excess amount of direct payments made by the parent who pays more than the amount calculated in line e",
            "Total amount of direct payments made for line 13d expenses multiplied by each parent's percentage of income (line 3)",
            "The excess amount of direct payments made by the parent who pays more than the amount calculated in line g",
            "Total amount of direct payments made for line 13e expenses multiplied by each parent's percentage of income (line 3)",
            "The excess amount of direct payments made by the parent who pays more than the amount calculated in line i",
        };
        for (int k = 0; k < 5; k++)
        {
            string key = ExpenseKeys[k];
            decimal total = x.Paid(0, key) + x.Paid(1, key);
            decimal?[] prop = new decimal?[2], excess = new decimal?[2];
            if (total > 0)
            {
                anyExpense = true;
                for (int i = 0; i < 2; i++)
                {
                    prop[i] = Money.Dollars(total * x.Share[i] / 100m);
                    excess[i] = Math.Max(0m, x.Paid(i, key) - prop[i]!.Value);
                    excessTotal[i] += excess[i]!.Value;
                }
            }
            wsC.Add(new() { Number = "C" + cLetters[2 * k], Caption = cCaptions[2 * k], IsMain = false, Parent1 = prop[0], Parent2 = prop[1] });
            wsC.Add(new() { Number = "C" + cLetters[2 * k + 1], Caption = cCaptions[2 * k + 1], IsMain = false, Parent1 = excess[0], Parent2 = excess[1] });
        }
        decimal reimburse = Math.Abs(excessTotal[0] - excessTotal[1]);
        int? overpaid = excessTotal[0] > excessTotal[1] ? 0 : excessTotal[1] > excessTotal[0] ? 1 : null;
        wsC.Add(new() { Number = "Ck", Caption = "For each parent, add lines b, d, f, h, and j", Parent1 = excessTotal[0], Parent2 = excessTotal[1] });
        wsC.Add(new() { Number = "Cl", Caption = "Subtract lesser amount from greater amount in line k; place under the greater amount (also line 14, Worksheet B)",
                        Parent1 = overpaid == 0 ? reimburse : null, Parent2 = overpaid == 1 ? reimburse : null,
                        Combined = overpaid is null ? 0m : null });
        if (!anyExpense) wsC.Clear();

        lines.Add(new() { Number = "14", Caption = "NET ADJUSTMENT FROM WORKSHEET C (line l). The amount is owed TO the parent in whose column it appears, by the other parent (FL §12-204(m)(4)).",
                          Parent1 = overpaid == 0 ? reimburse : null, Parent2 = overpaid == 1 ? reimburse : null,
                          Combined = anyExpense && overpaid is null ? 0m : null });
        lines.Add(new() { Number = "15", Caption = "NET BASIC CHILD SUPPORT OBLIGATION (From line 12, WORKSHEET B)",
                          Parent1 = greater == 0 ? net : null, Parent2 = greater == 1 ? net : null, Combined = greater is null ? 0m : null });

        // Line 16: positive D = Parent 1 owes Parent 2.
        decimal d = (greater == 0 ? net : greater == 1 ? -net : 0m) + (overpaid == 1 ? reimburse : overpaid == 0 ? -reimburse : 0m);
        decimal computed16 = Math.Abs(d);
        int? owing = d > 0 ? 0 : d < 0 ? 1 : null;
        decimal final16 = computed16;

        // §12-204(m)(5) limit (DECISIONS D10): no more than the payer would owe as non-custodial parent on Worksheet A.
        if (owing is int y)
        {
            var sole = ComputeSole(x, custodial: 1 - y);
            decimal cap = Math.Max(0m, sole.Net[y]);
            if (computed16 > cap)
            {
                limited = true;
                final16 = cap;
                string msg = $"The shared-custody amount for {x.Names[y]} ({Money.Format(computed16)}) is more than {x.Names[y]} would owe " +
                             $"as the non-custodial parent on Worksheet A ({Money.Format(cap)}: line 6 {Money.Format(sole.Obligation[y])} − " +
                             $"line 7 {Money.Format(sole.DirectPay[y])}). FL §12-204(m)(5) limits it to {Money.Format(cap)}.";
                x.Warnings.Add(new("SharedLimit", msg));
            }
            if (final16 > 0) { payer = (Parent)y; amount = final16; }
        }

        lines.Add(new() { Number = "16", Caption = "RECOMMENDED CHILD SUPPORT ORDER (Net lines 14 and 15 as the form instructs. May not exceed the amount the obligor would owe as a non-custodial parent on Worksheet A, FL §12-204(m)(5).)",
                          Parent1 = owing == 0 ? final16 : null, Parent2 = owing == 1 ? final16 : null, Combined = owing is null ? 0m : null,
                          Parent1Marker = limited && owing == 0 ? "*" : null, Parent2Marker = limited && owing == 1 ? "*" : null });
        if (limited)
            x.Notes.Add($"* Line 16: before the FL §12-204(m)(5) limit, lines 14 and 15 net to {Money.Format(computed16)}; " +
                        $"the Worksheet A amount for {x.Names[owing!.Value]} as non-custodial parent is {Money.Format(final16)}.");

        return Result(x, WorksheetKind.B, lines, wsC, payer, amount, false, limited, nights[0], nights[1], overnightsNote,
                      limited ? computed16 : null);
    }

    private static CalculationResult Result(Context x, WorksheetKind kind, List<WorksheetLine> lines, IReadOnlyList<WorksheetLine> wsC,
        Parent? payer, decimal amount, bool reimbursement, bool limited, int n1, int n2, string? overnightsNote,
        decimal? beforeLimit = null) => new()
    {
        Kind = kind,
        Guidelines = x.Case.Guidelines,
        Custody = x.Case.Custody,
        ChildCount = x.Children,
        Parent1Name = x.Names[0],
        Parent2Name = x.Names[1],
        ActualIncome = new[] { x.P[0].Income, x.P[1].Income },
        AdjustedIncome = (decimal[])x.Adjusted.Clone(),
        CountedAdjustedIncome = (decimal[])x.Counted.Clone(),
        SharePercent = (decimal[])x.Share.Clone(),
        CashMedicalDollars = (decimal[])x.CashMedical.Clone(),
        CombinedActualIncome = x.CombinedActual,
        CombinedAdjustedIncome = x.Combined,
        Basic = x.Basic,
        Parent1Overnights = n1,
        Parent2Overnights = n2,
        OvernightsNote = overnightsNote,
        Payer = payer,
        Amount = amount,
        IsReimbursement = reimbursement,
        SharedLimitApplied = limited,
        AmountBeforeLimit = beforeLimit,
        Lines = lines,
        WorksheetC = wsC,
        Warnings = x.Warnings,
        Notes = x.Notes,
    };
}
