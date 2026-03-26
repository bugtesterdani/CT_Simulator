using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ct3xxSimulator.Simulation;

namespace Ct3xxSimulator.Export;

public static class SimulationSnapshotSessionSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void Save(string path, SimulationSnapshotSessionDocument document)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
    }

    public static SimulationSnapshotSessionDocument Load(string path)
    {
        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<SimulationSnapshotSessionDocumentData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Snapshot-Session konnte nicht gelesen werden.");

        return document.ToDocument();
    }

    public static SimulationSnapshotSessionDocument Create(
        DateTimeOffset exportedAt,
        string? configurationSummary,
        IReadOnlyList<SimulationExportStep> steps,
        IReadOnlyList<SimulationExportLogEntry> logs,
        IReadOnlyList<SimulationStateSnapshot> timeline,
        IReadOnlyDictionary<string, IReadOnlyList<MeasurementCurvePoint>> signalHistory,
        int selectedTimelineIndex)
    {
        return new SimulationSnapshotSessionDocument(
            exportedAt,
            configurationSummary,
            steps,
            logs,
            timeline.Select((snapshot, index) => ToEntry(index, snapshot)).ToList(),
            signalHistory.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<SimulationSnapshotCurvePoint>)item.Value
                    .Select(point => new SimulationSnapshotCurvePoint(point.TimeMs, point.Label, point.Value, point.Unit))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase),
            selectedTimelineIndex);
    }

    public static SimulationStateSnapshot ToSnapshot(SimulationSnapshotEntry entry)
    {
        return new SimulationStateSnapshot(
            entry.CurrentStep,
            entry.CurrentTimeMs,
            CopyMap(entry.Signals),
            CopyMap(entry.MeasurementBuses),
            entry.RelayStates.ToList(),
            entry.ActiveFaults.ToList(),
            new ExternalDeviceStateSnapshot(
                entry.ExternalDeviceState.TimeMs,
                CopyMap(entry.ExternalDeviceState.Inputs),
                CopyMap(entry.ExternalDeviceState.Sources),
                CopyMap(entry.ExternalDeviceState.InternalSignals),
                CopyMap(entry.ExternalDeviceState.Outputs),
                CopyMap(entry.ExternalDeviceState.Interfaces)),
            entry.ElementStates.ToList(),
            entry.ActiveConcurrentGroup,
            entry.ConcurrentEvent,
            entry.ConcurrentBranches.Select(branch => new ConcurrentBranchSnapshot(
                branch.BranchName,
                branch.CurrentItem,
                branch.Status,
                branch.WaitUntilTimeMs,
                branch.Details)).ToList());
    }

    private static SimulationSnapshotEntry ToEntry(int index, SimulationStateSnapshot snapshot)
    {
        return new SimulationSnapshotEntry(
            index,
            snapshot.CurrentStep,
            snapshot.CurrentTimeMs,
            CopyMap(snapshot.Signals),
            CopyMap(snapshot.MeasurementBuses),
            snapshot.RelayStates.ToList(),
            snapshot.ActiveFaults.ToList(),
            new SimulationSnapshotExternalDeviceState(
                snapshot.ExternalDeviceState.TimeMs,
                CopyMap(snapshot.ExternalDeviceState.Inputs),
                CopyMap(snapshot.ExternalDeviceState.Sources),
                CopyMap(snapshot.ExternalDeviceState.InternalSignals),
                CopyMap(snapshot.ExternalDeviceState.Outputs),
                CopyMap(snapshot.ExternalDeviceState.Interfaces)),
            snapshot.ElementStates.ToList(),
            snapshot.ActiveConcurrentGroup,
            snapshot.ConcurrentEvent,
            snapshot.ConcurrentBranches.Select(branch => new SimulationSnapshotConcurrentBranch(
                branch.BranchName,
                branch.CurrentItem,
                branch.Status,
                branch.WaitUntilTimeMs,
                branch.Details)).ToList());
    }

    private static Dictionary<string, string> CopyMap(IReadOnlyDictionary<string, string> source)
    {
        return source.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class SimulationSnapshotSessionDocumentData
    {
        public DateTimeOffset ExportedAt { get; set; }
        public string? ConfigurationSummary { get; set; }
        public List<SimulationExportStep>? Steps { get; set; }
        public List<SimulationExportLogEntry>? Logs { get; set; }
        public List<SimulationSnapshotEntry>? Timeline { get; set; }
        public Dictionary<string, List<SimulationSnapshotCurvePoint>>? SignalHistory { get; set; }
        public int SelectedTimelineIndex { get; set; }

        public SimulationSnapshotSessionDocument ToDocument()
        {
            return new SimulationSnapshotSessionDocument(
                ExportedAt,
                ConfigurationSummary,
                Steps ?? new List<SimulationExportStep>(),
                Logs ?? new List<SimulationExportLogEntry>(),
                Timeline ?? new List<SimulationSnapshotEntry>(),
                (SignalHistory ?? new Dictionary<string, List<SimulationSnapshotCurvePoint>>(StringComparer.OrdinalIgnoreCase))
                    .ToDictionary(
                        item => item.Key,
                        item => (IReadOnlyList<SimulationSnapshotCurvePoint>)(item.Value ?? new List<SimulationSnapshotCurvePoint>()),
                        StringComparer.OrdinalIgnoreCase),
                SelectedTimelineIndex);
        }
    }
}
