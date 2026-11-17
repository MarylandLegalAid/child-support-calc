using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace MdChildSupport.Core.Pdf;

public sealed class PdfOptions
{
    public string AppName { get; init; } = "Maryland Child Support Calculator";
    public string Version { get; init; } = typeof(PdfOptions).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    /// <summary>Configurable footer line, e.g. "Prepared by …"; set by the brand folder's settings file.</summary>
    public string FooterLine { get; init; } = "";
}

/// <summary>
/// Draws the worksheet PDF in the layout of the official CC-DR-034 (Worksheet A) / CC-DR-035 (Worksheets B and C):
/// same line numbers, captions and order. Letter size, vector text.
/// </summary>
public sealed class WorksheetPdf
{
    private const double PageW = 612, PageH = 792, Margin = 40, FooterH = 30;
    private const double NumW = 26, OpW = 22, ValW = 74;
    private const double Left = Margin, Right = PageW - Margin;
    private const double ValuesLeft = Right - 3 * ValW;
    private const double CaptionLeft = Left + NumW;
    private const double CaptionW = ValuesLeft - OpW - CaptionLeft - 6;

    private readonly CaseData _case;
    private readonly CalculationResult _r;
    private readonly PdfOptions _opt;
    private readonly PdfDocument _doc = new();
    private readonly XFont _regular, _bold, _small, _smallBold, _title, _footer;
    private XGraphics? _g;
    private double _y;
    private readonly Dictionary<string, List<int>> _markers = new(); // line number -> footnote numbers
    private readonly List<(int Number, string Line, string Text)> _footnotes = new();

    private WorksheetPdf(CaseData c, CalculationResult r, PdfOptions opt)
    {
        WindowsFontResolver.EnsureInstalled();
        _case = c; _r = r; _opt = opt;
        var unicode = new XPdfFontOptions(PdfFontEncoding.Unicode);
        _regular = new XFont(WindowsFontResolver.FamilyName, 8.5, XFontStyleEx.Regular, unicode);
        _bold = new XFont(WindowsFontResolver.FamilyName, 8.5, XFontStyleEx.Bold, unicode);
        _small = new XFont(WindowsFontResolver.FamilyName, 7.5, XFontStyleEx.Regular, unicode);
        _smallBold = new XFont(WindowsFontResolver.FamilyName, 7.5, XFontStyleEx.Bold, unicode);
        _title = new XFont(WindowsFontResolver.FamilyName, 11, XFontStyleEx.Bold, unicode);
        _footer = new XFont(WindowsFontResolver.FamilyName, 7, XFontStyleEx.Regular, unicode);
    }

    public static void Write(CaseData c, Stream output, PdfOptions? options = null)
    {
        WindowsFontResolver.EnsureInstalled(); // before any PDFsharp object exists (licence and fonts)
        var r = Calculator.Calculate(c);
        new WorksheetPdf(c, r, options ?? new PdfOptions()).Render(output);
    }

    public static void Write(CaseData c, string path, PdfOptions? options = null)
    {
        using var fs = File.Create(path);
        Write(c, fs, options);
    }

    public static string Title(CalculationResult r) => r.Kind == WorksheetKind.A
        ? "WORKSHEET A - CHILD SUPPORT OBLIGATION: PRIMARY PHYSICAL CUSTODY"
        : "WORKSHEET B - CHILD SUPPORT OBLIGATION: SHARED PHYSICAL CUSTODY";

    // ------------------------------------------------------------------ rendering

    private void Render(Stream output)
    {
        _doc.Info.Title = Title(_r) + (_case.CaseNumber.Length > 0 ? " — " + _case.CaseNumber : "");
        _doc.Info.Creator = $"{_opt.AppName} {_opt.Version}";
        CollectFootnotes();
        NewPage(first: true);
        Caption();
        Children();
        ColumnHeader();
        foreach (var line in _r.Lines) Row(line);
        if (_r.WorksheetC.Count > 0)
        {
            Space(10);
            Heading("WORKSHEET C - FOR ADJUSTMENTS, LINE 12, WORKSHEET B");
            Paragraph("Used because at least one line 13 expense was entered. Proportionate shares use line 3.", _small);
            ColumnHeader(combined: false);
            foreach (var line in _r.WorksheetC) Row(line);
        }
        Comments();
        Notes();
        Signature();
        _g?.Dispose();
        _g = null;
        Footers();
        _doc.Save(output, false);
    }

