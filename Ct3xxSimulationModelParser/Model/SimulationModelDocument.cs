// Provides Simulation Model Document for the simulation model parser model support.
using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

/// <summary>
/// Represents the simulation model document.
/// </summary>
public sealed class SimulationModelDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimulationModelDocument"/> class.
    /// </summary>
    public SimulationModelDocument(string? sourcePath, IReadOnlyList<SimulationElementDefinition> elements)
    {
        SourcePath = sourcePath;
        Elements = elements ?? Array.Empty<SimulationElementDefinition>();
    }

    /// <summary>
    /// Gets the source path.
    /// </summary>
    public string? SourcePath { get; }
    /// <summary>
    /// Gets the elements.
    /// </summary>
    public IReadOnlyList<SimulationElementDefinition> Elements { get; }
}
