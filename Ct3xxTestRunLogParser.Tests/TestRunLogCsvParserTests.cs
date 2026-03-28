using Ct3xxTestRunLogParser.Model;
using Ct3xxTestRunLogParser.Parsing;

namespace Ct3xxTestRunLogParser.Tests;

/// <summary>
/// Verifies parsing and coarse row classification for imported CSV test run logs.
/// </summary>
[TestClass]
public sealed class TestRunLogCsvParserTests
{
    private static readonly string ExampleCsvPath =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "examples", "Test_X.csv"));

    /// <summary>
    /// Ensures that the parser reads the example CSV into a stable run model.
    /// </summary>
    [TestMethod]
    public void ParseFile_ExampleCsv_ReadsExpectedRunMetadata()
    {
        var parser = new TestRunLogCsvParser();

        var run = parser.ParseFile(ExampleCsvPath);

        Assert.AreEqual("75500", run.RunId);
        Assert.AreEqual("2633414", run.SerialNumber);
        Assert.AreEqual(595, run.Steps.Count);
    }

    /// <summary>
    /// Ensures that numeric measurement rows are detected and parsed correctly.
    /// </summary>
    [TestMethod]
    public void ParseFile_ExampleCsv_ClassifiesMeasurementRows()
    {
        var parser = new TestRunLogCsvParser();

        var run = parser.ParseFile(ExampleCsvPath);
        var step = run.Steps.First(item => item.Description == "LRM Ausgangsspannungen messen");

        Assert.AreEqual(ImportedTestRunStepKind.Measurement, step.Kind);
        Assert.AreEqual(8d, step.LowerLimit);
        Assert.AreEqual(10.9154d, step.MeasuredValue!.Value, 0.0001d);
        Assert.AreEqual(12d, step.UpperLimit);
        Assert.AreEqual("PASS", step.Result);
    }

    /// <summary>
    /// Ensures that wait-like rows are classified separately from measurements.
    /// </summary>
    [TestMethod]
    public void ParseFile_ExampleCsv_ClassifiesWaitRows()
    {
        var parser = new TestRunLogCsvParser();

        var run = parser.ParseFile(ExampleCsvPath);
        var step = run.Steps.First(item => item.Description == "Wartezeit");

        Assert.AreEqual(ImportedTestRunStepKind.Wait, step.Kind);
        Assert.AreEqual("Waited for 985 ms", step.Message);
    }

    /// <summary>
    /// Ensures that command-like rows are classified as actions.
    /// </summary>
    [TestMethod]
    public void ParseFile_ExampleCsv_ClassifiesActionRows()
    {
        var parser = new TestRunLogCsvParser();

        var run = parser.ParseFile(ExampleCsvPath);
        var step = run.Steps.First(item => item.Description.StartsWith("Lichtschranke einschalten", StringComparison.Ordinal));

        Assert.AreEqual(ImportedTestRunStepKind.Action, step.Kind);
    }

    /// <summary>
    /// Ensures that invalid headers are rejected early.
    /// </summary>
    [TestMethod]
    public void Parse_InvalidHeader_Throws()
    {
        const string csv = "A;B;C\r\n1;2;3\r\n";
        var parser = new TestRunLogCsvParser();

        using var reader = new StringReader(csv);

        try
        {
            parser.Parse(reader);
            Assert.Fail("Expected InvalidDataException for an unsupported CSV header.");
        }
        catch (InvalidDataException)
        {
            // Expected path.
        }
    }
}
