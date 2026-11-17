"""
Independent worked-case generator for tests/cases/*.md.

This is a second, separate implementation of docs/calculation.md, written from the spec (not from the C# engine).
For each case it writes a Markdown file that shows the official form filled in, with the arithmetic of every line,
and an "Expected" table that MdChildSupport.Core.Tests asserts against the C# engine.

Run from the repository root:  python -I tests/cases/tools/worked.py
"""
import json
import os
import sys
from decimal import Decimal as D, ROUND_HALF_UP, ROUND_CEILING

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
STATUTE = os.path.join(ROOT, "tests", "MdChildSupport.Core.Tests", "Data", "gfl-12-204.txt")
OUT = os.path.join(ROOT, "tests", "cases")
MINUS = "−"


# ---------------------------------------------------------------- schedule (parsed from the statute text)

def load_schedule():
    rows = []
    with open(STATUTE, encoding="utf-8") as f:
        for line in f:
            parts = line.split()
            if len(parts) != 7:
                continue
            first = parts[0].replace("–", "-")
            try:
                income = int(first.split("-")[1]) if "-" in first else int(first)
                amounts = [int(p.lstrip("*")) for p in parts[1:]]
            except ValueError:
                continue
            rows.append((income, amounts))
    assert rows[0][0] == 1200 and rows[-1][0] == 30000 and len(rows) == 577, len(rows)
    return rows


SCHEDULE = load_schedule()


def rd(x):  # R$: whole dollars, half away from zero
    return D(x).quantize(D(1), rounding=ROUND_HALF_UP)


def rp(x):  # R%: one decimal place
    return D(x).quantize(D("0.1"), rounding=ROUND_HALF_UP)


def money(x):
    x = rd(x)
    return (MINUS if x < 0 else "") + "${:,}".format(abs(int(x)))


def pct(x):
    x = rp(x)
    return (MINUS if x < 0 else "") + "{:.1f}%".format(abs(x))


def lookup(income, n):
    """Returns (amount, explanation, extrapolated)."""
    col = min(n, 6) - 1
    income = D(income)
    if income <= 30000:
        for row_income, amounts in SCHEDULE:
            if row_income >= income:
                if row_income == 1200:
                    where = "the $0–$1,200 row"
                elif row_income == income:
                    where = f"exactly on the {money(row_income)} row"
                else:
                    where = f"between rows, so the next higher row {money(row_income)} (§12-204(c))"
                return D(amounts[col]), where, False
    top = D(SCHEDULE[-1][1][col])
    base = D(next(a for i, a in SCHEDULE if i == 15000)[col])
    up = (income / 50).to_integral_value(rounding=ROUND_CEILING) * 50
    slope = (top - base) / D(15000)
    amount = rd(top + (up - 30000) * slope)
    where = (f"above $30,000 — ADVISORY extrapolation: income rounded up to {money(up)}; "
             f"{money(top)} + ({money(up)} − $30,000) × ({money(top)} − {money(base)}) ÷ 15,000 = {money(amount)}")
    return amount, where, True


# ---------------------------------------------------------------- worked calculation

class Sheet:
    def __init__(self):
        self.rows = []      # (number, p1, p2, combined) as display strings
        self.steps = []     # arithmetic, Markdown lines

    def row(self, number, p1="", p2="", comb=""):
        self.rows.append((number, p1, p2, comb))

    def say(self, text):
        self.steps.append(text)


