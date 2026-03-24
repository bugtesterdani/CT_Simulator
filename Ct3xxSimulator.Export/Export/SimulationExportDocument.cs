using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Export;

public sealed class SimulationExportDocument
{
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

    public DateTimeOffset ExportedAt { get; }
    public string? ConfigurationSummary { get; }
    public IReadOnlyList<SimulationExportStep> Steps { get; }
    public IReadOnlyList<SimulationExportLogEntry> Logs { get; }
}
