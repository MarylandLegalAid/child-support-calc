namespace MdChildSupport.Core.Tests;

/// <summary>A hand-worked case from tests/cases/*.md: inputs (JSON block) plus the expected worksheet table.</summary>
public sealed class WorkedCase
{
    public required string Name { get; init; }
    public required CaseData Case { get; init; }
    public required List<(string Line, string[] Values)> Expected { get; init; }
    public required string Result { get; init; }
    public required string[] Warnings { get; init; }

    public static string Directory => Path.Combine(AppContext.BaseDirectory, "cases");

    public static IEnumerable<string> Names() =>
        System.IO.Directory.GetFiles(Directory, "*.md").Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.Equals(n, "README", StringComparison.OrdinalIgnoreCase)).Order()!;

    public static WorkedCase Load(string name)
    {
        string[] lines = File.ReadAllLines(Path.Combine(Directory, name + ".md"));
        int jsonStart = Array.IndexOf(lines, "```json");
        int jsonEnd = Array.IndexOf(lines, "```", jsonStart + 1);
        var data = CaseFile.Deserialize(string.Join('\n', lines[(jsonStart + 1)..jsonEnd]));

        int expectedAt = Array.IndexOf(lines, "## Expected");
        var rows = new List<(string, string[])>();
        foreach (string l in lines.Skip(expectedAt + 1))
        {
            if (!l.StartsWith("| ") || l.StartsWith("| Line")) continue;
            string[] cells = l.Split('|').Select(s => s.Trim()).ToArray();
            rows.Add((cells[1], new[] { cells[2], cells[3], cells[4] }));
        }
        string result = lines.Single(l => l.StartsWith("Result: "))["Result: ".Length..];
        string warn = lines.Single(l => l.StartsWith("Warnings: "))["Warnings: ".Length..];
        return new WorkedCase
        {
            Name = name, Case = data, Expected = rows, Result = result,
            Warnings = warn == "none" ? Array.Empty<string>() : warn.Split(", "),
        };
    }

    public static string Describe(CalculationResult r) => r.Payer is null
        ? "none"
        : $"{r.Payer} {Money.Format(r.Amount)}" + (r.IsReimbursement ? " reimbursement" : "");
}