    private void NewPage(bool first = false)
    {
        _g?.Dispose();
        var page = _doc.AddPage();
        page.Size = PageSize.Letter;
        _g = XGraphics.FromPdfPage(page);
        _y = Margin;
        if (!first)
        {
            Text($"Case No. {_case.CaseNumber}", _small, Left, _y);
            TextRight(Title(_r) + " (continued)", _small, Right, _y);
            _y += 12;
            Rule(0.5);
            _y += 6;
        }
    }

    private XGraphics G => _g!;

    private void Ensure(double height)
    {
        if (_y + height > PageH - Margin - FooterH) NewPage();
    }

    private void Space(double h) => _y += h;

    private void Rule(double width = 0.75, double? from = null, double? to = null) =>
        G.DrawLine(new XPen(XColors.Black, width), from ?? Left, _y, to ?? Right, _y);

    private void Text(string s, XFont f, double x, double y) =>
        G.DrawString(s, f, XBrushes.Black, new XRect(x, y, PageW, 20), XStringFormats.TopLeft);

    private void TextRight(string s, XFont f, double right, double y) =>
        G.DrawString(s, f, XBrushes.Black, new XRect(right - 400, y, 400, 20), XStringFormats.TopRight);

    private void TextCenter(string s, XFont f, double y) =>
        G.DrawString(s, f, XBrushes.Black, new XRect(Left, y, Right - Left, 20), XStringFormats.TopCenter);

    private static double LineHeight(XFont f) => f.Size * 1.3;

    /// <summary>Word-wrap to a width; words longer than the width are broken.</summary>
    internal List<string> Wrap(string text, XFont f, double width)
    {
        var result = new List<string>();
        foreach (string para in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            string current = "";
            foreach (string word0 in para.Split(' '))
            {
                string word = word0;
                while (G.MeasureString(word, f).Width > width && word.Length > 1)
                {
                    if (current.Length > 0) { result.Add(current); current = ""; }
                    int n = word.Length - 1;
                    while (n > 1 && G.MeasureString(word[..n], f).Width > width) n--;
                    result.Add(word[..n]);
                    word = word[n..];
                }
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (G.MeasureString(candidate, f).Width <= width) current = candidate;
                else { result.Add(current); current = word; }
            }
            result.Add(current);
        }
        return result;
    }

    private void Paragraph(string text, XFont f, double indent = 0)
    {
        foreach (string l in Wrap(text, f, Right - Left - indent))
        {
            Ensure(LineHeight(f));
            Text(l, f, Left + indent, _y);
            _y += LineHeight(f);
        }
    }

    private void Heading(string text)
    {
        Ensure(30);
        Text(text, _bold, Left, _y);
        _y += LineHeight(_bold) + 2;
    }

    // ------------------------------------------------------------------ sections

    private void Caption()
    {
        string county = _case.County.ToUpperInvariant();
        string court = _case.Court == CourtType.Circuit
            ? $"CIRCUIT COURT FOR {county}, MARYLAND"
            : $"DISTRICT COURT OF MARYLAND FOR {county}";
        TextCenter(court, _title, _y);
        _y += 20;

        double mid = (Left + Right) / 2;
        double top = _y;
        double half = mid - Left - 20;
        var plaintiff = Wrap(Blank(_case.Plaintiff), _bold, half);
        var defendant = Wrap(Blank(_case.Defendant), _bold, half);
        double y = top;
        foreach (string l in plaintiff) { Text(l, _bold, Left, y); y += LineHeight(_bold); }
        Text("Plaintiff", _small, Left, y); y += 14;
        Text("vs.", _regular, Left, y); y += 14;
        foreach (string l in defendant) { Text(l, _bold, Left, y); y += LineHeight(_bold); }
        Text("Defendant", _small, Left, y); y += 12;

        Text("Case No.", _small, mid + 20, top);
        foreach (var (l, i) in Wrap(Blank(_case.CaseNumber), _bold, Right - mid - 70).Select((l, i) => (l, i)))
            Text(l, _bold, mid + 60, top + i * LineHeight(_bold));
        _y = y + 6;

        TextCenter(Title(_r), _title, _y);
        _y += 15;
        TextCenter(_r.Kind == WorksheetKind.A ? "(Md. Rule 9-206(c))" : "(Md. Rule 9-206(d))", _small, _y);
        _y += 13;
        string guidelines = _r.Guidelines == GuidelinesVersion.October2025
            ? "Guidelines: October 2025 (includes the multifamily adjustment, Code, Family Law Article, § 12-201(c)(1)(iii))"
            : "Guidelines: July 2022 (law before October 1, 2025; no multifamily adjustment)";
        string custody = _r.Custody switch
        {
            CustodyType.SoleWithParent1 => $"Primary physical custody: {_r.Parent1Name}",
            CustodyType.SoleWithParent2 => $"Primary physical custody: {_r.Parent2Name}",
            _ => "Shared physical custody",
        };
        TextCenter($"{guidelines}  ·  {custody}", _small, _y);
        _y += 14;
        Rule();
        _y += 6;
    }

