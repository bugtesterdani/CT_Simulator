using Ct3xxProgramParser.Model;
using Ct3xxTestRunLogParser.Matching;
using Ct3xxTestRunLogParser.Model;

namespace Ct3xxTestRunLogParser.Tests;

/// <summary>
/// Verifies matching between visible CT3xx program steps and imported CSV rows.
/// </summary>
[TestClass]
public sealed class ImportedTestRunMatcherTests
{
    /// <summary>
    /// Ensures that a simple ordered program and CSV run can be matched reliably.
    /// </summary>
    [TestMethod]
    public void Match_SimpleProgramAndCsv_MatchesAllVisibleSteps()
    {
        var program = new Ct3xxProgram
        {
            RootItems =
            {
                CreateTest("IOXX", "Lichtschranke einschalten"),
                CreateTest("PWT$", "Wartezeit"),
                CreatePetTest("LED Auswertung", "Helligkeit LED")
            }
        };

        var run = new ImportedTestRun(
            "memory.csv",
            new[] { "Lauf ID", "Testzeit", "Seriennummer", "Bezeichnung", "Message", "Untere Grenze", "Wert", "Obere Grenze", "Ergebnis" },
            new[]
            {
                CreateCsvStep(2, "Lichtschranke einschalten", "Ausgabe Signal", ImportedTestRunStepKind.Action, null, null, null, "PASS"),
                CreateCsvStep(3, "Wartezeit", "Waited for 985 ms", ImportedTestRunStepKind.Wait, null, null, null, "PASS"),
                CreateCsvStep(4, "Helligkeit LED", null, ImportedTestRunStepKind.Measurement, 0d, 12.3d, 20d, "PASS"),
            },
            "1",
            "SN1");

        var matcher = new ImportedTestRunMatcher();

        var report = matcher.Match(program, run);

        Assert.AreEqual(3, report.ProgramSteps.Count);
        Assert.AreEqual(3, report.RelevantCsvSteps.Count);
        Assert.AreEqual(3, report.Matches.Count);
        Assert.AreEqual(0, report.UnmatchedProgramSteps.Count);
        Assert.AreEqual(0, report.UnmatchedCsvSteps.Count);
        Assert.IsTrue(report.IsReliable);
    }

    /// <summary>
    /// Ensures that PET evaluations are expanded to visible synthetic sub-steps.
    /// </summary>
    [TestMethod]
    public void Match_PetEvaluation_ExpandsMeasurementLabels()
    {
        var program = new Ct3xxProgram
        {
            RootItems =
            {
                CreatePetTest("LED Auswertung", "Helligkeit LED", "Frequenz LED")
            }
        };

        var run = new ImportedTestRun(
            "memory.csv",
            new[] { "Lauf ID", "Testzeit", "Seriennummer", "Bezeichnung", "Message", "Untere Grenze", "Wert", "Obere Grenze", "Ergebnis" },
            new[]
            {
                CreateCsvStep(2, "Helligkeit LED", null, ImportedTestRunStepKind.Measurement, 0d, 12.3d, 20d, "PASS"),
                CreateCsvStep(3, "Frequenz LED", null, ImportedTestRunStepKind.Measurement, 1d, 2d, 3d, "PASS"),
            },
            "1",
            "SN1");

        var matcher = new ImportedTestRunMatcher();

        var report = matcher.Match(program, run);

        Assert.AreEqual(2, report.ProgramSteps.Count);
        Assert.IsTrue(report.ProgramSteps.All(step => step.IsSyntheticEvaluation));
        Assert.AreEqual(2, report.Matches.Count);
        Assert.IsTrue(report.IsReliable);
    }

    /// <summary>
    /// Ensures that large mismatches are flagged as unreliable.
    /// </summary>
    [TestMethod]
    public void Match_UnrelatedCsv_IsMarkedAsUnreliable()
    {
        var program = new Ct3xxProgram
        {
            RootItems =
            {
                CreateTest("IOXX", "HV einschalten"),
                CreateTest("PWT$", "Wartezeit"),
            }
        };

        var run = new ImportedTestRun(
            "memory.csv",
            new[] { "Lauf ID", "Testzeit", "Seriennummer", "Bezeichnung", "Message", "Untere Grenze", "Wert", "Obere Grenze", "Ergebnis" },
            new[]
            {
                CreateCsvStep(2, "Temperaturmessung", null, ImportedTestRunStepKind.Measurement, 10d, 11d, 12d, "PASS"),
                CreateCsvStep(3, "EEPROM lesen", null, ImportedTestRunStepKind.Step, null, null, null, "PASS"),
            },
            "1",
            "SN1");

        var matcher = new ImportedTestRunMatcher();

        var report = matcher.Match(program, run);

        Assert.AreEqual(0, report.Matches.Count);
        Assert.IsFalse(report.IsReliable);
        Assert.AreEqual(2, report.UnmatchedProgramSteps.Count);
        Assert.AreEqual(2, report.UnmatchedCsvSteps.Count);
    }

