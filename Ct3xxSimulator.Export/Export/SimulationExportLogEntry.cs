using System;

namespace Ct3xxSimulator.Export;

public sealed class SimulationExportLogEntry
{
    public SimulationExportLogEntry(DateTime timestamp, string message)
    {
        Timestamp = timestamp;
        Message = message;
    }

    public DateTime Timestamp { get; }
    public string Message { get; }
}
