// Provides Simulation Snapshot Session Document for the export layer export support.
using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Export;

/// <summary>
/// Represents the simulation snapshot session document.
/// </summary>
public sealed class SimulationSnapshotSessionDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationSnapshotSessionDocument"/> class.
    /// </summary>
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

    /// <summary>
    /// Gets the exported at.
    /// </summary>
    public DateTimeOffset ExportedAt { get; }
    /// <summary>
    /// Gets the configuration summary.
    /// </summary>
    public string? ConfigurationSummary { get; }
    /// <summary>
    /// Gets the steps.
    /// </summary>
    public IReadOnlyList<SimulationExportStep> Steps { get; }
    /// <summary>
    /// Gets the logs.
    /// </summary>
    public IReadOnlyList<SimulationExportLogEntry> Logs { get; }
    /// <summary>
    /// Gets the timeline.
    /// </summary>
    public IReadOnlyList<SimulationSnapshotEntry> Timeline { get; }
    /// <summary>
    /// Gets the signal history.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SimulationSnapshotCurvePoint>> SignalHistory { get; }
    /// <summary>
    /// Gets the selected timeline index.
    /// </summary>
    public int SelectedTimelineIndex { get; }
}
