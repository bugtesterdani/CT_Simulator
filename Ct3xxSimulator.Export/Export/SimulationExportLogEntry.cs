// Provides Simulation Export Log Entry for the export layer export support.
using System;

namespace Ct3xxSimulator.Export;

/// <summary>
/// Represents the simulation export log entry.
/// </summary>
public sealed class SimulationExportLogEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationExportLogEntry"/> class.
    /// </summary>
    public SimulationExportLogEntry(DateTime timestamp, string message)
    {
        Timestamp = timestamp;
        Message = message;
    }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; }
    /// <summary>
    /// Gets the message.
    /// </summary>
    public string Message { get; }
}
