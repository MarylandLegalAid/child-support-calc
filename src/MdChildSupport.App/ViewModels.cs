using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using MdChildSupport.Core;

namespace MdChildSupport.App;

/// <summary>
/// View model behind MainWindow.xaml. Inputs write straight into a <see cref="CaseData"/>; every edit recalculates
/// and raises change notifications for the computed properties only, never for the input being edited (so the
/// caret does not jump).
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private CaseData _case;
    private CalculationResult _result;

    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Raised when <see cref="IsDirty"/> changes.</summary>
    public event EventHandler? DirtyChanged;

    public MainViewModel()
    {
        _case = CaseData.CreateNew(DateTime.Today);
        _result = Calculator.Calculate(_case);
        Mother = new ParentViewModel(this, Parent.Parent1);
        Father = new ParentViewModel(this, Parent.Parent2);
        RebuildChildren();
    }

    internal CaseData Case => _case;
    public CalculationResult Result => _result;
    public bool IsDirty { get; private set; }

    // ------------------------------------------------------------------ load / save

    public void Load(CaseData data)
    {
        _case = data;
        Mother = new ParentViewModel(this, Parent.Parent1);
        Father = new ParentViewModel(this, Parent.Parent2);
        RebuildChildren();
        _result = Calculator.Calculate(_case);
        SetDirty(false);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>A copy of the case for saving or printing, with footnote text brought up to date.</summary>
    public CaseData Snapshot()
    {
        Footnotes.Refresh(_case);
        return _case.Clone();
    }

    public void MarkSaved() => SetDirty(false);

    private void SetDirty(bool value)
    {
        if (IsDirty == value) return;
        IsDirty = value;
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildChildren()
    {
        Children.Clear();
        for (int i = 0; i < CaseData.ChildRows; i++) Children.Add(new ChildViewModel(this, _case.Children[i], i + 1));
    }

    internal void Edited()
    {
        _result = Calculator.Calculate(_case);
        SetDirty(true);
        foreach (string name in ComputedProperties) Raise(name);
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static readonly string[] ComputedProperties =
    {
        nameof(IsMultiFamily), nameof(IsShared), nameof(MotherHeader), nameof(FatherHeader), nameof(AlimonyOppositeText),
        nameof(FatherOvernightsText), nameof(OvernightsNote), nameof(HasOvernightsNote), nameof(MotherCashMedicalDollars),
        nameof(FatherCashMedicalDollars), nameof(RecommendedPayerText), nameof(RecommendedAmountText), nameof(RecommendedAmountNote),
        nameof(HasRecommendedAmount), nameof(MotherAdjusted), nameof(FatherAdjusted), nameof(MotherShare),
        nameof(FatherShare), nameof(MotherRecommended), nameof(FatherRecommended), nameof(CombinedIncome),
        nameof(CombinedAdjusted), nameof(ChildCountText), nameof(BasicObligation), nameof(Warnings), nameof(HasWarnings),
    };

    private bool SetText(string current, string? value, Action<string> assign)
    {
        value ??= "";
        if (current == value) return false;
        assign(value);
        Edited();
        return true;
    }

    internal static decimal ToDollars(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? 0m : Money.Dollars((decimal)Math.Clamp(value, -1e12, 1e12));

    // ------------------------------------------------------------------ caption

    public string CaseNumber { get => _case.CaseNumber; set => SetText(_case.CaseNumber, value, v => _case.CaseNumber = v); }
    public string Plaintiff { get => _case.Plaintiff; set => SetText(_case.Plaintiff, value, v => _case.Plaintiff = v); }
    public string Defendant { get => _case.Defendant; set => SetText(_case.Defendant, value, v => _case.Defendant = v); }
    public string Preparer { get => _case.Preparer; set => SetText(_case.Preparer, value, v => _case.Preparer = v); }
    public string Date { get => _case.Date; set => SetText(_case.Date, value, v => _case.Date = v); }

    public IReadOnlyList<string> CountyNames => Counties.All;

    public int CountyIndex
    {
        get => Math.Max(0, Counties.All.ToList().IndexOf(_case.County));
        set
        {
            if (value < 0 || value >= Counties.All.Count || Counties.All[value] == _case.County) return;
            _case.County = Counties.All[value];
            Edited();
        }
    }

    public int CourtIndex
    {
        get => (int)_case.Court;
        set
        {
            if (value < 0 || value > 1 || value == (int)_case.Court) return;
            _case.Court = (CourtType)value;
            Edited();
        }
    }

    public bool UseParentLabels
    {
        get => _case.UseParentLabels;
        set
        {
            if (value == _case.UseParentLabels) return;
            _case.UseParentLabels = value;
            Edited();
        }
    }

    public int GuidelinesIndex
    {
        get => (int)_case.Guidelines;
        set
        {
            if (value < 0 || value > 1 || value == (int)_case.Guidelines) return;
            _case.Guidelines = (GuidelinesVersion)value;
            Edited();
        }
    }

    public int CustodyIndex
    {
        get => (int)_case.Custody;
        set
        {
            if (value < 0 || value > 2 || value == (int)_case.Custody) return;
            _case.Custody = (CustodyType)value;
            Edited();
        }
    }

    public bool IsMultiFamily => _case.Guidelines == GuidelinesVersion.October2025;
    public bool IsShared => _case.Custody == CustodyType.Shared;

    public string MotherHeader => Calculator.ParentName(_case, Parent.Parent1);
    public string FatherHeader => Calculator.ParentName(_case, Parent.Parent2);

    // ------------------------------------------------------------------ inputs

    public ObservableCollection<ChildViewModel> Children { get; } = new();
    public ParentViewModel Mother { get; private set; }
    public ParentViewModel Father { get; private set; }

    public ParentViewModel ParentVm(Parent p) => p == Parent.Parent1 ? Mother : Father;

    public double AlimonyToMother
    {
        get => (double)_case.AlimonyToParent1;
        set
        {
            decimal d = ToDollars(value);
            if (d == _case.AlimonyToParent1) return;
            _case.AlimonyToParent1 = d;
            Edited();
        }
    }

    public string AlimonyOppositeText => Money.Format(-_case.AlimonyToParent1) + " (opposite amount)";

    /// <summary>Parent 1's overnights as used (92–273). The entered value is kept in the case (DECISIONS D7).</summary>
    public double MotherOvernights
    {
        get => Calculator.EffectiveOvernights(_case.Parent1Overnights);
        set
        {
            if (double.IsNaN(value)) return;
            int v = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (Calculator.EffectiveOvernights(_case.Parent1Overnights) == v) return;
            _case.Parent1Overnights = v;
            Edited();
        }
    }

    /// <summary>
    /// Called from the window with the text the user typed into the overnights box, before the NumberBox limits it to
    /// 92–273, so an out-of-range entry is recorded and the caution can be shown (P5).
    /// </summary>
    public void NoteTypedOvernights(string text)
    {
        string s = (text ?? "").Replace(",", "").Trim();
        if (!int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int typed)) return;
        typed = Math.Clamp(typed, -1, 10000);
        // Leaving the box without changing what it shows keeps the entered value (and any caution).
        if (typed == _case.Parent1Overnights || typed == Calculator.EffectiveOvernights(_case.Parent1Overnights)) return;
        _case.Parent1Overnights = typed;
        Edited();
    }

    public string FatherOvernightsText => (365 - Calculator.EffectiveOvernights(_case.Parent1Overnights)).ToString(CultureInfo.InvariantCulture);

    public string OvernightsNote => IsShared ? _result.OvernightsNote ?? "" : "";
    public bool HasOvernightsNote => OvernightsNote.Length > 0;

    public string Comments
    {
        get => _case.Comments;
        set => SetText(_case.Comments, value, v => _case.Comments = v);
    }

    // ------------------------------------------------------------------ multifamily calculator

    public MultiFamilyAllowance PreviewMultiFamily(Parent p, int children) => MultiFamily.Calculate(_case.Inputs(p).Income, children);

    public void ApplyMultiFamily(Parent p, int children)
    {
        string field = FieldIds.ForParent(p, FieldIds.MultiFamily);
        var a = PreviewMultiFamily(p, children);
        _case.Inputs(p).MultiFamily = a.Allowance;
        if (children <= 0) _case.SetFootnote(field, null);
        else
        {
            _case.SetFootnote(field, a.Footnote(Calculator.ParentName(_case, p)));
            _case.Footnotes.Single(f => f.Field == field).MultiFamilyChildren = children;
        }
        ParentVm(p).RaiseMultiFamily();
        Edited();
    }

    public void ClearMultiFamily(Parent p)
    {
        _case.Inputs(p).MultiFamily = 0;
        _case.SetFootnote(FieldIds.ForParent(p, FieldIds.MultiFamily), null);
        ParentVm(p).RaiseMultiFamily();
        Edited();
    }

    /// <summary>
    /// Keep a calculator-made multifamily amount in step with the parent's income, unless the preparer has typed a
    /// different amount since.
    /// </summary>
    internal void IncomeChanged(Parent p, decimal oldIncome)
    {
        var note = _case.Footnotes.FirstOrDefault(f => f.Field == FieldIds.ForParent(p, FieldIds.MultiFamily));
        if (note?.MultiFamilyChildren is not int k) return;
        var inputs = _case.Inputs(p);
        if (inputs.MultiFamily != MultiFamily.Calculate(oldIncome, k).Allowance) return;
        inputs.MultiFamily = MultiFamily.Calculate(inputs.Income, k).Allowance;
        ParentVm(p).RaiseMultiFamily();
    }

    public bool HasMultiFamilyNote(Parent p) =>
        _case.Footnotes.Any(f => f.Field == FieldIds.ForParent(p, FieldIds.MultiFamily));

    // ------------------------------------------------------------------ results

    public string MotherCashMedicalDollars => Money.Format(_result.CashMedicalDollars[0]);
    public string FatherCashMedicalDollars => Money.Format(_result.CashMedicalDollars[1]);

    public string RecommendedPayerText
    {
        get
        {
            if (_result.Payer is not Parent payer) return "No child support owed by either parent";
            string name = _result.ParentName(payer);
            if (_result.IsReimbursement)
                return $"{name} reimburses {_result.ParentName(payer == Parent.Parent1 ? Parent.Parent2 : Parent.Parent1)} (direct-pay expenses)";
            return name + " pays" + (_result.IsAdvisory ? " (advisory)" : "");
        }
    }

    public string RecommendedAmountText => Money.Format(_result.Amount);

    /// <summary>Hover note on the amount when the FL §12-204(m)(5) limit lowered it (DECISIONS D10); empty otherwise.</summary>
    public string RecommendedAmountNote => _result.AmountBeforeLimit is decimal before
        ? $"Limited by FL §12-204(m)(5). Without the limit, the shared-custody calculation gives {Money.Format(before)}; " +
          $"{_result.ParentName(_result.Payer!.Value)} would owe {Money.Format(_result.Amount)} as the non-custodial parent on Worksheet A."
        : "";
    public bool HasRecommendedAmount => _result.Payer is not null;

    public string MotherAdjusted => Money.Format(_result.AdjustedIncome[0]);
    public string FatherAdjusted => Money.Format(_result.AdjustedIncome[1]);
    public string MotherShare => Money.FormatPercent(_result.SharePercent[0]);
    public string FatherShare => Money.FormatPercent(_result.SharePercent[1]);
    public string MotherRecommended => Money.Format(_result.OwedBy(Parent.Parent1));
    public string FatherRecommended => Money.Format(_result.OwedBy(Parent.Parent2));

    public string CombinedIncome => Money.Format(_result.CombinedActualIncome);
    public string CombinedAdjusted => Money.Format(_result.CombinedAdjustedIncome);
    public string ChildCountText => _result.ChildCount == 1 ? "1 child" : $"{_result.ChildCount} children";
    public string BasicObligation => Money.Format(_result.Basic.Amount) + (_result.Basic.Extrapolated ? " (extrapolated)" : "");

    public string Warnings => string.Join("\n\n", _result.Warnings.Select(w => w.Text));
    public bool HasWarnings => _result.Warnings.Count > 0;
}

/// <summary>One parent's input column. Values are doubles because NumberBox binds to double; NaN (blank) means 0.</summary>
public sealed class ParentViewModel : INotifyPropertyChanged
{
    private readonly MainViewModel _owner;
    private readonly Parent _parent;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal ParentViewModel(MainViewModel owner, Parent parent)
    {
        _owner = owner;
        _parent = parent;
    }

    private ParentInputs P => _owner.Case.Inputs(_parent);

    private void SetMoney(decimal current, double value, Action<decimal> assign)
    {
        decimal d = Math.Max(0m, MainViewModel.ToDollars(value));
        if (d == current) return;
        assign(d);
        _owner.Edited();
    }

    internal void RaiseMultiFamily() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MultiFamily)));

    public double Income
    {
        get => (double)P.Income;
        set
        {
            decimal old = P.Income;
            decimal d = Math.Max(0m, MainViewModel.ToDollars(value));
            if (d == old) return;
            P.Income = d;
            _owner.IncomeChanged(_parent, old);
            _owner.Edited();
        }
    }

    public double MultiFamily { get => (double)P.MultiFamily; set => SetMoney(P.MultiFamily, value, v => P.MultiFamily = v); }
    public double ExistingSupport { get => (double)P.ExistingSupport; set => SetMoney(P.ExistingSupport, value, v => P.ExistingSupport = v); }
    public double AlimonyPaid { get => (double)P.AlimonyPaid; set => SetMoney(P.AlimonyPaid, value, v => P.AlimonyPaid = v); }
    public double ChildCare { get => (double)P.ChildCare; set => SetMoney(P.ChildCare, value, v => P.ChildCare = v); }
    public double HealthInsurance { get => (double)P.HealthInsurance; set => SetMoney(P.HealthInsurance, value, v => P.HealthInsurance = v); }
    public double ExtraordinaryMedical { get => (double)P.ExtraordinaryMedical; set => SetMoney(P.ExtraordinaryMedical, value, v => P.ExtraordinaryMedical = v); }
    public double AdditionalExpenses { get => (double)P.AdditionalExpenses; set => SetMoney(P.AdditionalExpenses, value, v => P.AdditionalExpenses = v); }

    public double CashMedicalPercent
    {
        get => (double)P.CashMedicalPercent;
        set
        {
            decimal d = double.IsNaN(value) ? 0m : Money.Percent((decimal)Math.Clamp(value, 0, 100));
            if (d == P.CashMedicalPercent) return;
            P.CashMedicalPercent = d;
            _owner.Edited();
        }
    }
}

/// <summary>One of the six child rows.</summary>
public sealed class ChildViewModel
{
    private readonly MainViewModel _owner;
    private readonly ChildInfo _child;

    internal ChildViewModel(MainViewModel owner, ChildInfo child, int number)
    {
        _owner = owner;
        _child = child;
        Number = number.ToString(CultureInfo.InvariantCulture);
    }

    public string Number { get; }

    public string Name
    {
        get => _child.Name;
        set
        {
            value ??= "";
            if (value == _child.Name) return;
            _child.Name = value;
            _owner.Edited();
        }
    }

    public string BirthDate
    {
        get => _child.BirthDate;
        set
        {
            value ??= "";
            if (value == _child.BirthDate) return;
            _child.BirthDate = value;
            _owner.Edited();
        }
    }
}