def calculate(c):
    s = Sheet()
    names = ["Parent 1", "Parent 2"] if c.get("useParentLabels") else ["Mother", "Father"]
    p = [c["parent1"], c["parent2"]]
    get = lambda i, k: D(p[i].get(k, 0))
    oct2025 = c.get("guidelines", "october2025") == "october2025"
    n = sum(1 for ch in c["children"] if ch.get("name", "").strip())
    warnings = []

    cm = [rd(get(i, "income") * get(i, "cashMedicalPercent") / 100) for i in range(2)]
    for i in range(2):
        if get(i, "cashMedicalPercent") > 0:
            s.say(f"- Cash medical, {names[i]}: {money(get(i, 'income'))} × {get(i, 'cashMedicalPercent')}% = {money(cm[i])}")
        if get(i, "cashMedicalPercent") > 5:
            warnings.append("CashMedicalOver5")

    alim = rd(D(c.get("alimonyToParent1", 0)))
    c1 = [alim, -alim]
    d1 = [get(i, "multiFamily") if oct2025 else D(0) for i in range(2)]
    adj, cnt = [], []
    for i in range(2):
        a = get(i, "income") - get(i, "existingSupport") - get(i, "alimonyPaid") + c1[i] - d1[i]
        adj.append(a)
        cnt.append(max(D(0), a))
        s.say(f"- Line 2, {names[i]}: {money(get(i,'income'))} − {money(get(i,'existingSupport'))} − {money(get(i,'alimonyPaid'))} "
              f"+ ({money(c1[i])}) − {money(d1[i])} = **{money(a)}**" + (" → counted as $0" if a < 0 else ""))
        if a < 0:
            warnings.append("NegativeAdjusted")
    comb = cnt[0] + cnt[1]
    s.say(f"- Line 2, combined: {money(cnt[0])} + {money(cnt[1])} = **{money(comb)}**")
    share = [rp(100 * cnt[i] / comb) if comb > 0 else D("0.0") for i in range(2)]
    for i in range(2):
        if comb > 0:
            s.say(f"- Line 3, {names[i]}: {money(cnt[i])} ÷ {money(comb)} = {(100 * cnt[i] / comb):.4f}% → **{pct(share[i])}**")
    if n == 0:
        warnings.append("NoChildren")
        basic, extrap = D(0), False
        s.say("- No child has a name: nothing is calculated (D6).")
    else:
        basic, where, extrap = lookup(comb, n)
        s.say(f"- Line 4: combined {money(comb)}, {n} {'child' if n == 1 else 'children'}: {where} → **{money(basic)}**")
        if comb == 0:
            warnings.append("ZeroIncome")
        if extrap:
            warnings.append("AboveSchedule")
    if not oct2025 and (get(0, "multiFamily") > 0 or get(1, "multiFamily") > 0):
        warnings.append("MultifamilyIgnored")

    s.row("1", money(get(0, "income")), money(get(1, "income")), money(get(0, "income") + get(1, "income")))
    s.row("1a", money(get(0, "existingSupport")), money(get(1, "existingSupport")))
    s.row("1b", money(get(0, "alimonyPaid")), money(get(1, "alimonyPaid")))
    s.row("1c", money(c1[0]), money(c1[1]))
    s.row("1d", money(d1[0]), money(d1[1]))
    s.row("2", money(adj[0]), money(adj[1]), money(comb))
    s.row("3", pct(share[0]), pct(share[1]))
    s.row("4", "", "", money(basic))

    keys = ["childCare", "healthInsurance", "extraordinaryMedical", None, "additionalExpenses"]
    letters = "abcde"

    def paid(i, k):
        return cm[i] if keys[k] is None else get(i, keys[k])

    computable = n > 0 and comb > 0
    result = {"payer": None, "amount": D(0), "reimbursement": False}

    def sole(custodial, record):
        total = basic + sum(paid(0, k) + paid(1, k) for k in range(5))
        ob = [rd(total * share[i] / 100) for i in range(2)]
        dp = [sum(paid(i, k) for k in range(5)) for i in range(2)]
        net = [ob[i] - dp[i] for i in range(2)]
        if record:
            s.say(f"- Line 5: {money(basic)} + " + " + ".join(money(paid(0, k) + paid(1, k)) for k in range(5)) + f" = **{money(total)}**")
            for i in range(2):
                s.say(f"- Line 6, {names[i]}: {money(total)} × {pct(share[i])} = {(total * share[i] / 100):.2f} → **{money(ob[i])}**")
            for i in range(2):
                s.say(f"- Line 7, {names[i]}: " + " + ".join(money(paid(i, k)) for k in range(5)) + f" = **{money(dp[i])}**")
            for i in range(2):
                s.say(f"- Line 8, {names[i]}: {money(ob[i])} − {money(dp[i])} = **{money(net[i])}**")
        return total, ob, dp, net

    custody = c.get("custody", "shared")
    if custody != "shared":
        custodial = 0 if custody == "soleWithParent1" else 1
        ncp = 1 - custodial
        for k in range(5):
            s.row("4" + letters[k], money(paid(0, k)), money(paid(1, k)), money(paid(0, k) + paid(1, k)))
        if computable:
            total, ob, dp, net = sole(custodial, True)
            s.row("5", "", "", money(total))
            s.row("6", money(ob[0]), money(ob[1]))
            s.row("7", money(dp[0]), money(dp[1]))
            s.row("8", money(net[0]), money(net[1]))
            l9 = net[ncp]
            s.row("9", money(l9) if ncp == 0 else "", money(l9) if ncp == 1 else "")
            s.say(f"- Line 9: non-custodial parent is {names[ncp]}: **{money(l9)}**")
            if l9 > 0:
                result = {"payer": ncp, "amount": l9, "reimbursement": False}
            elif l9 < 0:
                result = {"payer": custodial, "amount": -l9, "reimbursement": True}
                warnings.append("Reimbursement")
                s.say(f"- Line 9 is negative: Comment (2) — {names[custodial]} reimburses {names[ncp]} {money(-l9)}.")
        else:
            for num in "56789":
                s.row(num)
        return s, names, result, warnings, n

    # shared
    entered = int(c.get("parent1Overnights", 183))
    n1 = min(max(entered, 92), 273)
    nights = [n1, 365 - n1]
    if entered != n1:
        s.say(f"- Overnights entered {entered}: limited to {n1} (D7).")
    if not computable:
        s.row("5")
        s.row("6", str(nights[0]), str(nights[1]), "365")
        for num in ["7", "8", "9", "10", "11", "12"]:
            s.row(num)
        for k in range(5):
            s.row("13" + letters[k], money(paid(0, k)), money(paid(1, k)), money(paid(0, k) + paid(1, k)))
        for num in ["14", "15", "16"]:
            s.row(num)
        return s, names, result, warnings, n

    b5 = rd(D("1.5") * basic)
    s.say(f"- Line 5: {money(basic)} × 1.5 = {D('1.5') * basic} → **{money(b5)}**")
    s.say(f"- Line 6: {nights[0]} + {nights[1]} = 365")
    v = [rp(D(100) * nights[i] / 365) for i in range(2)]
    for i in range(2):
        s.say(f"- Line 7, {names[i]}: {nights[i]} ÷ 365 = {(D(100) * nights[i] / 365):.4f}% → **{pct(v[i])}**")
    e = [rd(b5 * share[i] / 100) for i in range(2)]
    for i in range(2):
        s.say(f"- Line 8, {names[i]}: {money(b5)} × {pct(share[i])} = {(b5 * share[i] / 100):.2f} → **{money(e[i])}**")
    th = [rd(e[0] * v[1] / 100), rd(e[1] * v[0] / 100)]
    s.say(f"- Line 9A: {money(e[0])} × {pct(v[1])} = {(e[0] * v[1] / 100):.2f} → **{money(th[0])}**")
    s.say(f"- Line 9B: {money(e[1])} × {pct(v[0])} = {(e[1] * v[0] / 100):.2f} → **{money(th[1])}**")

    def factor(nn):
        for lo, hi, f in [(92, 94, "0.10"), (95, 98, "0.08"), (99, 102, "0.06"), (103, 105, "0.04"), (106, 109, "0.02")]:
            if lo <= nn <= hi:
                return D(f)
        return None

    x10, x11, eff = ["", ""], ["", ""], []
    for i in range(2):
        f = factor(nights[i])
        if f is not None:
            a = rd(th[i] * f)
            x10[i] = money(a)
            x11[i] = money(th[i] + a)
            eff.append(th[i] + a)
            s.say(f"- Line 10, {names[i]}: {nights[i]} overnights → factor {f}: {money(th[i])} × {f} = {(th[i] * f):.2f} → **{money(a)}**; "
                  f"line 11 = {money(th[i])} + {money(a)} = **{money(th[i] + a)}**")
        else:
            eff.append(th[i])
    net = abs(eff[0] - eff[1])
    g = 0 if eff[0] > eff[1] else 1 if eff[1] > eff[0] else None
    s.say(f"- Line 12: |{money(eff[0])} − {money(eff[1])}| = **{money(net)}** under " + (names[g] if g is not None else "neither parent"))

    s.row("5", "", "", money(b5))
    s.row("6", str(nights[0]), str(nights[1]), "365")
    s.row("7", pct(v[0]), pct(v[1]))
    s.row("8", money(e[0]), money(e[1]))
    s.row("9", money(th[0]), money(th[1]))
    s.row("10", x10[0], x10[1])
    s.row("11", x11[0], x11[1])
    s.row("12", money(net) if g == 0 else "", money(net) if g == 1 else "", "$0" if g is None else "")
    for k in range(5):
        s.row("13" + letters[k], money(paid(0, k)), money(paid(1, k)), money(paid(0, k) + paid(1, k)))

    # Worksheet C
    ktot = [D(0), D(0)]
    any_exp = False
    crow = []
    cl = "abcdefghij"
    for k in range(5):
        t = paid(0, k) + paid(1, k)
        if t > 0:
            any_exp = True
            q = [rd(t * share[i] / 100) for i in range(2)]
            z = [max(D(0), paid(i, k) - q[i]) for i in range(2)]
            ktot = [ktot[i] + z[i] for i in range(2)]
            crow.append(("C" + cl[2 * k], money(q[0]), money(q[1]), ""))
            crow.append(("C" + cl[2 * k + 1], money(z[0]), money(z[1]), ""))
            for i in range(2):
                s.say(f"- Worksheet C line {cl[2*k]}/{cl[2*k+1]} (13{letters[k]}), {names[i]}: {money(t)} × {pct(share[i])} = {money(q[i])}; "
                      f"paid {money(paid(i, k))} − {money(q[i])} → excess **{money(z[i])}**")
        else:
            crow.append(("C" + cl[2 * k], "", "", ""))
            crow.append(("C" + cl[2 * k + 1], "", "", ""))
    lval = abs(ktot[0] - ktot[1])
    over = 0 if ktot[0] > ktot[1] else 1 if ktot[1] > ktot[0] else None
    crow.append(("Ck", money(ktot[0]), money(ktot[1]), ""))
    crow.append(("Cl", money(lval) if over == 0 else "", money(lval) if over == 1 else "", "$0" if over is None else ""))
    if any_exp:
        s.say(f"- Worksheet C line k: {money(ktot[0])} / {money(ktot[1])}; line l = **{money(lval)}** under "
              + (names[over] if over is not None else "neither parent") + " (owed to that parent)")

    s.row("14", money(lval) if any_exp and over == 0 else "", money(lval) if any_exp and over == 1 else "",
          "$0" if any_exp and over is None else "")
    s.row("15", money(net) if g == 0 else "", money(net) if g == 1 else "", "$0" if g is None else "")

    dval = (net if g == 0 else -net if g == 1 else D(0)) + (lval if over == 1 else -lval if over == 0 else D(0))
    computed = abs(dval)
    owing = 0 if dval > 0 else 1 if dval < 0 else None
    final = computed
    s.say(f"- Line 16: amount {names[0]} owes {names[1]} = (line 15) {('+' if g == 0 else '−') if g is not None else ''}{money(net)} "
          f"{'+' if over == 1 else '−'} (line 14) {money(lval) if any_exp else '$0'} = {money(dval)}")
    if owing is not None:
        _, ob, dp, netA = sole(1 - owing, False)
        cap = max(D(0), netA[owing])
        s.say(f"- §12-204(m)(5) check: Worksheet A with {names[owing]} as non-custodial parent: line 6 {money(ob[owing])} − line 7 "
              f"{money(dp[owing])} = {money(netA[owing])}; limit {money(cap)}")
        if computed > cap:
            final = cap
            warnings.append("SharedLimit")
            s.say(f"- **Limit applies**: {money(computed)} → **{money(cap)}**")
        if final > 0:
            result = {"payer": owing, "amount": final, "reimbursement": False}
    s.row("16", money(final) if owing == 0 else "", money(final) if owing == 1 else "", "$0" if owing is None else "")
    if any_exp:
        s.rows.extend(crow)
    return s, names, result, warnings, n


