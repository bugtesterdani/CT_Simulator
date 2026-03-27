using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation.WireViz;

/// <summary>
/// Represents one resolved signal path through the WireViz graph.
/// </summary>
public sealed class WireVizSignalTrace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizSignalTrace"/> class.
    /// </summary>
    /// <param name="signalName">The logical signal name represented by the trace.</param>
    /// <param name="nodes">The ordered nodes visited by the trace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="signalName"/> is missing.</exception>
    public WireVizSignalTrace(string signalName, IReadOnlyList<string> nodes)
    {
        if (string.IsNullOrWhiteSpace(signalName))
        {
            throw new ArgumentException("Signal name must be provided.", nameof(signalName));
        }

        SignalName = signalName;
        Nodes = nodes ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets the logical signal name represented by the trace.
    /// </summary>
    public string SignalName { get; }
    /// <summary>
    /// Gets the ordered nodes visited by the trace.
    /// </summary>
    public IReadOnlyList<string> Nodes { get; }
}
