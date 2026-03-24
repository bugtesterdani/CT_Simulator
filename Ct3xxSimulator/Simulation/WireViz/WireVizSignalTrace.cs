using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed class WireVizSignalTrace
{
    public WireVizSignalTrace(string signalName, IReadOnlyList<string> nodes)
    {
        if (string.IsNullOrWhiteSpace(signalName))
        {
            throw new ArgumentException("Signal name must be provided.", nameof(signalName));
        }

        SignalName = signalName;
        Nodes = nodes ?? Array.Empty<string>();
    }

    public string SignalName { get; }
    public IReadOnlyList<string> Nodes { get; }
}