    private static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? "________________" : s.Trim();

    private void Children()
    {
        var kids = _case.Children.Where(c => !string.IsNullOrWhiteSpace(c.Name) || !string.IsNullOrWhiteSpace(c.BirthDate)).ToList();
        double dobX = Right - 110;
        Text("Name of Child", _smallBold, Left, _y);
        Text("Date of Birth", _smallBold, dobX, _y);
        _y += LineHeight(_smallBold);
        if (kids.Count == 0)
        {
            Text("(none entered)", _regular, Left, _y);
            _y += LineHeight(_regular);
        }
        foreach (var k in kids)
        {
            var nameLines = Wrap(k.Name.Trim(), _regular, dobX - Left - 10);
            Ensure(nameLines.Count * LineHeight(_regular));
            double y0 = _y;
            foreach (string l in nameLines) { Text(l, _regular, Left, _y); _y += LineHeight(_regular); }
            Text(k.BirthDate.Trim(), _regular, dobX, y0);
        }
        _y += 4;
        Text($"Children counted for the schedule: {_r.ChildCount}", _small, Left, _y);
        _y += 12;
    }

    private void ColumnHeader(bool combined = true)
    {
        Ensure(40);
        _y += 4;
        string[] heads = { _r.Parent1Name, _r.Parent2Name, combined ? "Combined" : "" };
        for (int c = 0; c < 3; c++)
            G.DrawString(heads[c], _bold, XBrushes.Black, new XRect(ValuesLeft + c * ValW, _y, ValW - 4, 12), XStringFormats.TopRight);
        _y += 13;
        Rule(0.75);
    }

    private void Row(WorksheetLine line)
    {
        XFont capFont = line.IsMain ? _bold : _regular;
        string num = line.Number.StartsWith('C') ? line.Number[1..] + "." : line.Number.Length > 1 && char.IsLetter(line.Number[^1]) ? line.Number[^1] + "." : line.Number + ".";
        double capLeft = line.IsMain ? CaptionLeft : CaptionLeft + 10;
        var capLines = Wrap(line.Caption, capFont, CaptionW - (capLeft - CaptionLeft));
        double h = Math.Max(capLines.Count * LineHeight(capFont), LineHeight(_regular)) + 5;
        Ensure(h);
        double top = _y + 3;

        Text(num, capFont, line.IsMain ? Left : CaptionLeft - 2, top);
        double cy = top;
        foreach (string l in capLines) { Text(l, capFont, capLeft, cy); cy += LineHeight(capFont); }
        if (line.Operator is not null)
            G.DrawString(line.Operator, _regular, XBrushes.Black, new XRect(ValuesLeft - OpW, top, OpW - 4, 12), XStringFormats.TopRight);

        for (int c = 0; c < 3; c++)
        {
            string v = line.FormatValue(c);
            string marker = line.Marker(c) ?? "";
            if (c < 2 && _markers.TryGetValue(FootnoteKey(line.Number, c), out var refs))
                marker += string.Concat(refs.Select(n => $"[{n}]"));
            if (v.Length == 0 && marker.Length == 0) continue;
            var rect = new XRect(ValuesLeft + c * ValW, top, ValW - 4, 12);
            G.DrawString(v + marker, line.IsMain ? _bold : _regular, XBrushes.Black, rect, XStringFormats.TopRight);
        }
        _y += h;
        G.DrawLine(new XPen(XColor.FromArgb(170, 170, 170), 0.4), Left, _y, Right, _y);
    }

    private static string FootnoteKey(string line, int column) => line + "|" + column;

    private void CollectFootnotes()
    {
        bool shared = _r.Kind == WorksheetKind.B;
        int n = 0;
        foreach (var note in _case.Footnotes)
        {
            string text = Footnotes.Render(_case, note);
            if (string.IsNullOrWhiteSpace(text)) continue;
            var (line, column) = LineForField(note.Field, shared);
            n++;
            string where = line is null ? "" : $"Line {line}" + (column is int c ? $", {(c == 0 ? _r.Parent1Name : _r.Parent2Name)}" : "");
            _footnotes.Add((n, where, text.Trim()));
            if (line is not null && column is int col)
            {
                string key = FootnoteKey(line, col);
                if (!_markers.TryGetValue(key, out var list)) _markers[key] = list = new();
                list.Add(n);
            }
        }
    }

