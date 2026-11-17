using PdfSharp.Fonts;

namespace MdChildSupport.Core.Pdf;

/// <summary>
/// Resolves the worksheet font from the fonts installed with Windows (no font files are shipped; DECISIONS D19).
/// Arial is preferred; Segoe UI is the fallback.
/// </summary>
internal sealed class WindowsFontResolver : IFontResolver
{
    public const string FamilyName = "Worksheet";

    private static readonly object Gate = new();
    private static bool _installed;

    private static readonly (string Regular, string Bold)[] Candidates =
    {
        ("arial.ttf", "arialbd.ttf"),
        ("segoeui.ttf", "segoeuib.ttf"),
    };

    public static void EnsureInstalled()
    {
        lock (Gate)
        {
            if (_installed) return;
            // PDFsharp 6.5+ requires the application to declare its licence. This project is AGPL-3.0-or-later,
            // so it uses PDFsharp under the AGPL (docs/licensing.md).
            PdfSharp.PdfSharpLicense.SetAgplLicense();
            if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new WindowsFontResolver();
            _installed = true;
        }
    }

    private static string FontsDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(bold ? "Worksheet#b" : "Worksheet#r");

    public byte[]? GetFont(string faceName)
    {
        bool bold = faceName.EndsWith("#b", StringComparison.Ordinal);
        foreach (var (regular, boldFile) in Candidates)
        {
            string path = Path.Combine(FontsDir, bold ? boldFile : regular);
            if (File.Exists(path)) return File.ReadAllBytes(path);
        }
        throw new FileNotFoundException("Neither Arial nor Segoe UI is installed in " + FontsDir + ".");
    }
}
