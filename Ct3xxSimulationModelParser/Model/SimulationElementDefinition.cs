// Provides Simulation Element Definition for the simulation model parser model support.
using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

public abstract class SimulationElementDefinition
{
    /// <summary>
    /// Initializes a new instance of SimulationElementDefinition.
    /// </summary>
    protected SimulationElementDefinition(string id, string type, IReadOnlyDictionary<string, string?>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Element id must be provided.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Element type must be provided.", nameof(type));
        }

        Id = id.Trim();
        Type = type.Trim();
        Metadata = metadata ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Gets the type.
    /// </summary>
    public string Type { get; }
    /// <summary>
    /// Gets the metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Metadata { get; }
}