    /// <summary>
    /// Ensures that intentionally unlogged program steps do not automatically make the match unreliable.
    /// </summary>
    [TestMethod]
    public void Match_MissingProgramLogRows_CanStillBeReliable()
    {
        var program = new Ct3xxProgram
        {
            RootItems =
            {
                CreateTest("IOXX", "HV einschalten"),
                CreateTest("PWT$", "Wartezeit"),
                CreatePetTest("LED Auswertung", "Helligkeit LED")
            }
        };

        var run = new ImportedTestRun(
            "memory.csv",
            new[] { "Lauf ID", "Testzeit", "Seriennummer", "Bezeichnung", "Message", "Untere Grenze", "Wert", "Obere Grenze", "Ergebnis" },
            new[]
            {
                CreateCsvStep(2, "HV einschalten", null, ImportedTestRunStepKind.Action, null, null, null, "PASS"),
                CreateCsvStep(3, "Helligkeit LED", null, ImportedTestRunStepKind.Measurement, 0d, 12.3d, 20d, "PASS"),
            },
            "1",
            "SN1");

        var matcher = new ImportedTestRunMatcher();

        var report = matcher.Match(program, run);

        Assert.AreEqual(3, report.ProgramSteps.Count);
        Assert.AreEqual(2, report.RelevantCsvSteps.Count);
        Assert.AreEqual(2, report.Matches.Count);
        Assert.AreEqual(1, report.UnmatchedProgramSteps.Count, "Der ungematchte Programmschritt entspricht einem nicht geloggten Schritt.");
        Assert.AreEqual(0, report.UnmatchedCsvSteps.Count);
        Assert.IsTrue(report.IsReliable, "Nicht geloggte Programmschritte sollen CSV-gefuehrte Analyse nicht automatisch blockieren.");
    }

    /// <summary>
    /// Ensures that split child steps are exposed to the matcher and can match CSV rows.
    /// </summary>
    [TestMethod]
    public void Match_SplitTestChildren_AreVisibleForMatching()
    {
        var splitTest = CreateTest("2ARB", "Spannungsmessung");
        splitTest.Items.Add(CreateTest("PWT$", "2s Pause"));
        splitTest.Items.Add(CreateTest("E488", "LED Abfrage"));
        splitTest.Items.Add(CreatePetTest("LED Helligkeit auswerten", "Helligkeit LED", "Rot Anteil LED"));
        splitTest.Items.Add(CreateTest("IOXX", "Relais schalten"));

        var program = new Ct3xxProgram();
        program.RootItems.Add(splitTest);

        var run = new ImportedTestRun(
            "memory.csv",
            new[] { "Lauf ID", "Testzeit", "Seriennummer", "Bezeichnung", "Message", "Untere Grenze", "Wert", "Obere Grenze", "Ergebnis" },
            new[]
            {
                CreateCsvStep(2, "2s Pause", null, ImportedTestRunStepKind.Wait, null, null, null, "PASS"),
                CreateCsvStep(3, "LED Abfrage", null, ImportedTestRunStepKind.Step, null, null, null, "PASS"),
                CreateCsvStep(4, "Helligkeit LED", "LED Aus", ImportedTestRunStepKind.Measurement, 0d, 0d, 0d, "PASS"),
                CreateCsvStep(5, "Rot Anteil LED", "LED Aus", ImportedTestRunStepKind.Measurement, 0d, 0d, 0d, "PASS"),
            },
            "1",
            "SN1");

        var matcher = new ImportedTestRunMatcher();

        var report = matcher.Match(program, run);

        Assert.IsTrue(report.ProgramSteps.Count >= 5);
        Assert.IsTrue(report.Matches.Any(match => match.ProgramStep.DisplayName == "2s Pause"));
        Assert.IsTrue(report.Matches.Any(match => match.ProgramStep.DisplayName == "LED Abfrage"));
        Assert.IsTrue(report.Matches.Any(match => match.ProgramStep.DisplayName == "Helligkeit LED"));
        Assert.IsTrue(report.Matches.Any(match => match.ProgramStep.DisplayName == "Rot Anteil LED"));
        Assert.IsTrue(report.IsReliable);
    }

    /// <summary>
    /// Executes CreateTest.
    /// </summary>
    private static Test CreateTest(string id, string name)
    {
        return new Test
        {
            Id = id,
            Name = name,
            Parameters = new TestParameters
            {
                Name = name
            }
        };
    }

    /// <summary>
    /// Executes CreatePetTest.
    /// </summary>
    private static Test CreatePetTest(string name, params string[] labels)
    {
        var table = new Table();
        for (var index = 0; index < labels.Length; index++)
        {
            table.Records.Add(new Record
            {
                Id = "R" + index,
                DrawingReference = labels[index]
            });
        }

        return new Test
        {
            Id = "PET$",
            Name = name,
            Parameters = new TestParameters
            {
                Name = name,
                Tables = { table }
            }
        };
    }

    /// <summary>
    /// Executes CreateCsvStep.
    /// </summary>
    private static ImportedTestRunStep CreateCsvStep(
        int rowNumber,
        string description,
        string? message,
        ImportedTestRunStepKind kind,
        double? lower,
        double? measured,
        double? upper,
        string result)
    {
        return new ImportedTestRunStep(
            rowNumber,
            "1",
            "0",
            "SN1",
            description,
            message,
            lower?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            measured?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            upper?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            result,
            kind,
            lower,
            measured,
            upper);
    }
}
