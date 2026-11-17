# Decisions log

Judgement calls where the statute (FL §§12-102, 12-201 – 12-204) and the official worksheets (CC-DR-034/035,
Rev. 07/2026) do not settle a question, decided by the principles below. The arithmetic itself is
specified in `docs/calculation.md`.

**Status:** D4, D10, D13 and D16 were reviewed and decided by the maintainers on 2026-11-17 (marked *Maintainer decision*).
The other items were decided during development from the sources and principles below.

## Principles

- **P1. Statute first**, then the official form's instructions, then the principles below.
- **P2. Hand-checkable.** Every number printed on the worksheet can be reproduced with pencil and paper from the
  other printed numbers and the form's instructions: money in `decimal`, round only where the form shows a value,
  and use that shown value in later steps.
- **P3. Ordinary rounding.** Dollars round to whole dollars, half away from zero. Percentages round to the precision
  the form shows; where it does not say, one decimal place of a percent.
- **P4. Don't invent law.** Where the statute leaves an amount to the court, label any helper value as advisory,
  show a Review warning, and add a note on the worksheet.
- **P5. Never change input silently.** Any limit applied to an input is visible on screen and on the worksheet.
- **P6. Apply mandatory limits.** Where the statute says "may not exceed", apply the limit and show it.
- **P7. Symmetry.** Swapping the two parents' inputs (and the custody direction) swaps every result exactly.

Sources are listed in `PROVENANCE.md`.

---

---

### D1 — Input precision
- **Question:** precision of money inputs and of cash medical support.
- **Sources:** UI (`DollarFormatter`, `PercentFormatter` one decimal); FL §12-102(c)(3)(ii), (c)(4).
- **Options:** cents; whole dollars.
- **Choice:** money inputs are whole dollars (the UI rounds on entry). Cash medical is a percent with one decimal;
  its dollar value is `R$(actual income × percent / 100)`.
- **Reasoning:** the form's columns are whole-dollar; §12-102 states the limit as a percent of *actual* income, so the
  percent applies to line 1, not line 2.

### D2 — Precision of percentages and use of shown values
- **Sources:** CC-DR-034/035 lines 3 and 7 show "%" with no precision; P2, P3.
- **Choice:** one decimal place of a percent, rounded half away from zero, each parent rounded independently.
  Later lines use the shown percentage (line 6 of A, lines 8 and 9 of B, Worksheet C).
- **Alternatives:** force Parent 2 = 100 − Parent 1 (sums always 100.0% but breaks symmetry P7 at exact .x5
  midpoints); unrounded ratios (not hand-checkable from the printed sheet).
- **Consequence:** shares can sum to 99.9–100.1%, so line 6 (A) or line 8 (B) may differ from the combined line by
  $1. Accepted and tested ("within display rounding").

### D3 — Schedule edge cases
- **Sources:** §12-204(c), (e).
- **Choice:** combined income ≤ $1,200 (including $0) uses the `0–1200` row; an exact row amount uses that row;
  anything between rows uses the next higher row. Combined adjusted income can never be negative (D11).
  If combined adjusted income is $0, shares cannot be computed (0 ÷ 0): both shares show 0.0%, nothing is owed,
  and a Review warning says the court must decide.

