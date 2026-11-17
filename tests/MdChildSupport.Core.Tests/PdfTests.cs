using MdChildSupport.Core.Pdf;
using UglyToad.PdfPig;

namespace MdChildSupport.Core.Tests;

/// <summary>The worksheet PDF contains every line value from the engine, in order; long text wraps cleanly.</summary>
public class PdfTests
{
    private static byte[] Render(CaseData c, PdfOptions? options = null)
    {
        using var ms = new MemoryStream();
        WorksheetPdf.Write(c, ms, options);
        return ms.ToArray();
    }

    private static (string Text, List<UglyToad.PdfPig.Content.Page> Pages, PdfDocument Doc) Extract(byte[] pdf)
    {
        var doc = PdfDocument.Open(pdf);
        var pages = doc.GetPages().ToList();
        return (string.Concat(pages.Select(p => p.Text)), pages, doc);
    }

    private static string Squash(string s) => new(s.Where(ch => !char.IsWhiteSpace(ch)).ToArray());

    public static TheoryData<string> Cases() => new(WorkedCase.Names());

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryLineValueAppearsInOrder(string name)
    {
        var c = WorkedCase.Load(name).Case;
        c.Comments = "Comment for " + name;
        var r = Calculator.Calculate(c);
        var (text, pages, doc) = Extract(Render(c));
        using var _ = doc;
        string flat = Squash(text);

        int pos = 0;
        foreach (var line in r.Lines.Concat(r.WorksheetC))
        {
            for (int col = 0; col < 3; col++)
            {
                string v = line.FormatValue(col);
                if (v.Length == 0) continue;
                int at = flat.IndexOf(Squash(v), pos, StringComparison.Ordinal);
                Assert.True(at >= 0, $"{name}: line {line.Number} column {col} value '{v}' not found after position {pos}");
                pos = at + Squash(v).Length;
            }
        }
        Assert.Contains(Squash(WorksheetPdf.Title(r)), flat);
        Assert.Contains("Comment for " + name, text.Replace("  ", " "));
        foreach (var w in r.Warnings) Assert.Contains(Squash("Review: " + w.Text)[..40], flat);
        Assert.All(pages, p => Assert.Equal(612, p.Width, 0.5));
        Assert.All(pages, p => Assert.Equal(792, p.Height, 0.5));
    }

    [Fact]
    public void CaptionChildrenFooterAndPreparer()
    {
        var c = WorkedCase.Load("S183").Case;
        c.County = "Prince George's County";
        c.CaseNumber = "C-16-FM-26-000777";
        c.Preparer = "Staff Attorney";
        var (text, pages, doc) = Extract(Render(c, new PdfOptions { FooterLine = "Prepared by Test Office", Version = "9.8.7" }));
        using var _ = doc;
        string flat = Squash(text);
        Assert.Contains(Squash("CIRCUIT COURT FOR PRINCE GEORGE'S COUNTY, MARYLAND"), flat);
        Assert.Contains("C-16-FM-26-000777", flat);
        Assert.Contains(Squash("Jordan Example"), flat);
        Assert.Contains(Squash("Riley Example"), flat);
        Assert.Contains(Squash("Child 1 Example"), flat);
        Assert.Contains("01/15/2014", flat);
        Assert.Contains(Squash("Staff Attorney"), flat);
        Assert.Contains("11/17/2026", flat);
        Assert.Contains(Squash("Maryland Child Support Calculator 9.8.7 · Prepared by Test Office"), flat);
        Assert.Contains(Squash("WORKSHEET C"), flat);

        c.Court = CourtType.District;
        var (text2, _, doc2) = Extract(Render(c));
        using var __ = doc2;
        Assert.Contains(Squash("DISTRICT COURT OF MARYLAND FOR PRINCE GEORGE'S COUNTY"), Squash(text2));
    }

    [Fact]
    public void FootnotesPrintWithMarkers()
    {
        var c = WorkedCase.Load("A11").Case;
        var mf = MultiFamily.Calculate(c.Parent2.Income, 2);
        c.SetFootnote(FieldIds.ForParent(Parent.Parent2, FieldIds.MultiFamily), mf.Footnote("Parent 2"));
        var (text, _, doc) = Extract(Render(c));
        using var _d = doc;
        string flat = Squash(text);
        Assert.Contains(Squash("$1,114[1]"), flat);
        Assert.Contains(Squash("[1] Line 1d, Parent 2:"), flat);
        Assert.Contains(Squash("× 75% = $1,114."), flat);
    }

    [Fact]
    public void LongTextWrapsWithinThePage()
    {
        var c = WorkedCase.Load("S183").Case;
        c.Plaintiff = string.Join(" ", Enumerable.Repeat("Alexandria-Konstantina Montgomery-Worthington", 6));
        c.Defendant = new string('W', 300); // one unbreakable word
        c.CaseNumber = "C-02-FM-26-001234-EXTREMELY-LONG-CASE-NUMBER-THAT-KEEPS-GOING-AND-GOING";
        c.Children[0].Name = string.Join(" ", Enumerable.Repeat("Bartholomew", 30));
        c.Comments = string.Join("\n", Enumerable.Range(1, 120).Select(i => $"Comment line {i}: the parties agree that the arrangement continues as before."));
        var (text, pages, doc) = Extract(Render(c));
        using var _ = doc;

        Assert.True(pages.Count >= 2, "long comments should flow onto more pages");
        foreach (var p in pages)
            foreach (var letter in p.Letters)
            {
                Assert.InRange(letter.BoundingBox.Left, 30, 582);
                Assert.InRange(letter.BoundingBox.Right, 30, 582);
                Assert.InRange(letter.BoundingBox.Bottom, 20, 772);
            }
        string flat = Squash(text);
        Assert.Contains(Squash("Comment line 120: the parties agree"), flat);
        Assert.Contains(new string('W', 50), flat);
        Assert.Contains("(continued)", text);
    }

    [Fact]
    public void WritesToFile()
    {
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "tmp"));
        string path = Path.Combine(AppContext.BaseDirectory, "tmp", "worksheet.pdf");
        WorksheetPdf.Write(WorkedCase.Load("A01").Case, path);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 4));
    }
}
