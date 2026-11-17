# Calculation specification

This document is the contract for `MdChildSupport.Core`. The engine (`Calculator.cs`) implements exactly what is
written here, and the hand-worked cases in `tests/cases/` are checked against it. Each step cites the statute
(Md. Code, Family Law, "FL") or the official worksheet (CC-DR-034 Worksheet A, CC-DR-035 Worksheets B and C,
both Rev. 07/2026). Decision numbers (D1 …) refer to `DECISIONS.md`.

## 0. Conventions

- **Money** is `decimal`, never binary floating point. Money inputs are whole dollars (D1).
- **R$(x)** = round `x` to whole dollars, half away from zero (P3). Applied only where the form shows a value.
- **R%(x)** = round `x` (a percentage, e.g. 42.2471) to one decimal place, half away from zero (D2).
- A rounded value, once shown on the worksheet, is the value used in every later step (P2).
- Parent 1 = Mother, Parent 2 = Father (labels only). Columns are Parent 1 | Parent 2 | Combined.
- **n** = number of children = number of the 6 child rows with a non-blank name (D6).
- **Guidelines version** (D15): *October 2025* applies line 1d; *July 2022* sets line 1d to $0 for both parents.

## 1. Inputs

Per parent *i*: actual income `I`, multifamily adjustment `MF`, pre-existing support paid `PS`, alimony paid in
other cases `AP`, child care `CC`, health insurance `HI`, extraordinary medical `EM`, cash medical percent `CM%`
(one decimal), additional expenses `AE`. Case: alimony awarded in this case to Parent 1 `AL` (signed),
Parent 1 overnights entered `ON₁` (shared custody only), custody type, guidelines version.

**Cash medical dollars** (FL §12-102(a)(6), (c)(5); D1): `CMD_i = R$(I_i × CM%_i / 100)`.
If `CM%_i > 5.0` a Review warning cites §12-102(c)(3)(ii)/(c)(4) (D14); the amount is still used.

## 2. Lines common to both worksheets (lines 1–4)

| Line | Parent *i* | Combined | Source |
|---|---|---|---|
| 1 | `I_i` | `I₁ + I₂` | FL §12-201(b) |
| 1a | `PS_i` (subtracted) | – | §12-201(c)(1)(i) |
| 1b | `AP_i` (subtracted) | – | §12-201(c)(1)(ii) |
| 1c | Parent 1: `+AL`; Parent 2: `−AL` | – | §12-204(a)(2)(ii) |
| 1d | October 2025: `MF_i` (subtracted); July 2022: $0 | – | §12-201(c)(1)(iii), §12-204(a)(3); Ch. 532 of 2025 |
| 2 | `A_i = I_i − PS_i − AP_i ± AL − MF_i` | `C = A′₁ + A′₂` | §12-201(c), (f) |
| 3 | `S_i = R%(100 × A′_i / C)` if `C > 0`, else 0.0% | – | §12-204(a)(1) |
| 4 | – | `B = schedule(C, n)` | §12-204(c), (e) |

where `A′_i = max(0, A_i)` is the adjusted income counted for shares (D11). Line 2 prints the actual `A_i`;
when `A_i < 0` the worksheet marks it "counted as $0" and a Review warning is shown. The combined figure on
line 2 is the sum of the counted amounts.

Shares are rounded independently, so `S₁ + S₂` can be 99.9%, 100.0% or 100.1% (it is exactly symmetric, P7).
When `C = 0`, no share can be computed: both shares are 0.0%, nothing is owed, and a Review warning says so (D3).

### 2.1 Schedule lookup `schedule(C, n)` (D3, D4, D5)

The schedule is §12-204(e), embedded verbatim in `Schedule/schedule.txt` (rows "0–1200", 1250, 1300, …, 30000;
columns 1 … "6 or more" children; asterisks kept as a self-support-reserve flag).

1. If `n = 0`: nothing is computed, `B = $0` (D6).
2. If `C ≤ 1200` (including $0): the `0–1200` row.
3. If `1200 < C ≤ 30000`: the first row whose income is `≥ C` — an exact match uses that row; any amount between
   rows uses the next higher row (§12-204(c)).
