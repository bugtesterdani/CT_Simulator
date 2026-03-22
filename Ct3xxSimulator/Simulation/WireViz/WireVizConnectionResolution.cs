using System;
using System.Collections.Generic;
using System.Linq;
using Ct3xxProgramParser.SignalTables;

namespace Ct3xxSimulator.Simulation.WireViz;

public sealed class WireVizConnectionResolution
{
    public WireVizConnectionResolution(
        SignalAssignment assignment,
        WireVizEndpoint source,
        IReadOnlyList<WireVizEndpoint> targets,
        string sourceDocumentPath)
    {
        Assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Targets = targets ?? Array.Empty<WireVizEndpoint>();
        SourceDocumentPath = sourceDocumentPath ?? string.Empty;
    }

    public SignalAssignment Assignment { get; }
    public WireVizEndpoint Source { get; }
    public IReadOnlyList<WireVizEndpoint> Targets { get; }
    public string SourceDocumentPath { get; }

    public string ToDisplayText()
    {
        var targetText = Targets.Count == 0
            ? "keine Zielpins"
            : string.Join(", ", Targets.Select(target => target.DisplayName));

        return $"{Assignment.Name} -> {Source.DisplayName} [{Source.Role}] -> {targetText}";
    }
}
