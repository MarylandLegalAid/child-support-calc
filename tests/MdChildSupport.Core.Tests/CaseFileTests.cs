namespace MdChildSupport.Core.Tests;

public class CaseFileTests
{
    [Fact]
    public void RoundTripPreservesEveryInput()
    {
        var rnd = new Random(1);
        for (int n = 0; n < 200; n++)
        {
            var c = PropertyTests.RandomCase(rnd);
            c.CaseNumber = "C-02-FM-26-00" + n;
            c.County = Counties.All[n % Counties.All.Count];
            c.Court = (CourtType)(n % 2);
            c.Plaintiff = "Jordan “Jo” Example";
            c.Defendant = "Riley Ñúñez";
            c.Preparer = "Staff Attorney";
            c.Date = "11/17/2026";
            c.UseParentLabels = n % 3 == 0;
            c.Children[5].BirthDate = "01/02/2020";
            c.Comments = "Line one\nLine two — with a dash";
            c.SetFootnote(FieldIds.ForParent(Parent.Parent2, FieldIds.MultiFamily), "Footnote text");

            string json = CaseFile.Serialize(c);
            var back = CaseFile.Deserialize(json);
            Assert.Equal(json, CaseFile.Serialize(back));
            Assert.Equal(Calculator.Calculate(c).Amount, Calculator.Calculate(back).Amount);
        }
    }

    [Fact]
    public void FileHasSchemaVersionAndNoComputedResults()
    {
        var c = CaseData.CreateNew(new DateTime(2026, 11, 17));
        c.Parent1.CashMedicalPercent = 2.5m;
        string json = CaseFile.Serialize(c);
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"date\": \"11/17/2026\"", json);
        Assert.Contains("\"cashMedicalPercent\": 2.5", json);
        Assert.Contains("\"guidelines\": \"october2025\"", json);
        Assert.Contains("\"custody\": \"shared\"", json);
        foreach (string computed in new[] { "sharePercent", "basic", "amount", "payer", "adjusted", "warnings" })
            Assert.DoesNotContain(computed, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveAndLoadThroughDisk()
    {
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "tmp"));
        string path = Path.Combine(AppContext.BaseDirectory, "tmp", Guid.NewGuid() + CaseFile.Extension);
        try
        {
            var c = CaseData.CreateNew(DateTime.Today);
            c.CaseNumber = "X";
            CaseFile.Save(path, c);
            Assert.Equal("X", CaseFile.Load(path).CaseNumber);
            Assert.NotEqual(0xEF, File.ReadAllBytes(path)[0]); // UTF-8 without BOM
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RejectsNewerOrInvalidFiles()
    {
        Assert.Throws<InvalidDataException>(() => CaseFile.Deserialize("{\"schemaVersion\": 99, \"case\": {}}"));
        Assert.Throws<InvalidDataException>(() => CaseFile.Deserialize("not json"));
        Assert.Throws<InvalidDataException>(() => CaseFile.Deserialize("{\"schemaVersion\": 1}"));
    }

    [Fact]
    public void NormalizesHandEditedFiles()
    {
        var c = CaseFile.Deserialize("{\"schemaVersion\":1,\"case\":{\"county\":\"Nowhere\",\"children\":[{\"name\":\"A\"}]}}");
        Assert.Equal(Counties.All[0], c.County);
        Assert.Equal(6, c.Children.Count);
        Assert.Equal(1, c.ChildCount);
        Assert.Equal(183, c.Parent1Overnights);
    }

    [Theory]
    [InlineData("C-02-FM-26-001234", "C-02-FM-26-001234")]
    [InlineData("12/34:56*7?", "12_34_56_7_")]
    [InlineData("", "child-support-case")]
    [InlineData("   ", "child-support-case")]
    [InlineData("a<b>c|d\"e", "a_b_c_d_e")]
    public void SuggestedName(string caseNumber, string expected) =>
        Assert.Equal(expected, CaseFile.SuggestedName(caseNumber));
}
