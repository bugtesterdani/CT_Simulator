// Provides Snapshot Session Persistence Tests for the simulator test project support code.
using Ct3xxSimulator.Export;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Tests;

[TestClass]
/// <summary>
/// Represents the snapshot session persistence tests.
/// </summary>
public sealed class SnapshotSessionPersistenceTests
{
    [TestMethod]
    /// <summary>
    /// Executes snapshot session should roundtrip timeline and history.
    /// </summary>
    public void SnapshotSession_ShouldRoundtrip_TimelineAndHistory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"ct3xx-snapshot-{Guid.NewGuid():N}.snapshot.json");
        try
        {
            var steps = new List<SimulationExportStep>
            {
                new("ON", "PASS", "3.3", "3.2", "3.5", "V", "ok", Array.Empty<StepConnectionTrace>(), Array.Empty<MeasurementCurvePoint>(), 0, 12, "ON", "CSV says fail", "FAIL", "1.5", "3.2", "3.5", "Name+Reihenfolge", "CsvDrivesResult")
            };
            var logs = new List<SimulationExportLogEntry>
            {
                new(DateTime.Parse("2026-03-26T10:00:00"), "Snapshot gespeichert")
            };
            var snapshots = new List<SimulationStateSnapshot>
            {
                new(
                    "ON",
                    3000,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ADC_IN"] = "4" },
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["$Result"] = "PASS" },
                    new List<string> { "RELAIS=closed" },
                    new List<string>(),
                    new ExternalDeviceStateSnapshot(
                        3000,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ADC_IN"] = "4" },
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DIG_OUT"] = "3.3" },
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
                    new List<string> { "relay: RELAIS closed" },
                    "Parallel checking",
                    "branch_resumed:LED Auswertung",
                    new List<ConcurrentBranchSnapshot>
                    {
                        new("LED Auswertung", "PWT$: 2s Pause", "running", null, "Wait abgeschlossen")
                    })
            };
            var history = new Dictionary<string, IReadOnlyList<MeasurementCurvePoint>>(StringComparer.OrdinalIgnoreCase)
            {
                ["DUT OUT DIG_OUT"] = new List<MeasurementCurvePoint>
                {
                    new(0, "DUT OUT DIG_OUT", 0, "V"),
                    new(3000, "DUT OUT DIG_OUT", 3.3, "V")
                }
            };

            var document = SimulationSnapshotSessionSerializer.Create(
                DateTimeOffset.Parse("2026-03-26T10:05:00+01:00"),
                "Konfiguration",
                steps,
                logs,
                snapshots,
                history,
                0);

            SimulationSnapshotSessionSerializer.Save(tempPath, document);
            var loaded = SimulationSnapshotSessionSerializer.Load(tempPath);

            Assert.AreEqual("Konfiguration", loaded.ConfigurationSummary);
            Assert.AreEqual(1, loaded.Timeline.Count);
            Assert.AreEqual(1, loaded.Steps.Count);
            Assert.AreEqual(1, loaded.SignalHistory.Count);
            Assert.AreEqual(0, loaded.SelectedTimelineIndex);
            Assert.AreEqual(0, loaded.Steps[0].TimelineIndex);
            Assert.AreEqual(12, loaded.Steps[0].CsvRowNumber);
            Assert.AreEqual("FAIL", loaded.Steps[0].CsvOutcome);
            Assert.AreEqual("CsvDrivesResult", loaded.Steps[0].CsvDisplayMode);

            var restored = SimulationSnapshotSessionSerializer.ToSnapshot(loaded.Timeline[0]);
            Assert.AreEqual("Parallel checking", restored.ActiveConcurrentGroup);
            Assert.AreEqual("branch_resumed:LED Auswertung", restored.ConcurrentEvent);
            Assert.AreEqual("3.3", restored.ExternalDeviceState.Outputs["DIG_OUT"]);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
