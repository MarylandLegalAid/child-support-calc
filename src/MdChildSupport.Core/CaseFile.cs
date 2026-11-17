using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdChildSupport.Core;

/// <summary>
/// Reads and writes the app's own case file format (.mdcase): UTF-8 JSON with a schema version. Only inputs are
/// stored, never computed results. Specification: docs/file-format.md.
/// </summary>
public static class CaseFile
{
    public const string Extension = ".mdcase";
    public const int SchemaVersion = 1;
    public const string FallbackFileName = "child-support-case";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed class Envelope
    {
        public int SchemaVersion { get; set; }
        public string Application { get; set; } = "Maryland Child Support Calculator";
        public CaseData? Case { get; set; }
    }

    public static string Serialize(CaseData data) =>
        JsonSerializer.Serialize(new Envelope { SchemaVersion = SchemaVersion, Case = data }, Options);

    public static CaseData Deserialize(string json)
    {
        Envelope? env;
        try { env = JsonSerializer.Deserialize<Envelope>(json, Options); }
        catch (JsonException e) { throw new InvalidDataException("This is not a valid case file: " + e.Message, e); }
        if (env is null || env.Case is null) throw new InvalidDataException("This is not a valid case file.");
        if (env.SchemaVersion < 1 || env.SchemaVersion > SchemaVersion)
            throw new InvalidDataException($"This case file uses format version {env.SchemaVersion}, which this version of the app cannot read.");
        return Normalize(env.Case);
    }

    public static void Save(string path, CaseData data) => File.WriteAllText(path, Serialize(data), new UTF8Encoding(false));

    public static CaseData Load(string path) => Deserialize(File.ReadAllText(path, Encoding.UTF8));

    /// <summary>Repair anything a hand-edited file could get wrong, so the model invariants hold.</summary>
    private static CaseData Normalize(CaseData c)
    {
        c.CaseNumber ??= ""; c.Plaintiff ??= ""; c.Defendant ??= ""; c.Preparer ??= ""; c.Date ??= ""; c.Comments ??= "";
        if (c.County is null || !Counties.All.Contains(c.County)) c.County = Counties.All[0];
        c.Children ??= new();
        c.Children = c.Children.Where(x => x is not null).Take(CaseData.ChildRows).ToList();
        foreach (var ch in c.Children) { ch.Name ??= ""; ch.BirthDate ??= ""; }
        while (c.Children.Count < CaseData.ChildRows) c.Children.Add(new ChildInfo());
        c.Parent1 ??= new();
        c.Parent2 ??= new();
        c.Footnotes = (c.Footnotes ?? new()).Where(f => f is not null && !string.IsNullOrWhiteSpace(f.Field)).ToList();
        foreach (var f in c.Footnotes) f.Text ??= "";
        return c;
    }

    /// <summary>File name suggested by Save/Export: the case number with invalid characters replaced by "_".</summary>
    public static string SuggestedName(string? caseNumber)
    {
        string s = (caseNumber ?? "").Trim();
        if (s.Length == 0) return FallbackFileName;
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }).ToHashSet();
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s) sb.Append(invalid.Contains(ch) || char.IsControl(ch) ? '_' : ch);
        string result = sb.ToString().Trim().TrimEnd('.');
        return result.Length == 0 ? FallbackFileName : result;
    }
}
