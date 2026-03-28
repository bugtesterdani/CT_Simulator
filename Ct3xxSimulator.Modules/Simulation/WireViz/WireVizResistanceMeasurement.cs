using System;
using System.Collections.Generic;

namespace Ct3xxSimulator.Simulation.WireViz;

/// <summary>
/// Describes one resistance/path measurement between two logical CT3xx test points.
/// </summary>
public sealed class WireVizResistanceMeasurement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WireVizResistanceMeasurement"/> class.
    /// </summary>
    public WireVizResistanceMeasurement(
        string sourceSignalName,
        string targetSignalName,
        bool sourceResolved,
        bool targetResolved,
        bool pathFound,
        double? resistanceOhms,
        IReadOnlyList<string>? nodes = null,
        IReadOnlyList<string>? edgeDescriptions = null,
        string? sourceDocumentPath = null,
        string? failureReason = null)
    {
        SourceSignalName = sourceSignalName ?? string.Empty;
        TargetSignalName = targetSignalName ?? string.Empty;
        SourceResolved = sourceResolved;
        TargetResolved = targetResolved;
        PathFound = pathFound;
        ResistanceOhms = resistanceOhms;
        Nodes = nodes ?? Array.Empty<string>();
        EdgeDescriptions = edgeDescriptions ?? Array.Empty<string>();
        SourceDocumentPath = sourceDocumentPath;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Gets the logical source signal or test point.
    /// </summary>
    public string SourceSignalName { get; }

    /// <summary>
    /// Gets the logical target signal or test point.
    /// </summary>
    public string TargetSignalName { get; }

    /// <summary>
    /// Gets a value indicating whether the logical source could be resolved in the loaded wiring.
    /// </summary>
    public bool SourceResolved { get; }

    /// <summary>
    /// Gets a value indicating whether the logical target could be resolved in the loaded wiring.
    /// </summary>
    public bool TargetResolved { get; }

    /// <summary>
    /// Gets a value indicating whether an active path between source and target could be found.
    /// </summary>
    public bool PathFound { get; }

    /// <summary>
    /// Gets the total measured resistance in ohms for the active path.
    /// </summary>
    public double? ResistanceOhms { get; }

    /// <summary>
    /// Gets the ordered path nodes from source to target.
    /// </summary>
    public IReadOnlyList<string> Nodes { get; }

    /// <summary>
    /// Gets the descriptions of the path elements that contributed to the measurement.
    /// </summary>
    public IReadOnlyList<string> EdgeDescriptions { get; }

    /// <summary>
    /// Gets the source document path that produced the measurement.
    /// </summary>
    public string? SourceDocumentPath { get; }

    /// <summary>
    /// Gets the failure reason when no valid measurement could be produced.
    /// </summary>
    public string? FailureReason { get; }
}