4. If `C > 30000` (§12-204(d), court's discretion): an **advisory** amount (D4):
   `C↑ = C` rounded up to a multiple of $50; `m = (B₃₀₀₀₀ − B₁₅₀₀₀) / 15000`;
   `B = R$(B₃₀₀₀₀ + (C↑ − 30000) × m)`. Shown as "$x (extrapolated)", with a Review warning and a worksheet note.

## 3. Worksheet A — sole (primary) physical custody (FL §12-204(l); CC-DR-034)

`P_i = CC_i + HI_i + EM_i + CMD_i + AE_i` (what parent *i* pays directly).

| Line | Parent *i* | Combined | Source |
|---|---|---|---|
| 4a | `CC_i` | `CC₁ + CC₂` | §12-204(g) |
| 4b | `HI_i` | `HI₁ + HI₂` | §12-204(h)(1) |
| 4c | `EM_i` | `EM₁ + EM₂` | §12-204(h)(2) |
| 4d | `CMD_i` | `CMD₁ + CMD₂` | §12-102(c)(5) |
| 4e | `AE_i` | `AE₁ + AE₂` | §12-204(i) |
| 5 | – | `T = B + 4a + 4b + 4c + 4d + 4e` | §12-204(l)(1) |
| 6 | `O_i = R$(T × S_i / 100)` | – | §12-204(l)(1) |
| 7 | `P_i` | – | §12-204(l)(3) |
| 8 | `O_i − P_i` | – | §12-204(l)(3) |
| 9 | line 8 of the non-custodial parent (NCP), in that parent's column | – | §12-204(l)(3) |

The per-parent figures on lines 4a–4e are printed so line 7 can be checked; the form's own entry is the
Combined column.

**Result.** If line 9 > 0 the NCP pays it. If line 9 < 0 the form's Comment (2) applies: the custodial parent
reimburses the NCP `|line 9|` for direct-pay expenses; the result says so, with a Review warning and a worksheet
note (D12). If line 9 = 0 nothing is owed.

## 4. Worksheet B — shared physical custody (FL §12-204(f), (m); CC-DR-035)

Overnights (D7): `N₁ = clamp(ON₁, 92, 273)`, `N₂ = 365 − N₁`. If `ON₁` was outside 92–273 the limited value is
used, and a caution note is shown on screen and printed (P5).

| Line | Parent *i* | Combined | Source |
|---|---|---|---|
| 4 | – | `B` | §12-204(e) |
| 5 | – | `B₅ = R$(1.5 × B)` | §12-204(f) |
| 6 | `N_i` | 365 | §12-201(o) |
| 7 | `V_i = R%(100 × N_i / 365)` | – | §12-204(m)(2)(i) |
| 8 | `E_i = R$(B₅ × S_i / 100)` | – | §12-204(m)(1) |
| 9 | `Θ₁ = R$(E₁ × V₂ / 100)`, `Θ₂ = R$(E₂ × V₁ / 100)` | – | §12-204(m)(2)(i) |
| 10 | if `92 ≤ N_i ≤ 109`: `X_i = R$(Θ_i × f(N_i))`, else blank | – | §12-204(m)(2)(ii) |
| 11 | if line 10 applies: `Θ_i + X_i`, else blank | – | §12-204(m)(2)(ii)1 |
| 12 | `|Θ*₁ − Θ*₂|` under the parent *G* with the greater `Θ*`, where `Θ*_i` = line 11 if it applies, else line 9 | – | §12-204(m)(3) |

`f(N)` = 0.10 (92–94), 0.08 (95–98), 0.06 (99–102), 0.04 (103–105), 0.02 (106–109) — §12-204(m)(2)(ii)2.A–E.
If `Θ*₁ = Θ*₂`, line 12 is $0 and no parent is *G*.

### 4.1 Expenses — line 13 and Worksheet C (§12-204(m)(4); D9)

Line 13a–e are printed exactly like Worksheet A lines 4a–4e (per-parent amounts and combined). Cash medical support
(13d) is treated as paid by the parent ordered to provide it (D9).

Worksheet C, for each expense line *k* ∈ {13a … 13e} whose combined total `T_k > 0`:

- proportionate share (C lines a, c, e, g, i): `Q_ik = R$(T_k × S_i / 100)`
- excess (C lines b, d, f, h, j): `Z_ik = max(0, paid_ik − Q_ik)`

C line k: `K_i = Σ_k Z_ik`. C line l: `L = |K₁ − K₂|`, placed under the parent *E* with the greater `K`.
Worksheet C is printed only when at least one expense is entered.

**Line 14** = `L` in parent *E*'s column. Parent *E* paid more than their proportionate share, so under
§12-204(m)(4) the **other** parent owes `L` to *E* (D16).

**Line 15** = line 12 (same column).

**Line 16** (form instructions): working in "amount Parent 1 owes Parent 2",
`D = (+line 15 if G = Parent 1, −line 15 if G = Parent 2) + (+L if E = Parent 2, −L if E = Parent 1)`.
If `D > 0` Parent 1 owes `D`; if `D < 0` Parent 2 owes `−D`; if `D = 0` nothing is owed.

### 4.2 Limit — §12-204(m)(5) (D10)

Let *Y* be the parent who owes on line 16. Compute Worksheet A (section 3) for the same inputs with the **other**
parent as custodial parent; `W` = line 8 for *Y*. If `line 16 > max(0, W)`, the amount owed is limited to
`max(0, W)`. The worksheet prints the limit row and a note giving both figures, and a Review warning is shown.

## 5. Results shown in the app

- *Recommended support*: "{payer} pays" + amount, or "No child support owed by either parent". For a Worksheet A
  reimbursement: "{custodial parent} reimburses {NCP}". When the basic obligation is advisory (D4) the payer text
  ends "(advisory)".
- *Adjusted actual income*: line 2 per parent. *Share of income*: line 3. *Monthly support owed*: the amount in the
  payer's column; $0 for the other parent.
- *Combined actual income*: line 1 combined. *Combined adjusted income*: line 2 combined. *Children counted*:
  "n child"/"n children". *Basic obligation (schedule)*: line 4, with "(extrapolated)" when advisory.

## 6. Multifamily allowance calculator (FL §12-201(c)(1)(iii), (c)(2); D13)

For a parent with actual income `I` (line 1) and `k` additional children (1–6) living in that parent's home more
than 92 overnights a year and not covered by this order:

1. `B_k = schedule(I, k)` — section 2.1 using only that parent's actual income and the column for `k` children.
2. Allowance `= R$(0.75 × B_k)`.

`k = 0` gives $0. The calculator fills the parent's line 1d and attaches a footnote showing this arithmetic. The
user may overwrite the amount (§12-201(c)(3)); typing over it keeps the footnote unless *Clear* is used.

## 7. Warnings and notes

Every Review warning is printed in the worksheet's Notes. The full list:

| Code | When | Text (summary) |
|---|---|---|
| NoChildren | `n = 0` | enter at least one child; nothing calculated |
| ZeroIncome | `n > 0` and `C = 0` | shares cannot be determined; court must decide |
| NegativeAdjusted | `A_i < 0` | parent's adjusted income below $0, counted as $0 |
| AboveSchedule | `C > 30000` | basic obligation extrapolated, advisory only (§12-204(d)) |
| CashMedicalOver5 | `CM%_i > 5` | above the 5% limit in §12-102(c) |
| Reimbursement | Worksheet A line 9 < 0 | custodial parent reimburses direct-pay expenses (Comment (2)) |
| SharedLimit | §12-204(m)(5) limit applied | amount limited to Worksheet A amount |
| MultifamilyIgnored | July 2022 and any `MF_i > 0` | multifamily amounts not applied before Oct 1, 2025 |

The overnights caution (D7) is shown under the inputs (not in Review) and printed in Notes.
