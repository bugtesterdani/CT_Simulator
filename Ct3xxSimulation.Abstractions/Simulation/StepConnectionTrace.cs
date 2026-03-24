using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation;

public sealed class StepConnectionTrace
{
    public StepConnectionTrace(string title, IReadOnlyList<string> nodes)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Trace title must be provided.", nameof(title));
        }

        Title = title;
        Nodes = nodes ?? Array.Empty<string>();
    }

    public string Title { get; }
    public IReadOnlyList<string> Nodes { get; }
}
