using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation;

/// <summary>
/// Describes one resolved signal path that contributed to a simulated step result.
/// </summary>
public sealed class StepConnectionTrace
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StepConnectionTrace"/> class.
    /// </summary>
    /// <param name="title">The display title of the trace.</param>
    /// <param name="nodes">The ordered nodes that form the trace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is missing.</exception>
    public StepConnectionTrace(string title, IReadOnlyList<string> nodes)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Trace title must be provided.", nameof(title));
        }

        Title = title;
        Nodes = nodes ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets the display title of the trace.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the ordered nodes that form the trace from source to target.
    /// </summary>
    public IReadOnlyList<string> Nodes { get; }
}
