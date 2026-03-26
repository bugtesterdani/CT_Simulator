using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Export;

public sealed class SimulationSnapshotSessionDocument
{
    public SimulationSnapshotSessionDocument(
        DateTimeOffset exportedAt,
        string? configurationSummary,
        IReadOnlyList<SimulationExportStep> steps,
        IReadOnlyList<SimulationExportLogEntry> logs,
        IReadOnlyList<SimulationSnapshotEntry> timeline,
        IReadOnlyDictionary<string, IReadOnlyList<SimulationSnapshotCurvePoint>> signalHistory,
        int selectedTimelineIndex)
    {
        ExportedAt = exportedAt;
        ConfigurationSummary = configurationSummary;
        Steps = steps;
        Logs = logs;
        Timeline = timeline;
        SignalHistory = signalHistory;
        SelectedTimelineIndex = selectedTimelineIndex;
    }

    public DateTimeOffset ExportedAt { get; }
    public string? ConfigurationSummary { get; }
    public IReadOnlyList<SimulationExportStep> Steps { get; }
    public IReadOnlyList<SimulationExportLogEntry> Logs { get; }
    public IReadOnlyList<SimulationSnapshotEntry> Timeline { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<SimulationSnapshotCurvePoint>> SignalHistory { get; }
    public int SelectedTimelineIndex { get; }
}