# ---------------------------------------------------------------- cases

def kids(n):
    rows = [{"name": f"Child {i + 1} Example", "birthDate": f"0{i + 1}/15/20{14 + i}"} for i in range(n)]
    return rows + [{"name": "", "birthDate": ""} for _ in range(6 - n)]


def parent(**kw):
    base = dict(income=0, multiFamily=0, existingSupport=0, alimonyPaid=0, childCare=0, healthInsurance=0,
                extraordinaryMedical=0, cashMedicalPercent=0, additionalExpenses=0)
    base.update(kw)
    return base


def case(title, purpose, custody="shared", children=2, p1=None, p2=None, alimony=0, overnights=183,
         guidelines="october2025", labels=False):
    return dict(title=title, purpose=purpose, case=dict(
        caseNumber="TEST-" + title.split(" ")[0], county="Anne Arundel County", court="circuit",
        plaintiff="Jordan Example", defendant="Riley Example", preparer="Test", date="11/17/2026",
        useParentLabels=labels, guidelines=guidelines, custody=custody, children=kids(children),
        parent1=p1 or parent(), parent2=p2 or parent(), alimonyToParent1=alimony, parent1Overnights=overnights,
        comments="", footnotes=[]))


def shared_on(nights, p1_income, p2_income, **kw):
    return case(f"S{nights:03d} shared, Parent 1 has {nights} overnights",
                f"Shared custody at {nights} overnights for Parent 1 (§12-204(m)(2)(ii) band check).",
                p1=parent(income=p1_income, **kw.get("e1", {})), p2=parent(income=p2_income, **kw.get("e2", {})),
                overnights=nights, children=kw.get("children", 2))