### D4 — Combined income above $30,000 — *Maintainer decision 2026-11-17*
- **Sources:** §12-204(d) (court's discretion); §12-204(e) schedule.
- **Observation:** from $15,000 to $30,000 every column of the schedule rises almost exactly linearly
  (e.g. one child: $2,160 → $2,661 at $22,500 → $3,163).
- **Choice (advisory only):** round the combined income up to a multiple of $50 (consistent with §12-204(c)), then
  extend the $15,000–$30,000 slope: `B = R$(B₃₀₀₀₀ + (C↑ − 30000) × (B₃₀₀₀₀ − B₁₅₀₀₀)/15000)`.
- **Alternatives:** cap at the $30,000 row; percentage-of-income extrapolation; refuse to compute.
- **Maintainer decision:** extend the $15,000–$30,000 slope, as above.
- **Presentation (P4):** "Basic obligation (schedule)" reads "$x (extrapolated)", the payer text ends "(advisory)",
  a Review warning explains §12-204(d), and the worksheet prints a note.

### D5 — Self-support reserve marks
- **Sources:** §12-204(e) asterisks; §12-201(n); forms.
- **Choice:** the embedded schedule keeps every asterisk (tested against the statute). The forms do not show them, so
  the worksheet does not either.

### D6 — Number of children
- **Choice:** count the child rows with a non-blank name (1–6; "6 or more" column for 6). With none, nothing is
  computed, the basic obligation and all support show $0, "0 children" is shown and a Review warning asks for a child.
- **Note:** a blank new case therefore shows the Review bar until a child is named.

### D7 — Overnights below 92 / year length
- **Sources:** §12-201(o), (p); §12-204(m)(2)(ii); CC-DR-035 lines 6–7 ("must total 365", "divide by 365").
- **Choice:** 365-day year (the form's own instruction). Parent 1's entered number is kept in the case file as
  entered; the engine uses it limited to 92–273, and Parent 2 = 365 − Parent 1. When the entered number is outside
  that range, a caution is shown under the inputs and printed in the worksheet notes (P5). The form's "STOP —
  use Worksheet A" instruction is reflected in that caution's wording.

### D8 — Order and rounding of shared-custody steps
- **Choice:** Worksheet B line order exactly; each line rounded to whole dollars (money) or 0.1% (percent) as shown,
  and the shown value carried forward. Line 5 = `R$(1.5 × line 4)`; line 9 uses line 7 of the *other* parent;
  line 10 = `R$(line 9 × factor)`.

### D9 — Cash medical support in shared custody
- **Sources:** §12-102(c)(5) ("added to the basic child support obligation and divided … in proportion"); CC-DR-035
  line 13d and Worksheet C lines g–h; CC-DR-034 lines 4d and 7.
- **Choice:** cash medical dollars are an expense paid by the parent ordered to pay it. Worksheet A: line 4d and the
  parent's line 7. Worksheet B: line 13d and Worksheet C lines g–h, exactly like the other expenses.

### D10 — §12-204(m)(5) limit — *Maintainer decision 2026-11-17*
- **Question:** what is compared with what.
- **Sources:** §12-204(m)(5) ("the amount owed under paragraph (3) … may not exceed the amount that would be owed
  under subsection (l)"); CC-DR-035 line 16 NOTE ("Amount owed in a shared custody arrangement may not exceed the
  amount that would be owed if the obligor parent were a non-custodial parent. See WORKSHEET A").
- **Options:** (a) limit line 12/15 (basic only) by the Worksheet A amount (which is net of expenses) — compares unlike
  quantities and double-counts direct payments; (b) limit line 16 (net of expenses) by the Worksheet A line 9
  amount for the line-16 payer as non-custodial parent — what the form says; (c) limit line 12 by the payer's share of
  the basic obligation only.
- **Choice:** (b). The statute's words leave the content of "the amount owed under (l)" open; the form resolves it
  (P1: statute, then form). Applied as a mandatory limit (P6): the support owed is the lesser amount; the worksheet
  prints the Worksheet A figure and a note, and a Review warning is shown.
- **Maintainer decision:** option (b), plus a hover note on the recommended amount in the app giving the higher total the
  shared-custody calculation would produce without the statutory limit (`CalculationResult.AmountBeforeLimit`).

### D11 — Negative adjusted actual income
- **Choice:** line 2 prints the actual (negative) figure marked "counted as $0"; shares and the combined figure use
  $0 for that parent; Review warning. Deductions are not rejected, because they are facts the court may examine.

### D12 — Worksheet A, direct payments exceed the obligation
- **Sources:** §12-204(l)(3); CC-DR-034 line 9 and Comment (2): a negative line 9 "indicates a recommended child
  support order directing the custodial parent to reimburse the non-custodial parent this amount for 'direct pay'
  expenses".
- **Choice:** the form covers it, so follow the form: line 9 prints the negative figure; the result reads
  "{custodial parent} reimburses {non-custodial parent}" with the amount; Review warning and worksheet note.
  (Showing $0 instead was considered; it is not needed because the form settles the point.)
- **Side effect found by the property tests:** in a reimbursement, a larger deduction for the *custodial* parent
  lowers the total obligation (line 5), so the non-custodial parent's line 6 falls and the reimbursement rises
  slightly. This follows from the form's arithmetic. The "support never rises when the payer's deductions rise"
  property is therefore tested for child support owed, not for reimbursements.

### D13 — Multifamily allowance method — *Maintainer decision 2026-11-17*
- **Question:** (1) schedule amount for *k* children vs. *k* × the one-child amount; (2) which income row; (3) how the
  75% is rounded.
- **Sources:** §12-201(c)(2)(i)–(ii); HB 275 (Ch. 532 of 2025) — same words; fiscal note ("the basic support obligation
  for each additional child in the parent's home must be determined according to the guidelines, using only the actual
  income of the parent entitled to the deduction"); CC-DR-034/035 line 1d (a single amount per parent, no method).
- **Readings considered:**
  1. *Schedule column for k children* at the parent's actual income, × 75%.
  2. *k × the one-child amount* ("for each additional child").
  3. Use the parent's *adjusted* income, or the combined income of the parent and another adult.
- **Choice:** reading 1. "Determined in accordance with § 12-204" points to the schedule, whose columns already price
  the number of children together; reading 2 would ignore the schedule's economies of scale and overstate the
  allowance for more than one child. "Actual income" is line 1 (not line 2) — the statute says "actual", and the
  multifamily deduction is itself one of the line-1 deductions. The schedule row follows §12-204(c) (next higher row;
  ≤ $1,200 uses the first row; above $30,000 uses the D4 advisory extrapolation and says so). 75% is applied to the
  whole-dollar schedule amount and rounded to whole dollars, half away from zero.
- **Maintainer decision:** reading 1 (schedule column for *k* children at line-1 actual income, × 75%).
- **Safeguard:** the amount is always editable (§12-201(c)(3)), and the footnote prints the arithmetic.

### D14 — Cash medical above 5%
- **Choice:** allowed; Review warning citing §12-102(c)(3)(ii) and (c)(4), printed on the worksheet.

### D15 — Guidelines version
- **Choice:** manual choice (default October 2025). July 2022 = the same law without line 1d (Ch. 532 of 2025,
  effective October 1, 2025). Multifamily amounts already entered are kept in the file but not applied, with a Review
  warning. The July 2022 version's other change (the former §12-202(a)(2)(iii)2 deviation factor for other children)
  is a ground for departure, not arithmetic, so it does not affect the calculation.

### D16 — Direction of Worksheet B line 14 — *Maintainer decision 2026-11-17*
- **Sources:** CC-DR-035 Worksheet C line l ("enter this answer on line 14 … in the same parent's column") and line 16;
  §12-204(m)(4) ("the parent not incurring the expense shall pay that parent's proportionate share to the parent
  making direct payments").
- **Choice:** line 14 sits in the column of the parent with the greater excess payments (as the form says). That
  parent has overpaid, so under §12-204(m)(4) the *other* parent owes the line 14 amount. The worksheet caption says
  so explicitly. Line 16 nets it against line 15 as the form instructs.
- **Maintainer decision:** as above — the parent who underpaid owes the parent who overpaid.

### D17 — Per-parent expense figures on the worksheet
- **Choice:** Worksheet A lines 4a–4e and Worksheet B line 13a–e print each parent's payment in the parent columns as
  well as the total in the Combined column, so line 7 (A) and Worksheet C can be checked by hand (P2).

### D18 — Worksheet A when custody is "Sole" and both parents have the same results
- **Choice:** the non-custodial parent is fixed by the custody choice; line 9 is always that parent's line 8.

### D19 — Fonts and PDF library
- **Choice:** PDFsharp (Core build) 6.5.0 under its AGPL licence, following the licence decision
  (`docs/licensing.md`); installed Windows font
  Arial at runtime (no font files shipped). If Arial is missing, Segoe UI is used.

### D20 — Branding kept out of the repository (2026-11-17)
- **Question:** how to publish the code without MLA's trademarks while keeping the official app's exact look.
- **Choice:** all branding lives in a brand folder (`branding/README.md`): `default/` (neutral placeholders, committed)
  or a git-ignored `branding/local/` / `-p:BrandDir` for the official build. Brand colours moved from `Brand.xaml` to the
  brand folder's `BrandResources.xaml`; the title-bar logo is `ms-appx:///Branding/BrandLogo.png` with its accessible
  name from `BrandLogoName`; the icon generator takes colours and logomark as parameters.
- **Consequence:** `MainWindow.xaml`, `App.xaml` and `Themes/Brand.xaml` take the logo, its name and the colours from
  the brand folder. With MLA's brand folder the app matches MLA's design exactly, and `tools/branding/make-app-icon.ps1`
  reproduces MLA's icon exactly from MLA's colours and logomark.

