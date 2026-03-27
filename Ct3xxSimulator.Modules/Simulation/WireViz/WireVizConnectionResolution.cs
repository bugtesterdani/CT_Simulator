using System;
using System.Collections.Generic;
using System.Linq;
using Ct3xxProgramParser.SignalTables;

namespace Ct3xxSimulator.Simulation.WireViz;

/// <summary>
/// Represents the resolved mapping from one CT3xx signal assignment to concrete WireViz endpoints.
/// </summary>
public sealed class WireVizConnectionResolution
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizConnectionResolution"/> class.
    /// </summary>
    /// <param name="assignment">The originating CT3xx signal assignment.</param>
    /// <param name="source">The resolved source endpoint.</param>
    /// <param name="targets">The resolved target endpoints.</param>
    /// <param name="sourceDocumentPath">The WireViz document path that produced the resolution.</param>
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

    /// <summary>
    /// Gets the originating CT3xx signal assignment.
    /// </summary>
    public SignalAssignment Assignment { get; }
    /// <summary>
    /// Gets the resolved source endpoint.
    /// </summary>
    public WireVizEndpoint Source { get; }
    /// <summary>
    /// Gets the resolved target endpoints.
    /// </summary>
    public IReadOnlyList<WireVizEndpoint> Targets { get; }
    /// <summary>
    /// Gets the source WireViz document path that produced the resolution.
    /// </summary>
    public string SourceDocumentPath { get; }

    /// <summary>
    /// Formats the resolution into a human-readable display string.
    /// </summary>
    /// <returns>The formatted display text.</returns>
    public string ToDisplayText()
    {
        var targetText = Targets.Count == 0
            ? "keine Zielpins"
            : string.Join(", ", Targets.Select(target => target.DisplayName));

        return $"{Assignment.Name} -> {Source.DisplayName} [{Source.Role}] -> {targetText}";
    }
}
