using System;
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

public abstract class SimulationElementDefinition
{
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

    public string Id { get; }
    public string Type { get; }
    public IReadOnlyDictionary<string, string?> Metadata { get; }
}