CASES = [
    case("A01 sole with Parent 1, alimony to Parent 1",
         "Worksheet A, custody with Mother; alimony awarded in this case to the first parent; pre-existing support "
         "paid by Father; Mother pays child care and extraordinary medical, Father pays health insurance and additional expenses.",
         custody="soleWithParent1", p1=parent(income=4200, childCare=600, extraordinaryMedical=40),
         p2=parent(income=6100, existingSupport=350, healthInsurance=210, additionalExpenses=75), alimony=300),
    case("A02 sole with Parent 2, alimony to Parent 2",
         "Worksheet A, custody with Father; alimony awarded to the second parent (negative entry); Father pays child care "
         "and extraordinary medical; Mother pays health insurance, additional expenses and alimony in another case.",
         custody="soleWithParent2", p1=parent(income=7350, alimonyPaid=400, healthInsurance=180, additionalExpenses=120),
         p2=parent(income=3100, childCare=450, extraordinaryMedical=60), alimony=-500, children=1),
    case("A03 sole, direct payments exceed obligation",
         "Worksheet A where the non-custodial parent's direct payments exceed that parent's share: line 9 negative, "
         "Comment (2) reimbursement by the custodial parent (D12).",
         custody="soleWithParent1", p1=parent(income=5000), p2=parent(income=2000, childCare=900, healthInsurance=400),
         children=1),
    case("A04 sole, cash medical support",
         "Cash medical support entered as a percentage for each parent, including one above the 5% limit (D14).",
         custody="soleWithParent2", p1=parent(income=3800, cashMedicalPercent=D("2.5")),
         p2=parent(income=4400, cashMedicalPercent=D("6.0")), children=3),
    case("A05 income exactly on a schedule row",
         "Combined adjusted income exactly $5,000 uses that row (§12-204(c)).",
         custody="soleWithParent1", p1=parent(income=2000), p2=parent(income=3000)),
    case("A06 income just above a schedule row",
         "Combined adjusted income $5,001 uses the next higher row, $5,050 (§12-204(c)).",
         custody="soleWithParent1", p1=parent(income=2000), p2=parent(income=3001)),
    case("A07 top of the schedule",
         "Combined adjusted income exactly $30,000: the last schedule row, not extrapolated.",
         custody="soleWithParent2", p1=parent(income=18000), p2=parent(income=12000), children=4),
    case("A08 above the schedule",
         "Combined adjusted income $31,234: advisory extrapolation (§12-204(d), D4).",
         custody="soleWithParent2", p1=parent(income=20000), p2=parent(income=11234), children=2),
    case("A09 zero income",
         "Both parents have $0 income: shares cannot be determined, nothing owed (D3).",
         custody="soleWithParent1"),
    case("A10 negative adjusted income",
         "Father's deductions exceed his income: line 2 negative, counted as $0 (D11).",
         custody="soleWithParent1", p1=parent(income=3000), p2=parent(income=1500, existingSupport=900, alimonyPaid=800)),
    case("A11 multifamily allowance, October 2025",
         "Father has 2 additional children at home; allowance from the multifamily calculator (D13): schedule at $6,100, "
         "2 children = $1,485 × 75% = $1,114.",
         custody="soleWithParent1", p1=parent(income=4200), p2=parent(income=6100, multiFamily=1114), labels=True),
    case("A12 same case under July 2022",
         "The A11 inputs under the July 2022 guidelines: the multifamily amount is not applied (D15).",
         custody="soleWithParent1", p1=parent(income=4200), p2=parent(income=6100, multiFamily=1114),
         guidelines="july2022", labels=True),
    case("A13 no children entered",
         "No child names: nothing calculated (D6).",
         custody="soleWithParent1", p1=parent(income=4000), p2=parent(income=5000), children=0),
    shared_on(92, 9000, 2500, e1={"healthInsurance": 250}, e2={"childCare": 300}),
    shared_on(94, 4000, 6000),
    shared_on(95, 5200, 5200, e1={"extraordinaryMedical": 90}),
    shared_on(98, 3000, 7500, e2={"additionalExpenses": 140}),
    shared_on(99, 6400, 4100),
    shared_on(102, 2500, 2500, children=3),
    shared_on(103, 8000, 3000, e1={"childCare": 700}),
    shared_on(105, 4500, 4500),
    shared_on(106, 12000, 6000, e2={"healthInsurance": 320, "cashMedicalPercent": D("1.0")}),
    shared_on(109, 3300, 9900),
    shared_on(110, 5000, 5000),
    shared_on(183, 4200, 6100, e1={"childCare": 600, "extraordinaryMedical": 40},
              e2={"existingSupport": 350, "healthInsurance": 210, "additionalExpenses": 75}),
    case("B01 shared, §12-204(m)(5) limit applies",
         "High-income parent with 92 overnights: the shared amount exceeds the Worksheet A amount and is limited (D10).",
         p1=parent(income=10000), p2=parent(income=2000), overnights=92, children=1),
    case("B02 shared, overnights entered below 92",
         "Parent 1 overnights entered as 60: limited to 92 with a caution (D7).",
         p1=parent(income=6000), p2=parent(income=3000), overnights=60),
    case("B03 shared, equal incomes and time",
         "Equal incomes, 183/182 overnights: small net amount; symmetry check.",
         p1=parent(income=5000), p2=parent(income=5000), overnights=183, children=1),
]


