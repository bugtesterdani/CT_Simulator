// Provides CSV replay view model tests for the simulator test project support code.
using Ct3xxSimulator.Desktop.ViewModels;

namespace Ct3xxSimulator.Tests;

[TestClass]
/// <summary>
/// Represents the CSV replay view model tests.
/// </summary>
public sealed class CsvReplayViewModelTests
{
    [TestMethod]
    /// <summary>
    /// Executes step result should expose simulator values in compare mode.
    /// </summary>
    public void StepResult_ShouldExpose_SimulatorValues_InCompareMode()
    {
        var result = new StepResultViewModel(
            "LED Abfrage",
            "PASS",
            "15",
            "10",
            "20",
            "V",
            "Simulator details",
            csvRowNumber: 14,
            csvDescription: "LED Abfrage",
            csvMessage: "CSV details",
            csvOutcome: "FAIL",
            csvMeasuredValue: "9",
            csvLowerLimit: "10",
            csvUpperLimit: "20",
            csvMatchReason: "Name+Reihenfolge",
            csvDisplayMode: "Compare");

        Assert.AreEqual("SIM/CSV", result.ResultSourceLabel);
        Assert.AreEqual("PASS", result.DisplayOutcome);
        Assert.AreEqual("15", result.DisplayMeasuredValue);
        Assert.AreEqual("10", result.DisplayLowerLimit);
        Assert.AreEqual("20", result.DisplayUpperLimit);
        Assert.AreEqual("V", result.DisplayUnit);
        Assert.AreEqual("Simulator details", result.DisplayDetails);
        Assert.IsTrue(result.HasComparisonSummary);
        StringAssert.Contains(result.ComparisonSummary, "SIM PASS | CSV FAIL");
    }

    [TestMethod]
    /// <summary>
    /// Executes step result should expose csv values in csv drives mode.
    /// </summary>
    public void StepResult_ShouldExpose_CsvValues_InCsvDrivesMode()
    {
        var result = new StepResultViewModel(
            "LED Abfrage",
            "PASS",
            "15",
            "10",
            "20",
            "V",
            "Simulator details",
            csvRowNumber: 14,
            csvDescription: "LED Abfrage",
            csvMessage: "CSV details",
            csvOutcome: "FAIL",
            csvMeasuredValue: "9",
            csvLowerLimit: "8",
            csvUpperLimit: "12",
            csvMatchReason: "Name+Reihenfolge",
            csvDisplayMode: "CsvDrivesResult");

        Assert.AreEqual("CSV", result.ResultSourceLabel);
        Assert.AreEqual("FAIL", result.DisplayOutcome);
        Assert.AreEqual("9", result.DisplayMeasuredValue);
        Assert.AreEqual("8", result.DisplayLowerLimit);
        Assert.AreEqual("12", result.DisplayUpperLimit);
        Assert.AreEqual(string.Empty, result.DisplayUnit);
        Assert.AreEqual("CSV details", result.DisplayDetails);
        Assert.IsTrue(result.UsesCsvAsPrimary);
    }

    [TestMethod]
    /// <summary>
    /// Executes step tree node should surface display values and comparison details.
    /// </summary>
    public void StepTreeNode_ShouldUse_DisplayValues_AndComparisonDetails()
    {
        var result = new StepResultViewModel(
            "Helligkeit LED",
            "PASS",
            "15",
            "10",
            "20",
            "cd",
            "Simulator details",
            csvRowNumber: 22,
            csvDescription: "Helligkeit LED",
            csvMessage: "CSV fail",
            csvOutcome: "FAIL",
            csvMeasuredValue: "8",
            csvLowerLimit: "10",
            csvUpperLimit: "20",
            csvMatchReason: "Lookahead",
            csvDisplayMode: "Compare");
        var node = new StepTreeNodeViewModel("Helligkeit LED", isGroup: false, "test:1");

        node.ApplyResult(result);
        node.Refresh();

        Assert.AreEqual("SIM/CSV", node.ResultSourceLabel);
        Assert.AreEqual("PASS", node.Outcome);
        Assert.AreEqual("15 cd", node.ValueSummary);
        Assert.AreEqual("10 .. 20 cd", node.RangeSummary);
        StringAssert.Contains(node.DetailLine, "SIM PASS | CSV FAIL");
    }

    [TestMethod]
    /// <summary>
    /// Executes step result should mark unlogged steps while csv replay stays active.
    /// </summary>
    public void StepResult_ShouldMark_UnloggedSteps_WhenCsvReplayIsActive()
    {
        var result = new StepResultViewModel(
            "Hilfsschritt",
            "PASS",
            "1",
            "-",
            "-",
            string.Empty,
            "Simulator details",
            csvDisplayMode: "Compare",
            csvLogFlags: "EF",
            csvLogExpectedForOutcome: false);

        Assert.IsTrue(result.HasActiveCsvReplay);
        Assert.IsTrue(result.IsUnloggedInActiveCsvReplay);
        Assert.IsFalse(result.IsMissingExpectedCsvReplayEntry);
        Assert.AreEqual("SIM", result.ResultSourceLabel);
        Assert.AreEqual("#FFE3E5E8", result.OutcomeBadgeBackground);
        Assert.AreEqual("#FF4B5563", result.OutcomeBadgeForeground);
        StringAssert.Contains(result.ComparisonSummary, "Kein CSV-Eintrag");
        Assert.IsTrue(result.HasComparisonSummary);
    }

    [TestMethod]
    /// <summary>
    /// Executes step result should highlight missing csv rows when log flags require a log entry.
    /// </summary>
    public void StepResult_ShouldHighlight_MissingRequiredCsvEntry_WhenLogFlagsRequireLogging()
    {
        var result = new StepResultViewModel(
            "LED Abfrage",
            "PASS",
            "15",
            "10",
            "20",
            "V",
            "Simulator details",
            csvDisplayMode: "Compare",
            csvLogFlags: "EFP",
            csvLogExpectedForOutcome: true);

        Assert.IsTrue(result.HasActiveCsvReplay);
        Assert.IsFalse(result.IsUnloggedInActiveCsvReplay);
        Assert.IsTrue(result.IsMissingExpectedCsvReplayEntry);
        Assert.AreEqual("SIM", result.ResultSourceLabel);
        Assert.AreEqual("#FFF8E8EA", result.OutcomeBadgeBackground);
        Assert.AreEqual("#FF8B1E3F", result.OutcomeBadgeForeground);
        StringAssert.Contains(result.ComparisonSummary, "LogFlags 'EFP'");
        Assert.IsTrue(result.HasComparisonSummary);
    }
}
