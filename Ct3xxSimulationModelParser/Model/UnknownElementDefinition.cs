// Provides Unknown Element Definition for the simulation model parser model support.
using System.Collections.Generic;

namespace Ct3xxSimulationModelParser.Model;

/// <summary>
/// Represents the unknown element definition.
/// </summary>
public sealed class UnknownElementDefinition : SimulationElementDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownElementDefinition"/> class.
    /// </summary>
    public UnknownElementDefinition(string id, string type, IReadOnlyDictionary<string, string?> metadata)
        : base(id, type, metadata)
    {
    }
}