def jsonable(o):
    if isinstance(o, D):
        return float(o) if o != o.to_integral_value() else int(o)
    raise TypeError(type(o))


def write(spec):
    c = spec["case"]
    s, names, result, warnings, n = calculate(c)
    slug = spec["title"].split(" ")[0]
    sheet = "A" if c["custody"] != "shared" else "B"
    lines = [f"# {spec['title']}", "", spec["purpose"], "",
             f"Worksheet {sheet}. Generated by `tests/cases/tools/worked.py` (an implementation independent of the C# "
             "engine) from `docs/calculation.md`; asserted by `WorkedCaseTests`.", "",
             "## Inputs", "", "```json",
             json.dumps({"schemaVersion": 1, "case": c}, indent=2, default=jsonable, ensure_ascii=False), "```", "",
             "## Arithmetic", ""]
    lines += s.steps
    lines += ["", "## Expected", "", f"| Line | {names[0]} | {names[1]} | Combined |", "|---|---|---|---|"]
    lines += [f"| {r[0]} | {r[1]} | {r[2]} | {r[3]} |" for r in s.rows]
    if result["payer"] is None:
        res = "none"
    else:
        res = f"{'Parent1' if result['payer'] == 0 else 'Parent2'} {money(result['amount'])}" + (" reimbursement" if result["reimbursement"] else "")
    lines += ["", f"Result: {res}", "", "Warnings: " + (", ".join(sorted(set(warnings))) or "none"), ""]
    with open(os.path.join(OUT, slug + ".md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))
    return slug, res


if __name__ == "__main__":
    for spec in CASES:
        print(*write(spec))