    /// <summary>Map an input field id (see <see cref="FieldIds"/>) to the worksheet line and column where it appears.</summary>
    public static (string? Line, int? Column) LineForField(string field, bool shared)
    {
        int? column = field.StartsWith("parent1.") ? 0 : field.StartsWith("parent2.") ? 1 : null;
        string name = field.Contains('.') ? field[(field.IndexOf('.') + 1)..] : field;
        string ex = shared ? "13" : "4";
        string? line = name switch
        {
            FieldIds.Income => "1",
            FieldIds.ExistingSupport => "1a",
            FieldIds.AlimonyPaid => "1b",
            FieldIds.AlimonyToParent1 => "1c",
            FieldIds.MultiFamily => "1d",
            FieldIds.ChildCare => ex + "a",
            FieldIds.HealthInsurance => ex + "b",
            FieldIds.ExtraordinaryMedical => ex + "c",
            FieldIds.CashMedicalPercent => ex + "d",
            FieldIds.AdditionalExpenses => ex + "e",
            FieldIds.Parent1Overnights => shared ? "6" : null,
            _ => null,
        };
        if (name == FieldIds.AlimonyToParent1 || name == FieldIds.Parent1Overnights) column = 0;
        return (line, column);
    }

    private void Comments()
    {
        _y += 10;
        Ensure(50);
        string label = _r.Kind == WorksheetKind.A
            ? "Comments or special adjustments, such as (1) any adjustment for certain third party benefits paid to or for the child of an obligor who is disabled, retired, or receiving benefits as a result of a compensable claim (see Code, Family Law Article, § 12-204(j)) or (2) that there is a negative dollar amount on line 9, which indicates a recommended child support order directing the custodial parent to reimburse the non-custodial parent this amount for \"direct pay\" expenses):"
            : "Comments or special adjustments, such as any adjustment for certain third party benefits paid to or for the child of an obligor who is disabled, retired, or receiving benefits as a result of a compensable claim (see Code, Family Law Article, § 12-204(j)):";
        Paragraph(label, _small);
        _y += 2;
        Paragraph(string.IsNullOrWhiteSpace(_case.Comments) ? "(none)" : _case.Comments.Trim(), _regular, 8);
    }

    private void Notes()
    {
        var items = new List<string>();
        foreach (var (num, where, text) in _footnotes)
            items.Add($"[{num}] {(where.Length > 0 ? where + ": " : "")}{text}");
        foreach (var w in _r.Warnings) items.Add("Review: " + w.Text);
        items.AddRange(_r.Notes);
        if (_r.IsReimbursement)
            items.Add($"Line 9 is negative: {_r.ParentName(_r.Payer!.Value)} (custodial parent) reimburses the non-custodial parent {Money.Format(_r.Amount)} a month for direct-pay expenses (Comment (2)).");
        if (items.Count == 0) return;
        _y += 8;
        Heading("NOTES");
        foreach (string item in items)
        {
            Paragraph(item, _small, 8);
            _y += 2;
        }
    }

    private void Signature()
    {
        _y += 14;
        Ensure(30);
        Text("PREPARED BY:", _smallBold, Left, _y);
        Text(_case.Preparer.Trim(), _regular, Left + 62, _y - 1);
        Text("Date:", _smallBold, Right - 150, _y);
        Text(_case.Date.Trim(), _regular, Right - 122, _y - 1);
        _y += 12;
        Rule(0.5, Left + 60, Right - 170);
        Rule(0.5, Right - 124, Right);
    }

    private void Footers()
    {
        int count = _doc.PageCount;
        for (int i = 0; i < count; i++)
        {
            using var g = XGraphics.FromPdfPage(_doc.Pages[i], XGraphicsPdfPageOptions.Append);
            double y = PageH - Margin + 6;
            g.DrawLine(new XPen(XColors.Gray, 0.4), Left, y - 4, Right, y - 4);
            string left = $"{_opt.AppName} {_opt.Version}" + (string.IsNullOrWhiteSpace(_opt.FooterLine) ? "" : " · " + _opt.FooterLine.Trim());
            g.DrawString(left, _footer, XBrushes.Black, new XRect(Left, y, Right - Left - 80, 10), XStringFormats.TopLeft);
            g.DrawString($"{(_r.Kind == WorksheetKind.A ? "Worksheet A" : "Worksheet B")} · Page {i + 1} of {count}", _footer, XBrushes.Black,
                new XRect(Left, y, Right - Left, 10), XStringFormats.TopRight);
        }
    }
}
