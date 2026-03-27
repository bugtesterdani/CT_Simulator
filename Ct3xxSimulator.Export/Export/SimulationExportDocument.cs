// Provides Simulation Export Document for the export layer export support.
using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Export;

/// <summary>
/// Represents the simulation export document.
/// </summary>
public sealed class SimulationExportDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationExportDocument"/> class.
    /// </summary>
    public SimulationExportDocument(
        DateTimeOffset exportedAt,
        string? configurationSummary,
        IReadOnlyList<SimulationExportStep> steps,
        IReadOnlyList<SimulationExportLogEntry> logs)
    {
        ExportedAt = exportedAt;
        ConfigurationSummary = configurationSummary;
        Steps = steps;
        Logs = logs;
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
}
